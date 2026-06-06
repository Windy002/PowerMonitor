using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Services;

public class PowerEstimationEngine : IPowerEstimationEngine
{
    // CPU TDP 查表 — 常见桌面 CPU，key 为型号关键词
    private static readonly Dictionary<string, int> CpuTdpTable = new(StringComparer.OrdinalIgnoreCase)
    {
        // AMD Ryzen 桌面
        { "Ryzen 9 7950X", 170 }, { "Ryzen 9 7900X", 170 },
        { "Ryzen 7 7800X3D", 120 }, { "Ryzen 7 7700X", 105 },
        { "Ryzen 5 7600X", 105 }, { "Ryzen 5 5600X", 65 },
        { "Ryzen 9 5950X", 105 }, { "Ryzen 9 5900X", 105 },
        { "Ryzen 7 5800X3D", 105 }, { "Ryzen 7 5800X", 105 }, { "Ryzen 7 5700X", 65 },
        { "Ryzen 5 5600", 65 }, { "Ryzen 5 5500", 65 },
        // Intel Core 桌面 (13/14 代)
        { "i9-14900K", 125 }, { "i7-14700K", 125 }, { "i5-14600K", 125 },
        { "i9-13900K", 125 }, { "i7-13700K", 125 }, { "i5-13600K", 125 },
        // Intel Core 桌面 (12 代)
        { "i9-12900K", 125 }, { "i7-12700K", 125 }, { "i5-12600K", 125 },
        { "i5-12400", 65 },
        // AMD APU
        { "Ryzen 7 8700G", 65 }, { "Ryzen 5 8600G", 65 },
        { "Ryzen 7 5700G", 65 }, { "Ryzen 5 5600G", 65 },
    };

    public List<ComponentPower> EstimateComponents(HardwareInfo info, double sensorTotal)
    {
        var components = new List<ComponentPower>();

        // --- CPU（新增）— 基于 TDP 和实时负载估算 ---
        var cpuTdp = LookupCpuTdp(info.CpuModel, info.CpuCoreCount);
        if (cpuTdp > 0)
        {
            // idle base 约为 TDP 的 15%（现代 CPU 空闲时功耗很低）
            var cpuIdleBase = Math.Max(8, cpuTdp * 0.15);
            var load = Math.Clamp(info.CpuLoadPercent, 0, 100) / 100.0;
            var cpuWatts = cpuIdleBase + (cpuTdp - cpuIdleBase) * load;
            components.Add(new ComponentPower
            {
                Name = "CPU (估算)",
                Watts = Math.Round(cpuWatts, 1),
                Source = PowerSource.Estimated
            });
        }

        // --- 内存 — 按 DDR 代际区分 ---
        var memPerDimm = info.MemoryType switch
        {
            "DDR5" => 5.0,
            "DDR4" => 3.5,
            _ => 4.0 // 未知类型沿用默认
        };
        var memoryWatts = info.DimmCount * memPerDimm;
        components.Add(new ComponentPower
        {
            Name = "内存",
            Watts = Math.Round(memoryWatts, 1),
            Source = PowerSource.Estimated
        });

        // --- 主板 — 按芯片组推断 ---
        var motherboardWatts = EstimateMotherboardWatts(info.MotherboardProduct);
        components.Add(new ComponentPower
        {
            Name = "主板",
            Watts = Math.Round(motherboardWatts, 1),
            Source = PowerSource.Estimated
        });

        // --- 硬盘 ---
        var diskWatts = info.HddCount * 6.0 + info.SsdCount * 3.0;
        if (diskWatts > 0)
            components.Add(new ComponentPower
            {
                Name = "硬盘",
                Watts = Math.Round(diskWatts, 1),
                Source = PowerSource.Estimated
            });

        // --- 网卡 ---
        const double nicWatts = 5.0;
        components.Add(new ComponentPower
        {
            Name = "网卡",
            Watts = nicWatts,
            Source = PowerSource.Estimated
        });

        // --- 风扇 ---
        if (info.FanCount > 0)
        {
            var fanWatts = info.FanCount * 2.0;
            components.Add(new ComponentPower
            {
                Name = "风扇",
                Watts = Math.Round(fanWatts, 1),
                Source = PowerSource.Estimated
            });
        }

        // --- 电源损耗 ---
        var otherTotal = components.Sum(c => c.Watts);
        var psuLoss = (sensorTotal + otherTotal) * 0.10;
        components.Add(new ComponentPower
        {
            Name = "电源损耗",
            Watts = Math.Round(psuLoss, 1),
            Source = PowerSource.Estimated
        });

        return components;
    }

    /// <summary>
    /// 从 CPU 型号字符串匹配 TDP。匹配策略：
    /// 1. 完整型号名精确匹配（忽略大小写）
    /// 2. 提取型号关键词（如 "5800X"）匹配
    /// 3. 都不匹配时按核心数估算： cores × 12W
    /// </summary>
    private static int LookupCpuTdp(string cpuModel, int coreCount)
    {
        if (string.IsNullOrWhiteSpace(cpuModel)) return 0;

        // 策略 1：完整匹配
        foreach (var kv in CpuTdpTable)
        {
            if (cpuModel.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        // 策略 2：如果名称包含 "Ryzen"，尝试提取核心代号匹配
        foreach (var kv in CpuTdpTable)
        {
            var term = kv.Key;
            // 取最后一部分（如 "5800X" from "Ryzen 7 5800X"）
            var parts = term.Split(' ');
            var codeName = parts.Last();
            if (codeName.Length >= 4 && cpuModel.Contains(codeName, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        // 策略 3：启发式 — 桌面 CPU 约 12W/核
        if (coreCount > 0) return coreCount * 12;

        return 0; // 完全无法判断
    }

    /// <summary>
    /// 根据主板型号推断功耗。芯片组代码优先于品牌。
    /// </summary>
    private static double EstimateMotherboardWatts(string boardProduct)
    {
        if (string.IsNullOrWhiteSpace(boardProduct)) return 20.0;

        var p = boardProduct;

        // 高端芯片组
        if (p.Contains("Z690") || p.Contains("Z790") || p.Contains("Z890") ||
            p.Contains("X670") || p.Contains("X570") || p.Contains("X870") ||
            p.Contains("X399") || p.Contains("TRX40") || p.Contains("WRX80"))
            return 22.0;

        // 中端芯片组
        if (p.Contains("B650") || p.Contains("B660") || p.Contains("B760") ||
            p.Contains("B550") || p.Contains("B450") || p.Contains("B560") ||
            p.Contains("H670") || p.Contains("H770"))
            return 18.0;

        // 入门芯片组
        if (p.Contains("A620") || p.Contains("A520") || p.Contains("H610") ||
            p.Contains("H510") || p.Contains("H410"))
            return 15.0;

        // 品牌识别（芯片组未命中时的补充）
        if (p.Contains("PROART") || p.Contains("ROG") || p.Contains("MEG") || p.Contains("AORUS"))
            return 22.0;

        // ITX / 移动平台
        if (p.Contains("ITX") || p.Contains("NUC") || p.Contains("LattePanda"))
            return 12.0;

        return 20.0;
    }
}
