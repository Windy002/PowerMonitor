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
