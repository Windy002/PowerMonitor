using LibreHardwareMonitor.Hardware;
using PowerMonitor.Core.Logging;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public class SensorService : ISensorService
{
    private Computer? _computer;
    private int _consecutiveFailures = 0;

    public int ConsecutiveFailures => _consecutiveFailures;

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
            if (info.DimmCount == 0) info.DimmCount = 2;
            info.FanCount = CountFans();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Hardware info detection failed: {ex.Message}");
            info.DimmCount = info.DimmCount == 0 ? 2 : info.DimmCount;
            info.FanCount = info.FanCount == 0 ? 3 : info.FanCount;
        }

        return info;
    }

    private string GetDriveType(IHardware storage)
    {
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
}

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
