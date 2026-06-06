# 主机功耗监控工具 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows desktop power monitor that samples hardware sensors via LibreHardwareMonitor, calculates electricity cost, and displays a real-time WPF dashboard with system tray support.

**Architecture:** Single-process .NET 8 WPF app with a class library core. The core handles sensor reading (LibreHardwareMonitor), power estimation, billing math, SQLite storage, and sampling orchestration. The WPF app provides dashboard, settings, history views and system tray integration via MVVM pattern.

**Tech Stack:** .NET 8, WPF, LibreHardwareMonitor NuGet, Microsoft.Data.Sqlite, OxyPlot.Wpf (charts), xUnit

---

## File Structure

```
PowerMonitor/
├── PowerMonitor.sln
├── PowerMonitor.Core/                      # Class library (.NET 8)
│   ├── PowerMonitor.Core.csproj
│   ├── Models/
│   │   ├── ComponentPower.cs               # Per-component power reading
│   │   └── PowerSample.cs                  # DB record + aggregates
│   ├── Data/
│   │   └── PowerDbContext.cs               # SQLite init, CRUD, migration
│   ├── Services/
│   │   ├── ISensorService.cs               # Interface for hardware sensor reading
│   │   ├── SensorService.cs                # LibreHardwareMonitor wrapper
│   │   ├── IPowerEstimationEngine.cs       # Interface for estimation
│   │   ├── PowerEstimationEngine.cs        # Static estimates for unsensored components
│   │   ├── IBillingCalculator.cs           # Interface for billing
│   │   ├── BillingCalculator.cs            # kWh conversion and cost accumulation
│   │   ├── ISamplingScheduler.cs           # Interface for sampling lifecycle
│   │   ├── SamplingScheduler.cs            # Timer-based orchestrator
│   │   ├── IPowerMonitorService.cs         # Top-level service interface
│   │   └── PowerMonitorService.cs          # Wires sensor + estimation + billing + storage
│   └── Logging/
│       └── Logger.cs                       # Simple file logger
├── PowerMonitor.App/                       # WPF application (.NET 8)
│   ├── PowerMonitor.App.csproj
│   ├── App.xaml
│   ├── App.xaml.cs                         # Startup, tray icon, DI setup
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── ViewModels/
│   │   ├── MainViewModel.cs                # Dashboard bindings
│   │   ├── SettingsViewModel.cs            # Settings bindings
│   │   └── HistoryViewModel.cs             # History bindings
│   ├── Views/
│   │   ├── DashboardView.xaml
│   │   ├── DashboardView.xaml.cs
│   │   ├── SettingsView.xaml
│   │   ├── SettingsView.xaml.cs
│   │   ├── HistoryView.xaml
│   │   └── HistoryView.xaml.cs
│   └── Controls/
│       └── TrayIconManager.cs              # System tray icon management
└── PowerMonitor.Tests/                     # xUnit test project (.NET 8)
    ├── PowerMonitor.Tests.csproj
    ├── BillingCalculatorTests.cs
    └── PowerEstimationEngineTests.cs
```

---

### Task 1: Solution and Project Scaffolding

**Files:**
- Create: `PowerMonitor.sln`
- Create: `PowerMonitor.Core/PowerMonitor.Core.csproj`
- Create: `PowerMonitor.App/PowerMonitor.App.csproj`
- Create: `PowerMonitor.Tests/PowerMonitor.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

```bash
mkdir -p PowerMonitor
cd PowerMonitor
dotnet new sln -n PowerMonitor
dotnet new classlib -n PowerMonitor.Core -f net8.0
dotnet new wpf -n PowerMonitor.App -f net8.0
dotnet new xunit -n PowerMonitor.Tests -f net8.0
dotnet sln add PowerMonitor.Core/PowerMonitor.Core.csproj
dotnet sln add PowerMonitor.App/PowerMonitor.App.csproj
dotnet sln add PowerMonitor.Tests/PowerMonitor.Tests.csproj
```

- [ ] **Step 2: Add NuGet packages to Core project**

```bash
cd PowerMonitor.Core
dotnet add package LibreHardwareMonitorLib --version 0.9.4
dotnet add package Microsoft.Data.Sqlite --version 8.0.0
cd ..
```

- [ ] **Step 3: Add NuGet packages to App project**

```bash
cd PowerMonitor.App
dotnet add package Microsoft.Extensions.DependencyInjection --version 8.0.0
dotnet add package OxyPlot.Wpf --version 2.2.0
dotnet add reference ../PowerMonitor.Core/PowerMonitor.Core.csproj
cd ..
```

- [ ] **Step 4: Add NuGet packages to Tests project**

```bash
cd PowerMonitor.Tests
dotnet add reference ../PowerMonitor.Core/PowerMonitor.Core.csproj
cd ..
```

- [ ] **Step 5: Verify solution builds**

```bash
dotnet build
```
Expected: Build succeeded with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add PowerMonitor.sln PowerMonitor.Core/ PowerMonitor.App/ PowerMonitor.Tests/
git commit -m "feat: scaffold solution with Core, App, and Tests projects"
```

---

### Task 2: Core Models

**Files:**
- Create: `PowerMonitor.Core/Models/ComponentPower.cs`
- Create: `PowerMonitor.Core/Models/PowerSample.cs`

- [ ] **Step 1: Create ComponentPower model**

Create `PowerMonitor.Core/Models/ComponentPower.cs`:

```csharp
namespace PowerMonitor.Core.Models;

public enum PowerSource
{
    Sensor,
    Estimated
}

public class ComponentPower
{
    public string Name { get; set; } = string.Empty;
    public double Watts { get; set; }
    public PowerSource Source { get; set; }
}
```

- [ ] **Step 2: Create PowerSample model**

Create `PowerMonitor.Core/Models/PowerSample.cs`:

```csharp
namespace PowerMonitor.Core.Models;

public class PowerSample
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public double TotalWatts { get; set; }
    public string SensorJson { get; set; } = "[]";
    public double PricePerKwh { get; set; }
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build PowerMonitor.Core/
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add PowerMonitor.Core/Models/
git commit -m "feat: add ComponentPower and PowerSample models"
```

---

### Task 3: Database Layer

**Files:**
- Create: `PowerMonitor.Core/Data/PowerDbContext.cs`

- [ ] **Step 1: Create PowerDbContext**

Create `PowerMonitor.Core/Data/PowerDbContext.cs`:

