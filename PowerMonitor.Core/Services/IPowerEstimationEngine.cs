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
