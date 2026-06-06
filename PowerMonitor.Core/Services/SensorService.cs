using System.Management;
using LibreHardwareMonitor.Hardware;
using PowerMonitor.Core.Logging;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public class SensorService : ISensorService
{
    private Computer? _computer;
    private int _consecutiveFailures = 0;
    private HardwareInfo? _cachedHwInfo; // WMI 结果缓存，只查一次

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

        // --- WMI 查询（仅首次，后续用缓存）---
        if (_cachedHwInfo == null)
        {
            _cachedHwInfo = QueryWmi();
        }
        // 合并缓存的 WMI 字段
        info.CpuModel = _cachedHwInfo.CpuModel;
        info.CpuCoreCount = _cachedHwInfo.CpuCoreCount;
        info.CpuMaxClockGhz = _cachedHwInfo.CpuMaxClockGhz;
        info.MemoryType = _cachedHwInfo.MemoryType;
        info.DimmCount = _cachedHwInfo.DimmCount;
        info.MotherboardProduct = _cachedHwInfo.MotherboardProduct;

        // --- LHM 检测：硬盘类型 + 风扇数（每次刷新）---
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Storage)
                {
                    var driveType = GetDriveType(hardware);
                    if (driveType == "HDD") info.HddCount++;
                    else if (driveType == "SSD") info.SsdCount++;
                }
            }
            info.FanCount = CountFans();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Hardware info detection failed: {ex.Message}");
            info.FanCount = info.FanCount == 0 ? 3 : info.FanCount;
        }

        return info;
    }

    /// <summary>WMI 查询：CPU、内存、主板 — 只在初始化时调用一次</summary>
    private HardwareInfo QueryWmi()
    {
        var info = new HardwareInfo();
        try
        {
            // CPU
            using var cpuSearcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, MaxClockSpeed FROM Win32_Processor");
            foreach (ManagementObject cpu in cpuSearcher.Get())
            {
                info.CpuModel = cpu["Name"]?.ToString()?.Trim() ?? "";
                info.CpuCoreCount = Convert.ToInt32(cpu["NumberOfCores"] ?? 0);
                info.CpuMaxClockGhz = Convert.ToInt32(cpu["MaxClockSpeed"] ?? 0) / 1000.0;
                break;
            }

            // 内存类型 + 条数
            var memTypes = new HashSet<string>();
            using var memSearcher = new ManagementObjectSearcher("SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory");
            foreach (ManagementObject mem in memSearcher.Get())
            {
                var typeCode = Convert.ToInt32(mem["SMBIOSMemoryType"] ?? 0);
                memTypes.Add(SmbiosToMemoryType(typeCode));
            }
            info.MemoryType = memTypes.Count switch
            {
                1 => memTypes.First(),
                > 1 => memTypes.FirstOrDefault(t => t != "Unknown") ?? "Unknown",
                _ => "Unknown"
            };
            info.DimmCount = memTypes.Count > 0 ? memTypes.Count : 2;

            // 主板
            using var boardSearcher = new ManagementObjectSearcher("SELECT Product FROM Win32_BaseBoard");
            foreach (ManagementObject board in boardSearcher.Get())
            {
                info.MotherboardProduct = board["Product"]?.ToString()?.Trim() ?? "";
                break;
            }

            Logger.Info($"WMI detected: CPU={info.CpuModel}, Cores={info.CpuCoreCount}, " +
                        $"Memory={info.MemoryType} x{info.DimmCount}, MB={info.MotherboardProduct}");
        }
        catch (Exception ex)
        {
            Logger.Warn($"WMI query failed: {ex.Message}");
        }
        return info;
    }

    private static string SmbiosToMemoryType(int typeCode) => typeCode switch
    {
        34 => "DDR5",
        26 => "DDR4",
        24 => "DDR3",
        21 => "DDR2",
        20 => "DDR",
        _ => "Unknown"
    };

    private string GetDriveType(IHardware storage)
    {
        foreach (var sensor in storage.Sensors)
        {
            if (sensor.Name.Contains("Rotation") || sensor.Name.Contains("RPM"))
                return "HDD";
        }
        return "SSD";
    }

    public double ReadCpuLoad()
    {
        if (_computer == null) return 0;

        try
        {
            _computer.Accept(new UpdateVisitor());

            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu) continue;

                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                    {
                        // "CPU Total" 是整体负载，优先返回
                        if (sensor.Name.Contains("Total"))
                            return Math.Round(sensor.Value.Value, 1);
                    }
                }

                // 没找到 "CPU Total"，取所有 Load 传感器最大值
                double maxLoad = 0;
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                        maxLoad = Math.Max(maxLoad, sensor.Value.Value);
                }
                if (maxLoad > 0) return Math.Round(maxLoad, 1);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"CPU load read failed: {ex.Message}");
        }

        return 0;
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