```csharp
using Microsoft.Data.Sqlite;
using System.Text.Json;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Data;

public class PowerDbContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;

    public PowerDbContext(string dbPath)
    {
        _dbPath = dbPath;
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS power_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                total_watts REAL NOT NULL,
                sensor_json TEXT NOT NULL DEFAULT '[]',
                price_per_kwh REAL NOT NULL DEFAULT 0.6
            );
            CREATE TABLE IF NOT EXISTS config (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_samples_timestamp ON power_samples(timestamp);
        ";
        cmd.ExecuteNonQuery();
    }

    public void InsertSample(PowerSample sample)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO power_samples (timestamp, total_watts, sensor_json, price_per_kwh)
            VALUES (@ts, @tw, @sj, @ppk)";
        cmd.Parameters.AddWithValue("@ts", sample.Timestamp.ToString("o"));
        cmd.Parameters.AddWithValue("@tw", sample.TotalWatts);
        cmd.Parameters.AddWithValue("@sj", sample.SensorJson);
        cmd.Parameters.AddWithValue("@ppk", sample.PricePerKwh);
        cmd.ExecuteNonQuery();
    }

    public List<PowerSample> GetSamples(DateTime from, DateTime to)
    {
        var samples = new List<PowerSample>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, timestamp, total_watts, sensor_json, price_per_kwh
            FROM power_samples
            WHERE timestamp >= @from AND timestamp <= @to
            ORDER BY timestamp ASC";
        cmd.Parameters.AddWithValue("@from", from.ToString("o"));
        cmd.Parameters.AddWithValue("@to", to.ToString("o"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            samples.Add(new PowerSample
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTime.Parse(reader.GetString(1)),
                TotalWatts = reader.GetDouble(2),
                SensorJson = reader.GetString(3),
                PricePerKwh = reader.GetDouble(4)
            });
        }
        return samples;
    }

    public PowerSample? GetLatestSample()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, timestamp, total_watts, sensor_json, price_per_kwh
            FROM power_samples
            ORDER BY id DESC LIMIT 1";

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new PowerSample
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTime.Parse(reader.GetString(1)),
                TotalWatts = reader.GetDouble(2),
                SensorJson = reader.GetString(3),
                PricePerKwh = reader.GetDouble(4)
            };
        }
        return null;
    }

    public void DeleteSamplesOlderThan(DateTime cutoff)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM power_samples WHERE timestamp < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public string GetConfig(string key, string defaultValue = "")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM config WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? defaultValue;
    }

    public void SetConfig(string key, string value)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO config (key, value, updated_at)
            VALUES (@key, @value, @now)";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build PowerMonitor.Core/
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add PowerMonitor.Core/Data/
git commit -m "feat: add PowerDbContext with SQLite CRUD operations"
```

---

### Task 4: Power Estimation Engine (Tests First)

**Files:**
- Create: `PowerMonitor.Core/Services/IPowerEstimationEngine.cs`
- Create: `PowerMonitor.Core/Services/PowerEstimationEngine.cs`
- Create: `PowerMonitor.Tests/PowerEstimationEngineTests.cs`

- [ ] **Step 1: Create interface**

Create `PowerMonitor.Core/Services/IPowerEstimationEngine.cs`:

```csharp
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public class HardwareInfo
{
    public int DimmCount { get; set; }
    public int HddCount { get; set; }
    public int SsdCount { get; set; }
    public int FanCount { get; set; }
}

public interface IPowerEstimationEngine
{
    List<ComponentPower> EstimateComponents(HardwareInfo info, double sensorTotal);
}
```

- [ ] **Step 2: Create the engine**

Create `PowerMonitor.Core/Services/PowerEstimationEngine.cs`:

```csharp
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public class PowerEstimationEngine : IPowerEstimationEngine
{
    public List<ComponentPower> EstimateComponents(HardwareInfo info, double sensorTotal)
    {
        var components = new List<ComponentPower>();

        var memoryWatts = info.DimmCount * 4.0;
        components.Add(new ComponentPower { Name = "内存", Watts = memoryWatts, Source = PowerSource.Estimated });

        var motherboardWatts = 20.0;
        components.Add(new ComponentPower { Name = "主板", Watts = motherboardWatts, Source = PowerSource.Estimated });

        var diskWatts = info.HddCount * 6.0 + info.SsdCount * 3.0;
        if (diskWatts > 0)
            components.Add(new ComponentPower { Name = "硬盘", Watts = diskWatts, Source = PowerSource.Estimated });

        var nicWatts = 5.0;
        components.Add(new ComponentPower { Name = "网卡", Watts = nicWatts, Source = PowerSource.Estimated });

        if (info.FanCount > 0)
        {
            var fanWatts = info.FanCount * 2.0;
            components.Add(new ComponentPower { Name = "风扇", Watts = fanWatts, Source = PowerSource.Estimated });
        }

        var otherTotal = components.Sum(c => c.Watts);
        var psuLoss = (sensorTotal + otherTotal) * 0.10;
        components.Add(new ComponentPower { Name = "电源损耗", Watts = Math.Round(psuLoss, 1), Source = PowerSource.Estimated });

        return components;
    }
}
```

- [ ] **Step 3: Write failing tests**

Create `PowerMonitor.Tests/PowerEstimationEngineTests.cs`:

```csharp
using PowerMonitor.Core.Services;

namespace PowerMonitor.Tests;

public class PowerEstimationEngineTests
{
    [Fact]
    public void EstimateComponents_ReturnsAllEstimatedSources()
    {
        var engine = new PowerEstimationEngine();
        var info = new HardwareInfo { DimmCount = 2, HddCount = 1, SsdCount = 1, FanCount = 3 };

        var result = engine.EstimateComponents(info, sensorTotal: 200);

        Assert.All(result, c => Assert.Equal(PowerMonitor.Core.Models.PowerSource.Estimated, c.Source));
    }

    [Fact]
    public void EstimateComponents_MemoryIsDimmCountTimes4Watts()
    {
        var engine = new PowerEstimationEngine();
        var info = new HardwareInfo { DimmCount = 4, HddCount = 0, SsdCount = 0, FanCount = 0 };

        var result = engine.EstimateComponents(info, sensorTotal: 100);

        var mem = result.First(c => c.Name == "内存");
        Assert.Equal(16.0, mem.Watts);
    }

    [Fact]
    public void EstimateComponents_DisksAreCalculatedCorrectly()
    {
        var engine = new PowerEstimationEngine();
        var info = new HardwareInfo { DimmCount = 0, HddCount = 2, SsdCount = 2, FanCount = 0 };

        var result = engine.EstimateComponents(info, sensorTotal: 100);

        var disk = result.First(c => c.Name == "硬盘");
        Assert.Equal(18.0, disk.Watts); // 2*6 + 2*3 = 18
    }

    [Fact]
    public void EstimateComponents_PsuLossIsTenPercent()
    {
        var engine = new PowerEstimationEngine();
        var info = new HardwareInfo { DimmCount = 0, HddCount = 0, SsdCount = 0, FanCount = 0 };

        var result = engine.EstimateComponents(info, sensorTotal: 200);

        // Estimated: 主板 20 + 网卡 5 = 25
        // PSU loss: (200 + 25) * 0.10 = 22.5
        var psu = result.First(c => c.Name == "电源损耗");
        Assert.Equal(22.5, psu.Watts);
    }

    [Fact]
    public void EstimateComponents_ReturnsMotherboardAndNicEvenWithZeroHardware()
    {
        var engine = new PowerEstimationEngine();
        var info = new HardwareInfo { DimmCount = 0, HddCount = 0, SsdCount = 0, FanCount = 0 };

        var result = engine.EstimateComponents(info, sensorTotal: 0);

        Assert.Contains(result, c => c.Name == "主板");
        Assert.Contains(result, c => c.Name == "网卡");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test PowerMonitor.Tests/
```
Expected: All 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add PowerMonitor.Core/Services/IPowerEstimationEngine.cs PowerMonitor.Core/Services/PowerEstimationEngine.cs PowerMonitor.Tests/PowerEstimationEngineTests.cs
git commit -m "feat: add power estimation engine with tests"
```

---

### Task 5: Billing Calculator (Tests First)

**Files:**
- Create: `PowerMonitor.Core/Services/IBillingCalculator.cs`
- Create: `PowerMonitor.Core/Services/BillingCalculator.cs`
- Create: `PowerMonitor.Tests/BillingCalculatorTests.cs`

- [ ] **Step 1: Create interface**

Create `PowerMonitor.Core/Services/IBillingCalculator.cs`:

```csharp
namespace PowerMonitor.Core.Services;

