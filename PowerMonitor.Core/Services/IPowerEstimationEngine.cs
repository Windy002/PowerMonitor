using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public class HardwareInfo
{
    // 现有字段 — 从 LHM 传感器检测
    public int DimmCount { get; set; }
    public int HddCount { get; set; }
    public int SsdCount { get; set; }
    public int FanCount { get; set; }

    // 新增 — 从 WMI 自动检测
    public string CpuModel { get; set; } = "";
    public int CpuCoreCount { get; set; }
    public double CpuMaxClockGhz { get; set; }
    public string MotherboardProduct { get; set; } = "";
    public string MemoryType { get; set; } = "";  // "DDR4" / "DDR5" / "Unknown"

    // 从 LHM Load 传感器读取
    public double CpuLoadPercent { get; set; }
}

public interface IPowerEstimationEngine
{
    List<ComponentPower> EstimateComponents(HardwareInfo info, double sensorTotal);
}
