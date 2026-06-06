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

    [Fact]
    public void EstimateComponents_CpuIsAddedWhenModelIsKnown()
    {
        var engine = new PowerEstimationEngine();
        var info = new HardwareInfo
        {
            CpuModel = "AMD Ryzen 7 5800X",
            CpuCoreCount = 8,
            CpuLoadPercent = 50,
            DimmCount = 2,
            FanCount = 0
        };

        var result = engine.EstimateComponents(info, sensorTotal: 100);

        var cpu = result.FirstOrDefault(c => c.Name == "CPU (估算)");
        Assert.NotNull(cpu);
        Assert.Equal(PowerMonitor.Core.Models.PowerSource.Estimated, cpu!.Source);
        // 5800X TDP=105W, idle≈15.75W, at 50% load → 15.75+89.25×0.5=60.375 → rounds to 60.4
        Assert.True(cpu.Watts > 15 && cpu.Watts < 90, $"Expected CPU watts between 15-90, got {cpu.Watts}");
    }

    [Fact]
    public void EstimateComponents_CpuIsSkippedWhenModelUnknown()
    {
        var engine = new PowerEstimationEngine();
        var info = new HardwareInfo { CpuModel = "", CpuCoreCount = 0, DimmCount = 2, FanCount = 0 };

        var result = engine.EstimateComponents(info, sensorTotal: 100);

        Assert.DoesNotContain(result, c => c.Name == "CPU (估算)");
    }

    [Fact]
    public void EstimateComponents_Ddr4VsDdr5Memory()
    {
        var engine = new PowerEstimationEngine();

        var ddr4Info = new HardwareInfo { DimmCount = 2, MemoryType = "DDR4", FanCount = 0 };
        var ddr4Result = engine.EstimateComponents(ddr4Info, sensorTotal: 100);
        var ddr4Mem = ddr4Result.First(c => c.Name == "内存");
        Assert.Equal(7.0, ddr4Mem.Watts); // 2 × 3.5W

        var ddr5Info = new HardwareInfo { DimmCount = 2, MemoryType = "DDR5", FanCount = 0 };
        var ddr5Result = engine.EstimateComponents(ddr5Info, sensorTotal: 100);
        var ddr5Mem = ddr5Result.First(c => c.Name == "内存");
        Assert.Equal(10.0, ddr5Mem.Watts); // 2 × 5W
    }

    [Fact]
    public void EstimateComponents_MotherboardChipsetDetection()
    {
        var engine = new PowerEstimationEngine();

        // 高端 Z790
        var z790Info = new HardwareInfo { MotherboardProduct = "ROG STRIX Z790-A", DimmCount = 2, FanCount = 0 };
        var z790Result = engine.EstimateComponents(z790Info, sensorTotal: 100);
        Assert.Equal(22.0, z790Result.First(c => c.Name == "主板").Watts);

        // 中端 B650
        var b650Info = new HardwareInfo { MotherboardProduct = "TUF GAMING B650M-PLUS", DimmCount = 2, FanCount = 0 };
        var b650Result = engine.EstimateComponents(b650Info, sensorTotal: 100);
        Assert.Equal(18.0, b650Result.First(c => c.Name == "主板").Watts);

        // ROG 品牌主板（芯片组未命中，品牌匹配高端）
        var rogInfo = new HardwareInfo { MotherboardProduct = "ROG MAXIMUS FORMULA", DimmCount = 2, FanCount = 0 };
        var rogResult = engine.EstimateComponents(rogInfo, sensorTotal: 100);
        Assert.Equal(22.0, rogResult.First(c => c.Name == "主板").Watts);
    }
}
