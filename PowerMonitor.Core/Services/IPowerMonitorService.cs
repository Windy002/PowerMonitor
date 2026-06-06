using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public class PowerSnapshot
{
    public DateTime Timestamp { get; set; }
    public double TotalWatts { get; set; }
    public List<ComponentPower> Components { get; set; } = new();
    public BillingPeriod DayBilling { get; set; } = new();
    public BillingPeriod WeekBilling { get; set; } = new();
    public BillingPeriod MonthBilling { get; set; } = new();
    public SamplingState State { get; set; }
}

public interface IPowerMonitorService
{
    event EventHandler<PowerSnapshot>? SnapshotUpdated;
    event EventHandler<string>? SensorWarning;
    void Start();
    void Pause();
    void Resume();
    void Stop();
    void SetPricePerKwh(double price);
    double GetPricePerKwh();
    void SetInterval(int seconds);
    int GetInterval();
    PowerSnapshot GetLatestSnapshot();
    List<PowerSample> GetHistory(DateTime from, DateTime to);
    void ExportCsv(DateTime from, DateTime to, string filePath);
    void CleanupOldData(int retentionDays);
    void SetRetentionDays(int days);
    int GetRetentionDays();
}