public class BillingPeriod
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double TotalKwh { get; set; }
    public double Cost { get; set; }
}

public interface IBillingCalculator
{
    double WattsToKwh(double watts, int intervalSeconds);
    double KwhToCost(double kwh, double pricePerKwh);
    BillingPeriod CalculateDayBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh);
    BillingPeriod CalculateWeekBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh);
    BillingPeriod CalculateMonthBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh);
}
```

- [ ] **Step 2: Create calculator**

Create `PowerMonitor.Core/Services/BillingCalculator.cs`:

```csharp
namespace PowerMonitor.Core.Services;

public class BillingCalculator : IBillingCalculator
{
    public double WattsToKwh(double watts, int intervalSeconds)
    {
        return watts * intervalSeconds / 3_600_000.0;
    }

    public double KwhToCost(double kwh, double pricePerKwh)
    {
        return kwh * pricePerKwh;
    }

    public BillingPeriod CalculateDayBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh)
    {
        var today = DateTime.Today;
        return CalculateBilling(samples, pricePerKwh, today, today.AddDays(1));
    }

    public BillingPeriod CalculateWeekBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh)
    {
        var today = DateTime.Today;
        int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var monday = today.AddDays(-diff);
        return CalculateBilling(samples, pricePerKwh, monday, today.AddDays(1));
    }

    public BillingPeriod CalculateMonthBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh)
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);
        return CalculateBilling(samples, pricePerKwh, firstOfMonth, today.AddDays(1));
    }

    private BillingPeriod CalculateBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh, DateTime start, DateTime end)
    {
        var periodSamples = samples.Where(s => s.Timestamp >= start && s.Timestamp < end).ToList();
        double totalKwh = 0;

        for (int i = 0; i < periodSamples.Count; i++)
        {
            int intervalSeconds;
            if (i < periodSamples.Count - 1)
            {
                intervalSeconds = (int)(periodSamples[i + 1].Timestamp - periodSamples[i].Timestamp).TotalSeconds;
            }
            else
            {
                intervalSeconds = 5; // default interval for last sample
            }
            if (intervalSeconds <= 0) intervalSeconds = 5;
            if (intervalSeconds > 300) intervalSeconds = 5; // cap to avoid spikes after pause

            totalKwh += WattsToKwh(periodSamples[i].TotalWatts, intervalSeconds);
        }

        return new BillingPeriod
        {
            Start = start,
            End = end,
            TotalKwh = Math.Round(totalKwh, 4),
            Cost = Math.Round(totalKwh * pricePerKwh, 2)
        };
    }
}
```

- [ ] **Step 3: Write failing tests**

Create `PowerMonitor.Tests/BillingCalculatorTests.cs`:

```csharp
using PowerMonitor.Core.Services;

namespace PowerMonitor.Tests;

public class BillingCalculatorTests
{
    [Fact]
    public void WattsToKwh_ConvertsCorrectly()
    {
        var calc = new BillingCalculator();
        // 200W for 5 seconds
        var kwh = calc.WattsToKwh(200, 5);
        Assert.Equal(200.0 * 5 / 3_600_000.0, kwh);
    }

    [Fact]
    public void WattsToKwh_ZeroForZeroWatts()
    {
        var calc = new BillingCalculator();
        var kwh = calc.WattsToKwh(0, 10);
        Assert.Equal(0, kwh);
    }

    [Fact]
    public void KwhToCost_CalculatesCorrectly()
    {
        var calc = new BillingCalculator();
        var cost = calc.KwhToCost(10, 0.6);
        Assert.Equal(6.0, cost);
    }

    [Fact]
    public void CalculateDayBilling_OnlyIncludesTodaySamples()
    {
        var calc = new BillingCalculator();
        var now = DateTime.Now;
        var today = DateTime.Today;
        var samples = new List<(DateTime, double)>
        {
            (today.AddHours(10), 200),
            (today.AddHours(10).AddSeconds(5), 200),
            (today.AddDays(-1).AddHours(10), 100) // yesterday - excluded
        };

        var result = calc.CalculateDayBilling(samples, 0.6);
        Assert.True(result.TotalKwh > 0);
        Assert.Equal(today, result.Start.Date);
    }

    [Fact]
    public void CalculateWeekBilling_UsesMondayAsStart()
    {
        var calc = new BillingCalculator();
        var samples = new List<(DateTime, double)>();
        var result = calc.CalculateWeekBilling(samples, 0.6);
        Assert.Equal(DayOfWeek.Monday, result.Start.DayOfWeek);
    }

