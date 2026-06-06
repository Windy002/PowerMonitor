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
