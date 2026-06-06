using System.Text.Json;
using PowerMonitor.Core.Data;
using PowerMonitor.Core.Logging;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public class PowerMonitorService : IPowerMonitorService, IDisposable
{
    private readonly ISensorService _sensor;
    private readonly IPowerEstimationEngine _estimation;
    private readonly IBillingCalculator _billing;
    private readonly ISamplingScheduler _scheduler;
    private readonly PowerDbContext _db;
    private readonly List<(DateTime Timestamp, double TotalWatts)> _sessionSamples = new();
    private readonly object _lock = new();
    private int _consecutiveEmptyReads = 0;

    public event EventHandler<PowerSnapshot>? SnapshotUpdated;
    public event EventHandler<string>? SensorWarning;

    public PowerMonitorService(
        ISensorService sensor,
        IPowerEstimationEngine estimation,
        IBillingCalculator billing,
        ISamplingScheduler scheduler,
        string dbPath)
    {
        _sensor = sensor;
        _estimation = estimation;
        _billing = billing;
        _scheduler = scheduler;
        _db = new PowerDbContext(dbPath);

        _scheduler.SampleTick += OnSampleTick;
    }

    public void Start()
    {
        if (!_sensor.Initialize())
        {
            Logger.Error("Sensor initialization failed");
            throw new InvalidOperationException("无法访问硬件传感器，请以管理员权限运行");
        }
        // Auto-cleanup old data on startup
        var retentionDaysStr = _db.GetConfig("retention_days", "90");
        if (int.TryParse(retentionDaysStr, out var retentionDays))
        {
            CleanupOldData(retentionDays);
        }
        _scheduler.Start();
    }

    public void Pause() => _scheduler.Pause();
    public void Resume() => _scheduler.Resume();
    public void Stop() => _scheduler.Stop();

    public void SetPricePerKwh(double price) => _db.SetConfig("price_per_kwh", price.ToString("F2"));
    public double GetPricePerKwh()
    {
        var val = _db.GetConfig("price_per_kwh", "0.6");
        return double.TryParse(val, out var price) ? price : 0.6;
    }

    public void SetInterval(int seconds) => _scheduler.IntervalSeconds = seconds;
    public int GetInterval() => _scheduler.IntervalSeconds;

    public PowerSnapshot GetLatestSnapshot() => BuildSnapshot();

    public List<PowerSample> GetHistory(DateTime from, DateTime to) => _db.GetSamples(from, to);

    public void ExportCsv(DateTime from, DateTime to, string filePath)
    {
        var samples = _db.GetSamples(from, to);
        using var writer = new StreamWriter(filePath);
        writer.WriteLine("时间,总功率(W),电价(元/kWh),组件明细");
        foreach (var s in samples)
        {
            writer.WriteLine($"{s.Timestamp:yyyy-MM-dd HH:mm:ss},{s.TotalWatts:F1},{s.PricePerKwh:F2},\"{s.SensorJson}\"");
        }
    }

    public void CleanupOldData(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        _db.DeleteSamplesOlderThan(cutoff);
        Logger.Info($"Cleaned up data older than {cutoff:yyyy-MM-dd}");
    }

    public void SetRetentionDays(int days)
    {
        _db.SetConfig("retention_days", days.ToString());
    }

    public int GetRetentionDays()
    {
        var val = _db.GetConfig("retention_days", "90");
        return int.TryParse(val, out var days) ? days : 90;
    }

    private void OnSampleTick(object? sender, EventArgs e)
    {
        try
        {
            var sensorComponents = _sensor.ReadSensors();
            if (sensorComponents.Count == 0)
            {
                _consecutiveEmptyReads++;
                Logger.Warn($"No sensor data read this tick (consecutive: {_consecutiveEmptyReads})");
                if (_consecutiveEmptyReads == 3)
                {
                    SensorWarning?.Invoke(this, "传感器连续3次读取失败，请检查硬件监控驱动");
                }
                return;
            }
            _consecutiveEmptyReads = 0;

            var sensorTotal = sensorComponents.Sum(c => c.Watts);
            var hwInfo = _sensor.GetHardwareInfo();
            var estimatedComponents = _estimation.EstimateComponents(hwInfo, sensorTotal);

            var allComponents = new List<ComponentPower>();
            allComponents.AddRange(sensorComponents);
            allComponents.AddRange(estimatedComponents);

            var totalWatts = allComponents.Sum(c => c.Watts);
            var pricePerKwh = GetPricePerKwh();

            lock (_lock)
            {
                _sessionSamples.Add((DateTime.Now, totalWatts));
            }

            var sample = new PowerSample
            {
                Timestamp = DateTime.Now,
                TotalWatts = Math.Round(totalWatts, 1),
                SensorJson = JsonSerializer.Serialize(allComponents),
                PricePerKwh = pricePerKwh
            };

            try
            {
                _db.InsertSample(sample);
            }
            catch (Exception ex)
            {
                Logger.Error($"Database write failed: {ex.Message}");
            }

            var snapshot = BuildSnapshot();
            SnapshotUpdated?.Invoke(this, snapshot);
        }
        catch (Exception ex)
        {
            Logger.Error($"Sample tick error: {ex.Message}");
        }
    }

    private PowerSnapshot BuildSnapshot()
    {
        List<(DateTime, double)> samples;
        lock (_lock)
        {
            samples = _sessionSamples.ToList();
        }

        var pricePerKwh = GetPricePerKwh();

        return new PowerSnapshot
        {
            Timestamp = DateTime.Now,
            TotalWatts = samples.Count > 0 ? samples.Last().Item2 : 0,
            Components = samples.Count > 0
                ? JsonSerializer.Deserialize<List<ComponentPower>>(
                    _db.GetLatestSample()?.SensorJson ?? "[]") ?? new List<ComponentPower>()
                : new List<ComponentPower>(),
            DayBilling = _billing.CalculateDayBilling(samples, pricePerKwh),
            WeekBilling = _billing.CalculateWeekBilling(samples, pricePerKwh),
            MonthBilling = _billing.CalculateMonthBilling(samples, pricePerKwh),
            State = _scheduler.State
        };
    }

    public void Dispose()
    {
        _scheduler.SampleTick -= OnSampleTick;
        _scheduler.Dispose();
        _db.Dispose();
    }
}