    [Fact]
    public void CalculateMonthBilling_UsesFirstOfMonthAsStart()
    {
        var calc = new BillingCalculator();
        var samples = new List<(DateTime, double)>();
        var result = calc.CalculateMonthBilling(samples, 0.6);
        Assert.Equal(1, result.Start.Day);
    }
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test PowerMonitor.Tests/
```
Expected: All 11 tests pass (6 new + 5 from Task 4).

- [ ] **Step 5: Commit**

```bash
git add PowerMonitor.Core/Services/IBillingCalculator.cs PowerMonitor.Core/Services/BillingCalculator.cs PowerMonitor.Tests/BillingCalculatorTests.cs
git commit -m "feat: add billing calculator with tests"
```

---

### Task 6: Sensor Service (LibreHardwareMonitor Wrapper)

**Files:**
- Create: `PowerMonitor.Core/Services/ISensorService.cs`
- Create: `PowerMonitor.Core/Services/SensorService.cs`
- Create: `PowerMonitor.Core/Logging/Logger.cs`

- [ ] **Step 1: Create simple logger**

Create `PowerMonitor.Core/Logging/Logger.cs`:

```csharp
namespace PowerMonitor.Core.Logging;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PowerMonitor", "app.log");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"{DateTime.UtcNow:o} [{level}] {message}{Environment.NewLine}");
        }
        catch { /* silently drop log if we can't write */ }
    }

    public static string LogPathValue => LogPath;
}
```

- [ ] **Step 2: Create ISensorService interface**

Create `PowerMonitor.Core/Services/ISensorService.cs`:

```csharp
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public interface ISensorService
{
    bool Initialize();
    List<ComponentPower> ReadSensors();
    HardwareInfo GetHardwareInfo();
}
```

- [ ] **Step 3: Create SensorService**

Create `PowerMonitor.Core/Services/SensorService.cs`:

```csharp
using LibreHardwareMonitor.Hardware;
using PowerMonitor.Core.Logging;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public class SensorService : ISensorService
{
    private Computer? _computer;
    private int _consecutiveFailures = 0;

    public bool Initialize()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = true
            };
            _computer.Open();
            _computer.Accept(new UpdateVisitor());
            Logger.Info("LibreHardwareMonitor initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize LibreHardwareMonitor: {ex.Message}");
            return false;
        }
    }

    public List<ComponentPower> ReadSensors()
    {
        var components = new List<ComponentPower>();
        if (_computer == null) return components;

        try
        {
            _computer.Accept(new UpdateVisitor());

            foreach (var hardware in _computer.Hardware)
            {
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Power && sensor.Value.HasValue)
                    {
                        components.Add(new ComponentPower
                        {
                            Name = $"{hardware.Name} {sensor.Name}",
                            Watts = Math.Round(sensor.Value.Value, 1),
                            Source = PowerSource.Sensor
                        });
                    }
                }
            }
            _consecutiveFailures = 0;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            Logger.Warn($"Sensor read failed (attempt {_consecutiveFailures}): {ex.Message}");
        }

        return components;
    }

    public HardwareInfo GetHardwareInfo()
    {
        var info = new HardwareInfo();
        if (_computer == null) return info;

        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                switch (hardware.HardwareType)
                {
                    case HardwareType.Memory:
                        info.DimmCount = Math.Max(info.DimmCount, 1);
                        break;
                    case HardwareType.Storage:
                        var driveType = GetDriveType(hardware);
                        if (driveType == "HDD") info.HddCount++;
                        else if (driveType == "SSD") info.SsdCount++;
                        break;
                }
            }
            // Fallback: assume at least 2 DIMMs if no memory hardware detected
            if (info.DimmCount == 0) info.DimmCount = 2;
            // Count fans from motherboard
            info.FanCount = CountFans();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Hardware info detection failed: {ex.Message}");
            // Safe defaults
            info.DimmCount = info.DimmCount == 0 ? 2 : info.DimmCount;
            info.FanCount = info.FanCount == 0 ? 3 : info.FanCount;
        }

        return info;
    }

    private string GetDriveType(IHardware storage)
    {
        // Heuristic: HDDs report rotation rate, SSDs don't
        foreach (var sensor in storage.Sensors)
        {
            if (sensor.Name.Contains("Rotation") || sensor.Name.Contains("RPM"))
                return "HDD";
        }
        return "SSD";
    }

    private int CountFans()
    {
        if (_computer == null) return 0;
        int count = 0;
        foreach (var hardware in _computer.Hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Fan && sensor.Value.HasValue)
                    count++;
            }
        }
        return count;
    }

    public int ConsecutiveFailures => _consecutiveFailures;
}

// Required by LibreHardwareMonitor to traverse the hardware tree
public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer)
    {
        computer.Traverse(this);
    }

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (var subHardware in hardware.SubHardware)
            subHardware.Accept(this);
    }

    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}
```

- [ ] **Step 4: Verify build**

```bash
dotnet build PowerMonitor.Core/
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add PowerMonitor.Core/Services/ISensorService.cs PowerMonitor.Core/Services/SensorService.cs PowerMonitor.Core/Logging/Logger.cs
git commit -m "feat: add SensorService wrapping LibreHardwareMonitor"
```

---

### Task 7: PowerMonitorService (Top-Level Orchestration)

**Files:**
- Create: `PowerMonitor.Core/Services/IPowerMonitorService.cs`
- Create: `PowerMonitor.Core/Services/PowerMonitorService.cs`
- Create: `PowerMonitor.Core/Services/ISamplingScheduler.cs`
- Create: `PowerMonitor.Core/Services/SamplingScheduler.cs`

- [ ] **Step 1: Create ISamplingScheduler**

Create `PowerMonitor.Core/Services/ISamplingScheduler.cs`:

```csharp
namespace PowerMonitor.Core.Services;

public enum SamplingState
{
    Stopped,
    Running,
    Paused
}

public interface ISamplingScheduler
{
    SamplingState State { get; }
    int IntervalSeconds { get; set; }
    event EventHandler? SampleTick;
    void Start();
    void Pause();
    void Resume();
    void Stop();
}
```

- [ ] **Step 2: Create SamplingScheduler**

Create `PowerMonitor.Core/Services/SamplingScheduler.cs`:

```csharp
using PowerMonitor.Core.Logging;
using System.Timers;

namespace PowerMonitor.Core.Services;

public class SamplingScheduler : ISamplingScheduler, IDisposable
{
    private System.Timers.Timer? _timer;
    private int _intervalSeconds = 5;

    public SamplingState State { get; private set; } = SamplingState.Stopped;
    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set
        {
            _intervalSeconds = Math.Clamp(value, 1, 60);
            if (_timer != null)
                _timer.Interval = _intervalSeconds * 1000;
        }
    }

    public event EventHandler? SampleTick;

    public void Start()
    {
        if (State == SamplingState.Running) return;
        _timer = new System.Timers.Timer(_intervalSeconds * 1000);
        _timer.Elapsed += OnTick;
        _timer.AutoReset = true;
        _timer.Start();
        State = SamplingState.Running;
        Logger.Info("Sampling started");
    }

    public void Pause()
    {
        if (State != SamplingState.Running) return;
        _timer?.Stop();
        State = SamplingState.Paused;
        Logger.Info("Sampling paused");
    }

    public void Resume()
    {
        if (State != SamplingState.Paused) return;
        _timer?.Start();
        State = SamplingState.Running;
        Logger.Info("Sampling resumed");
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        State = SamplingState.Stopped;
        Logger.Info("Sampling stopped");
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        SampleTick?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
```

- [ ] **Step 3: Create IPowerMonitorService**

Create `PowerMonitor.Core/Services/IPowerMonitorService.cs`:

```csharp
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
}
```

- [ ] **Step 4: Create PowerMonitorService**

Create `PowerMonitor.Core/Services/PowerMonitorService.cs`:

```csharp
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

    public event EventHandler<PowerSnapshot>? SnapshotUpdated;

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

    private void OnSampleTick(object? sender, EventArgs e)
    {
        try
        {
            var sensorComponents = _sensor.ReadSensors();
            if (sensorComponents.Count == 0)
            {
                Logger.Warn("No sensor data read this tick");
                return;
            }

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
            TotalWatts = samples.Count > 0 ? samples.Last().TotalWatts : 0,
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
```

- [ ] **Step 5: Verify build**

```bash
dotnet build
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add PowerMonitor.Core/Services/
git commit -m "feat: add PowerMonitorService with sampling orchestration and DB persistence"
```

---

### Task 8: WPF App Shell & System Tray

**Files:**
- Create: `PowerMonitor.App/App.xaml` (replace default)
- Create: `PowerMonitor.App/App.xaml.cs` (replace default)
- Create: `PowerMonitor.App/Controls/TrayIconManager.cs`
- Create: `PowerMonitor.App/MainWindow.xaml` (replace default)
- Create: `PowerMonitor.App/MainWindow.xaml.cs` (replace default)

- [ ] **Step 1: Write App.xaml**

Replace `PowerMonitor.App/App.xaml`:

```xml
<Application x:Class="PowerMonitor.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
    </Application.Resources>
</Application>
```

- [ ] **Step 2: Write App.xaml.cs**

Replace `PowerMonitor.App/App.xaml.cs`:

```csharp
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PowerMonitor.Core.Services;
using PowerMonitor.App.Controls;

namespace PowerMonitor.App;

public partial class App : Application
{
    private readonly ServiceProvider _serviceProvider;
    private TrayIconManager? _trayManager;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerMonitor", "power.db");

        services.AddSingleton<ISensorService, SensorService>();
        services.AddSingleton<IPowerEstimationEngine, PowerEstimationEngine>();
        services.AddSingleton<IBillingCalculator, BillingCalculator>();
        services.AddSingleton<ISamplingScheduler, SamplingScheduler>();
        services.AddSingleton<IPowerMonitorService>(sp =>
            new PowerMonitorService(
                sp.GetRequiredService<ISensorService>(),
                sp.GetRequiredService<IPowerEstimationEngine>(),
                sp.GetRequiredService<IBillingCalculator>(),
                sp.GetRequiredService<ISamplingScheduler>(),
                dbPath));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var powerService = _serviceProvider.GetRequiredService<IPowerMonitorService>();
        _trayManager = new TrayIconManager(powerService, _serviceProvider);

        try
        {
            powerService.Start();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayManager?.Dispose();
        var powerService = _serviceProvider.GetRequiredService<IPowerMonitorService>();
        powerService.Stop();
        _serviceProvider.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 3: Create TrayIconManager**

Create `PowerMonitor.App/Controls/TrayIconManager.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PowerMonitor.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace PowerMonitor.App.Controls;

public class TrayIconManager : IDisposable
{
    private readonly IPowerMonitorService _powerService;
    private readonly IServiceProvider _serviceProvider;
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private MainWindow? _mainWindow;

    public TrayIconManager(IPowerMonitorService powerService, IServiceProvider serviceProvider)
    {
        _powerService = powerService;
        _serviceProvider = serviceProvider;

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "主机功耗监控 - 启动中...",
            Visible = true
        };

        // Generate a simple icon programmatically
        _notifyIcon.Icon = GenerateTrayIcon(0);

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("打开仪表盘", null, OnOpenDashboard);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("暂停采样", null, OnTogglePause);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("退出", null, OnExit);
        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += OnOpenDashboard;

        _powerService.SnapshotUpdated += OnSnapshotUpdated;
    }

    private void OnSnapshotUpdated(object? sender, PowerSnapshot snapshot)
    {
        _notifyIcon.Text = $"总功率: {snapshot.TotalWatts:F0}W\n" +
                          $"今日电费: ¥{snapshot.DayBilling.Cost:F2}\n" +
                          $"状态: {(snapshot.State == SamplingState.Running ? "运行中" : "已暂停")}";
    }

    private void OnOpenDashboard(object? sender, EventArgs? e)
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow(_powerService, _serviceProvider);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
            _mainWindow.Show();
        }
        else
        {
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }
    }

    private void OnTogglePause(object? sender, EventArgs? e)
    {
        if (_powerService.GetLatestSnapshot().State == SamplingState.Running)
        {
            _powerService.Pause();
            _notifyIcon.ContextMenuStrip!.Items[2].Text = "恢复采样";
        }
        else
        {
            _powerService.Resume();
            _notifyIcon.ContextMenuStrip!.Items[2].Text = "暂停采样";
        }
    }

    private void OnExit(object? sender, EventArgs? e)
    {
        _notifyIcon.Visible = false;
        Application.Current.Shutdown();
    }

    private System.Drawing.Icon GenerateTrayIcon(int watts)
    {
        // Create a 16x16 icon with power text
        var bitmap = new System.Drawing.Bitmap(16, 16);
        using var g = System.Drawing.Graphics.FromImage(bitmap);
        g.Clear(System.Drawing.Color.Transparent);
        using var font = new System.Drawing.Font("Consolas", 7);
        var text = watts > 0 ? $"{watts}" : "⚡";
        g.DrawString(text, font, System.Drawing.Brushes.LimeGreen, 0, 2);
        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _powerService.SnapshotUpdated -= OnSnapshotUpdated;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
```

Note: the WPF project needs a reference to `System.Windows.Forms` and `System.Drawing` for NotifyIcon. Add to `.csproj`:

```bash
cd PowerMonitor.App
dotnet add package System.Windows.Forms --version 8.0.0
```

Also add `<UseWindowsForms>true</UseWindowsForms>` to the WPF `.csproj` PropertyGroup.

- [ ] **Step 4: Write MainWindow.xaml**

Replace `PowerMonitor.App/MainWindow.xaml`:

```xml
<Window x:Class="PowerMonitor.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="⚡ 主机功耗监控" Height="600" Width="900"
        MinHeight="400" MinWidth="600"
        WindowStartupLocation="CenterScreen"
        StateChanged="Window_StateChanged">
    <TabControl x:Name="MainTabControl">
        <TabItem Header="仪表盘">
            <Frame x:Name="DashboardFrame" NavigationUIVisibility="Hidden"/>
        </TabItem>
        <TabItem Header="历史记录">
            <Frame x:Name="HistoryFrame" NavigationUIVisibility="Hidden"/>
        </TabItem>
        <TabItem Header="设置">
            <Frame x:Name="SettingsFrame" NavigationUIVisibility="Hidden"/>
        </TabItem>
    </TabControl>
</Window>
```

- [ ] **Step 5: Write MainWindow.xaml.cs**

Replace `PowerMonitor.App/MainWindow.xaml.cs`:

```csharp
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PowerMonitor.Core.Services;
using PowerMonitor.App.Views;

namespace PowerMonitor.App;

public partial class MainWindow : Window
{
    private readonly IPowerMonitorService _powerService;

    public MainWindow(IPowerMonitorService powerService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _powerService = powerService;

        DashboardFrame.Content = new DashboardView(powerService);
        HistoryFrame.Content = new HistoryView(powerService);
        SettingsFrame.Content = new SettingsView(powerService);
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }
}
```

- [ ] **Step 6: Update App.csproj for Windows Forms support**

Edit `PowerMonitor.App/PowerMonitor.App.csproj` to add `<UseWindowsForms>true</UseWindowsForms>` inside the `<PropertyGroup>`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  ...
</Project>
```

- [ ] **Step 7: Verify build**

```bash
dotnet build
```
Expected: Build succeeded with 0 errors.

- [ ] **Step 8: Commit**

```bash
git add PowerMonitor.App/
git commit -m "feat: add WPF app shell with system tray icon"
```

---

### Task 9: Dashboard View

**Files:**
- Create: `PowerMonitor.App/ViewModels/MainViewModel.cs`
- Create: `PowerMonitor.App/Views/DashboardView.xaml`
- Create: `PowerMonitor.App/Views/DashboardView.xaml.cs`

- [ ] **Step 1: Create MainViewModel**

Create `PowerMonitor.App/ViewModels/MainViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PowerMonitor.Core.Models;
using PowerMonitor.Core.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace PowerMonitor.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IPowerMonitorService _powerService;
    private readonly List<DataPoint> _powerHistory = new();
    private const int MaxPoints = 720; // 1 hour at 5-second intervals

    public MainViewModel(IPowerMonitorService powerService)
    {
        _powerService = powerService;
        PlotModel = new PlotModel { Title = "实时功率 (W)" };
        PlotModel.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss",
            IntervalType = DateTimeIntervalType.Seconds
        });
        PlotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "W",
            Minimum = 0
        });
        var series = new LineSeries
        {
            Color = OxyColor.FromRgb(0, 200, 100),
            StrokeThickness = 2,
            LineStyle = LineStyle.Solid
        };
        PlotModel.Series.Add(series);

        _powerService.SnapshotUpdated += OnSnapshotUpdated;
    }

    private double _totalWatts;
    public double TotalWatts
    {
        get => _totalWatts;
        set { _totalWatts = value; OnPropertyChanged(); }
    }

    private string _stateText = "已停止";
    public string StateText
    {
        get => _stateText;
        set { _stateText = value; OnPropertyChanged(); }
    }

    private string _dayCost = "¥0.00";
    public string DayCost
    {
        get => _dayCost;
        set { _dayCost = value; OnPropertyChanged(); }
    }

    private string _weekCost = "¥0.00";
    public string WeekCost
    {
        get => _weekCost;
        set { _weekCost = value; OnPropertyChanged(); }
    }

    private string _monthCost = "¥0.00";
    public string MonthCost
    {
        get => _monthCost;
        set { _monthCost = value; OnPropertyChanged(); }
    }

    private string _priceInfo = "¥0.60/kWh";
    public string PriceInfo
    {
        get => _priceInfo;
        set { _priceInfo = value; OnPropertyChanged(); }
    }

    private string _intervalInfo = "采样: 5秒";
    public string IntervalInfo
    {
        get => _intervalInfo;
        set { _intervalInfo = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ComponentPower> Components { get; } = new();
    public PlotModel PlotModel { get; }

    public void TogglePause()
    {
        var snapshot = _powerService.GetLatestSnapshot();
        if (snapshot.State == SamplingState.Running)
            _powerService.Pause();
        else if (snapshot.State == SamplingState.Paused)
            _powerService.Resume();
    }

    public void ExportCsv()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"power_export_{DateTime.Now:yyyyMMdd}.csv"
        };
        if (dialog.ShowDialog() == true)
        {
            _powerService.ExportCsv(DateTime.Today.AddDays(-7), DateTime.Now, dialog.FileName);
            System.Windows.MessageBox.Show("导出成功", "导出", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    private void OnSnapshotUpdated(object? sender, PowerSnapshot snapshot)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            TotalWatts = snapshot.TotalWatts;
            StateText = snapshot.State == SamplingState.Running ? "运行中 🟢" : "已暂停 🟡";
            DayCost = $"¥{snapshot.DayBilling.Cost:F2}";
            WeekCost = $"¥{snapshot.WeekBilling.Cost:F2}";
            MonthCost = $"¥{snapshot.MonthBilling.Cost:F2}";
            PriceInfo = $"¥{_powerService.GetPricePerKwh():F2}/kWh";
            IntervalInfo = $"采样: {_powerService.GetInterval()}秒";

            Components.Clear();
            foreach (var c in snapshot.Components)
                Components.Add(c);

            // Update chart
            _powerHistory.Add(new DataPoint(
                DateTimeAxis.ToDouble(snapshot.Timestamp), snapshot.TotalWatts));
            while (_powerHistory.Count > MaxPoints)
                _powerHistory.RemoveAt(0);

            var series = PlotModel.Series[0] as LineSeries;
            if (series != null)
            {
                series.ItemsSource = _powerHistory.ToArray();
                PlotModel.InvalidatePlot(true);
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
```

- [ ] **Step 2: Create DashboardView.xaml**

Create `PowerMonitor.App/Views/DashboardView.xaml`:

```xml
<UserControl x:Class="PowerMonitor.App.Views.DashboardView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:oxy="http://oxyplot.org/wpf">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Top: real-time total + chart -->
        <Grid Grid.Row="0" Margin="0,0,0,10">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="200"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Big power number -->
            <Border Grid.Column="0" Background="#1A1A2E" CornerRadius="8" Padding="20">
                <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center">
                    <TextBlock Text="当前功率" Foreground="#888" FontSize="14"/>
                    <TextBlock Text="{Binding TotalWatts, StringFormat='{}{0:F0}W'}"
                               Foreground="#00C864" FontSize="48" FontWeight="Bold"
                               HorizontalAlignment="Center"/>
                    <TextBlock Text="{Binding StateText}" Foreground="#AAA" FontSize="12"
                               HorizontalAlignment="Center" Margin="0,5,0,0"/>
                </StackPanel>
            </Border>

            <!-- Power curve -->
            <Border Grid.Column="1" Background="#1A1A2E" CornerRadius="8" Padding="10" Margin="10,0,0,0">
                <oxy:PlotView Model="{Binding PlotModel}"/>
            </Border>
        </Grid>

        <!-- Bottom: components + billing -->
        <Grid Grid.Row="1">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Component list -->
            <Border Grid.Column="0" Background="#1A1A2E" CornerRadius="8" Padding="15" Margin="0,0,5,0">
                <StackPanel>
                    <TextBlock Text="组件明细" Foreground="White" FontSize="16" FontWeight="Bold" Margin="0,0,0,10"/>
                    <ItemsControl ItemsSource="{Binding Components}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Grid Margin="0,2">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="80"/>
                                    </Grid.ColumnDefinitions>
                                    <StackPanel Orientation="Horizontal">
                                        <Ellipse Width="8" Height="8" Margin="0,0,5,0">
                                            <Ellipse.Style>
                                                <Style TargetType="Ellipse">
                                                    <Style.Triggers>
                                                        <DataTrigger Binding="{Binding Source}" Value="Sensor">
                                                            <Setter Property="Fill" Value="#00C864"/>
                                                        </DataTrigger>
                                                        <DataTrigger Binding="{Binding Source}" Value="Estimated">
                                                            <Setter Property="Fill" Value="#FFA500"/>
                                                        </DataTrigger>
                                                    </Style.Triggers>
                                                </Style>
                                            </Ellipse.Style>
                                        </Ellipse>
                                        <TextBlock Text="{Binding Name}" Foreground="#CCC" FontSize="13"/>
                                    </StackPanel>
                                    <TextBlock Grid.Column="1" Text="{Binding Watts, StringFormat='{}{0:F1}W'}"
                                               Foreground="#CCC" FontSize="13" HorizontalAlignment="Right"/>
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </StackPanel>
            </Border>

            <!-- Billing summary -->
            <Border Grid.Column="1" Background="#1A1A2E" CornerRadius="8" Padding="15" Margin="5,0,0,0">
                <StackPanel>
                    <TextBlock Text="电费汇总" Foreground="White" FontSize="16" FontWeight="Bold" Margin="0,0,0,15"/>
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition/>
                            <RowDefinition/>
                            <RowDefinition/>
                            <RowDefinition/>
                        </Grid.RowDefinitions>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition/>
                            <ColumnDefinition/>
                        </Grid.ColumnDefinitions>

                        <TextBlock Text="今日" Foreground="#888" FontSize="14"/>
                        <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding DayCost}"
                                   Foreground="#00C864" FontSize="28" FontWeight="Bold"
                                   HorizontalAlignment="Right"/>

                        <TextBlock Grid.Row="1" Text="本周" Foreground="#888" FontSize="14" Margin="0,10,0,0"/>
                        <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding WeekCost}"
                                   Foreground="White" FontSize="28" FontWeight="Bold"
                                   HorizontalAlignment="Right" Margin="0,10,0,0"/>

                        <TextBlock Grid.Row="2" Text="本月" Foreground="#888" FontSize="14" Margin="0,10,0,0"/>
                        <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding MonthCost}"
                                   Foreground="White" FontSize="28" FontWeight="Bold"
                                   HorizontalAlignment="Right" Margin="0,10,0,0"/>
                    </Grid>
                </StackPanel>
            </Border>
        </Grid>

        <!-- Bottom bar -->
        <Border Grid.Row="2" Background="#1A1A2E" CornerRadius="8" Padding="10" Margin="0,10,0,0">
            <Grid>
                <StackPanel Orientation="Horizontal">
                    <Button Content="开始/暂停" Click="TogglePause_Click" Width="80" Margin="0,0,10,0"
                            Background="#333" Foreground="White" BorderThickness="0"/>
                    <Button Content="导出CSV" Click="ExportCsv_Click" Width="80" Margin="0,0,10,0"
                            Background="#333" Foreground="White" BorderThickness="0"/>
                </StackPanel>
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                    <TextBlock Text="{Binding PriceInfo}" Foreground="#888" FontSize="12" VerticalAlignment="Center" Margin="0,0,15,0"/>
                    <TextBlock Text="{Binding IntervalInfo}" Foreground="#888" FontSize="12" VerticalAlignment="Center"/>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Create DashboardView.xaml.cs**

Create `PowerMonitor.App/Views/DashboardView.xaml.cs`:

```csharp
using System.Windows.Controls;
using PowerMonitor.App.ViewModels;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.Views;

public partial class DashboardView : UserControl
{
    private readonly MainViewModel _viewModel;

    public DashboardView(IPowerMonitorService powerService)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(powerService);
        DataContext = _viewModel;
    }

    private void TogglePause_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.TogglePause();
    }

    private void ExportCsv_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.ExportCsv();
    }
}
```

- [ ] **Step 4: Verify build**

```bash
dotnet build
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add PowerMonitor.App/ViewModels/ PowerMonitor.App/Views/DashboardView*
git commit -m "feat: add dashboard view with real-time chart and billing summary"
```

---

### Task 10: Settings View

**Files:**
- Create: `PowerMonitor.App/ViewModels/SettingsViewModel.cs`
- Create: `PowerMonitor.App/Views/SettingsView.xaml`
- Create: `PowerMonitor.App/Views/SettingsView.xaml.cs`

- [ ] **Step 1: Create SettingsViewModel**

Create `PowerMonitor.App/ViewModels/SettingsViewModel.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IPowerMonitorService _powerService;

    public SettingsViewModel(IPowerMonitorService powerService)
    {
        _powerService = powerService;
        _pricePerKwh = _powerService.GetPricePerKwh().ToString("F2");
        _interval = _powerService.GetInterval();
        _retentionDays = 90;

        SaveCommand = new RelayCommand(_ => Save());
    }

    private string _pricePerKwh;
    public string PricePerKwh
    {
        get => _pricePerKwh;
        set { _pricePerKwh = value; OnPropertyChanged(); }
    }

    private int _interval;
    public int Interval
    {
        get => _interval;
        set { _interval = value; OnPropertyChanged(); }
    }

    private int _retentionDays;
    public int RetentionDays
    {
        get => _retentionDays;
        set { _retentionDays = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public ICommand SaveCommand { get; }

    private void Save()
    {
        if (double.TryParse(_pricePerKwh, out var price) && price > 0)
        {
            _powerService.SetPricePerKwh(price);
        }
        _powerService.SetInterval(Math.Clamp(_interval, 1, 60));
        _powerService.CleanupOldData(_retentionDays);
        StatusMessage = "设置已保存 ✓";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action<object?> execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
```

- [ ] **Step 2: Create SettingsView.xaml**

Create `PowerMonitor.App/Views/SettingsView.xaml`:

```xml
<UserControl x:Class="PowerMonitor.App.Views.SettingsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Margin="10">
        <StackPanel MaxWidth="400">
            <TextBlock Text="设置" Foreground="White" FontSize="20" FontWeight="Bold" Margin="0,0,0,20"/>

            <TextBlock Text="电价 (¥/kWh)" Foreground="#CCC" FontSize="14" Margin="0,0,0,5"/>
            <TextBox Text="{Binding PricePerKwh}" Background="#333" Foreground="White"
                     FontSize="16" Padding="8" BorderThickness="1" BorderBrush="#555" Margin="0,0,0,15"/>

            <TextBlock Text="采样间隔 (秒, 1-60)" Foreground="#CCC" FontSize="14" Margin="0,0,0,5"/>
            <TextBox Text="{Binding Interval}" Background="#333" Foreground="White"
                     FontSize="16" Padding="8" BorderThickness="1" BorderBrush="#555" Margin="0,0,0,15"/>

            <TextBlock Text="数据保留天数" Foreground="#CCC" FontSize="14" Margin="0,0,0,5"/>
            <TextBox Text="{Binding RetentionDays}" Background="#333" Foreground="White"
                     FontSize="16" Padding="8" BorderThickness="1" BorderBrush="#555" Margin="0,0,0,20"/>

            <Button Content="保存设置" Command="{Binding SaveCommand}"
                    Background="#00C864" Foreground="White" FontSize="14"
                    Padding="15,8" BorderThickness="0" HorizontalAlignment="Left"/>

            <TextBlock Text="{Binding StatusMessage}" Foreground="#00C864" FontSize="14" Margin="0,10,0,0"/>
        </StackPanel>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Create SettingsView.xaml.cs**

Create `PowerMonitor.App/Views/SettingsView.xaml.cs`:

```csharp
using System.Windows.Controls;
using PowerMonitor.App.ViewModels;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(IPowerMonitorService powerService)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(powerService);
    }
}
```

- [ ] **Step 4: Verify build**

```bash
dotnet build
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add PowerMonitor.App/ViewModels/SettingsViewModel.cs PowerMonitor.App/Views/SettingsView*
git commit -m "feat: add settings view with price, interval, and retention configuration"
```

---

### Task 11: History View

**Files:**
- Create: `PowerMonitor.App/ViewModels/HistoryViewModel.cs`
- Create: `PowerMonitor.App/Views/HistoryView.xaml`
- Create: `PowerMonitor.App/Views/HistoryView.xaml.cs`

- [ ] **Step 1: Create HistoryViewModel**

Create `PowerMonitor.App/ViewModels/HistoryViewModel.cs`:

```csharp
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.ViewModels;

public class HistoryViewModel : INotifyPropertyChanged
{
    private readonly IPowerMonitorService _powerService;

    public HistoryViewModel(IPowerMonitorService powerService)
    {
        _powerService = powerService;
        LoadDay();
    }

    public ObservableCollection<HistoryRow> Rows { get; } = new();

    private string _title = "历史用电";
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public void LoadDay()
    {
        Title = "今日用电记录";
        var today = DateTime.Today;
        Load(today, today.AddDays(1));
    }

    public void LoadWeek()
    {
        Title = "本周用电记录";
        int diff = (7 + (int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var monday = DateTime.Today.AddDays(-diff);
        Load(monday, DateTime.Now);
    }

    public void LoadMonth()
    {
        Title = "本月用电记录";
        var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        Load(firstOfMonth, DateTime.Now);
    }

    private void Load(DateTime from, DateTime to)
    {
        Rows.Clear();
        var samples = _powerService.GetHistory(from, to);
        // Aggregate by hour for display
        var grouped = samples
            .GroupBy(s => new DateTime(s.Timestamp.Year, s.Timestamp.Month, s.Timestamp.Day, s.Timestamp.Hour, 0, 0))
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            Rows.Add(new HistoryRow
            {
                Time = group.Key.ToString("MM-dd HH:mm"),
                AvgWatts = $"{group.Average(s => s.TotalWatts):F1}",
                MinWatts = $"{group.Min(s => s.TotalWatts):F1}",
                MaxWatts = $"{group.Max(s => s.TotalWatts):F1}",
                Kwh = $"{group.Sum(s => s.TotalWatts) * 5 / 3_600_000.0:F4}",
                Cost = $"¥{group.Sum(s => s.TotalWatts) * 5 / 3_600_000.0 * s.PricePerKwh:F2}"
            });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class HistoryRow
{
    public string Time { get; set; } = "";
    public string AvgWatts { get; set; } = "";
    public string MinWatts { get; set; } = "";
    public string MaxWatts { get; set; } = "";
    public string Kwh { get; set; } = "";
    public string Cost { get; set; } = "";
}
```

- [ ] **Step 2: Create HistoryView.xaml**

Create `PowerMonitor.App/Views/HistoryView.xaml`:

```xml
<UserControl x:Class="PowerMonitor.App.Views.HistoryView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="{Binding Title}" Foreground="White" FontSize="20" FontWeight="Bold" Margin="0,0,0,10"/>

        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,10">
            <Button Content="今日" Click="Day_Click" Background="#333" Foreground="White"
                    BorderThickness="0" Padding="10,5" Margin="0,0,5,0"/>
            <Button Content="本周" Click="Week_Click" Background="#333" Foreground="White"
                    BorderThickness="0" Padding="10,5" Margin="0,0,5,0"/>
            <Button Content="本月" Click="Month_Click" Background="#333" Foreground="White"
                    BorderThickness="0" Padding="10,5"/>
        </StackPanel>

        <ListView Grid.Row="2" ItemsSource="{Binding Rows}" Background="#1A1A2E" Foreground="#CCC">
            <ListView.View>
                <GridView>
                    <GridViewColumn Header="时间" DisplayMemberBinding="{Binding Time}" Width="120"/>
                    <GridViewColumn Header="平均(W)" DisplayMemberBinding="{Binding AvgWatts}" Width="80"/>
                    <GridViewColumn Header="最低(W)" DisplayMemberBinding="{Binding MinWatts}" Width="80"/>
                    <GridViewColumn Header="最高(W)" DisplayMemberBinding="{Binding MaxWatts}" Width="80"/>
                    <GridViewColumn Header="耗电(kWh)" DisplayMemberBinding="{Binding Kwh}" Width="100"/>
                    <GridViewColumn Header="费用" DisplayMemberBinding="{Binding Cost}" Width="80"/>
                </GridView>
            </ListView.View>
        </ListView>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Create HistoryView.xaml.cs**

Create `PowerMonitor.App/Views/HistoryView.xaml.cs`:

```csharp
using System.Windows.Controls;
using PowerMonitor.App.ViewModels;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.Views;

public partial class HistoryView : UserControl
{
    private readonly HistoryViewModel _viewModel;

    public HistoryView(IPowerMonitorService powerService)
    {
        InitializeComponent();
        _viewModel = new HistoryViewModel(powerService);
        DataContext = _viewModel;
    }

    private void Day_Click(object sender, System.Windows.RoutedEventArgs e) => _viewModel.LoadDay();
    private void Week_Click(object sender, System.Windows.RoutedEventArgs e) => _viewModel.LoadWeek();
    private void Month_Click(object sender, System.Windows.RoutedEventArgs e) => _viewModel.LoadMonth();
}
```

- [ ] **Step 4: Verify full solution builds**

```bash
dotnet build
```
Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Run all tests**

```bash
dotnet test
```
Expected: All 11 tests pass.

- [ ] **Step 6: Commit**

```bash
git add PowerMonitor.App/ViewModels/HistoryViewModel.cs PowerMonitor.App/Views/HistoryView*
git commit -m "feat: add history view with day/week/month aggregation"
```

---

### Task 12: Final Integration & Polish

- [ ] **Step 1: Add auto data cleanup on startup**

Edit `PowerMonitor.Core/Services/PowerMonitorService.cs`, add to the `Start()` method after `_sensor.Initialize()` succeeds:

```csharp
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
```

- [ ] **Step 2: Add sensor failure event to PowerMonitorService**

Edit `PowerMonitor.Core/Services/IPowerMonitorService.cs`, add to the interface:

```csharp
event EventHandler<string>? SensorWarning;
```

Edit `PowerMonitor.Core/Services/PowerMonitorService.cs`, add the event and fire it in `OnSampleTick` when consecutive failures reach 3:

```csharp
public event EventHandler<string>? SensorWarning;
private int _consecutiveEmptyReads = 0;

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
        // ... rest of existing code
```

- [ ] **Step 3: Wire tray bubble notification in TrayIconManager**

Edit `PowerMonitor.App/Controls/TrayIconManager.cs`, subscribe to `SensorWarning` in constructor:

```csharp
public TrayIconManager(IPowerMonitorService powerService, IServiceProvider serviceProvider)
{
    // ... existing constructor code ...
    _powerService.SnapshotUpdated += OnSnapshotUpdated;
    _powerService.SensorWarning += OnSensorWarning;
}

private void OnSensorWarning(object? sender, string message)
{
    _notifyIcon.ShowBalloonTip(5000, "功耗监控警告", message, System.Windows.Forms.ToolTipIcon.Warning);
}
```

Also update `Dispose()` to unsubscribe:

```csharp
public void Dispose()
{
    _powerService.SnapshotUpdated -= OnSnapshotUpdated;
    _powerService.SensorWarning -= OnSensorWarning;
    _notifyIcon.Visible = false;
    _notifyIcon.Dispose();
}
```

- [ ] **Step 4: Verify full build and test**

```bash
dotnet build
dotnet test
```
Expected: Build 0 errors, 11 tests pass.

- [ ] **Step 5: Verify dark theme consistency across all views**

All XAML files use `#1A1A2E` background and `#CCC`/`White` foreground. Settings inputs use `#333` background.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: add auto-cleanup, sensor failure notifications, and final polish"
```
