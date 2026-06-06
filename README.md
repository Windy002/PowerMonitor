# ⚡ PowerMonitor — 主机功耗监控

实时监控 Windows 主机功耗并计算电费的桌面工具。通过 LibreHardwareMonitor 读取硬件传感器数据，支持系统托盘常驻、组件明细、历史记录和电费统计。

## 功能

- 🔌 **实时功耗监控** — CPU、GPU 传感器直读 + 内存/主板/硬盘/风扇估算
- 💰 **电费计算** — 自定义电价，今日/本周/本月电费汇总
- 📈 **功率曲线** — 实时波形图，支持缩放悬停
- 📋 **历史记录** — 按小时聚合，日/周/月可切换
- 📤 **导出 CSV** — 自定义时间段导出原始数据
- 🖥️ **系统托盘** — 最小化到托盘，悬停显示实时数据
- ⚙️ **可配置** — 电价、采样间隔、数据保留天数

## 截图

```
┌─────────────────────────────────────────────────────┐
│  ⚡ 主机功耗监控                            ─ ✕ 托盘  │
├──────────────┬──────────────────────────────────────┤
│  实时总功率   │  功率曲线（最近 1 小时）                │
│              │                                      │
│    234W      │  📈 实时折线图                        │
│  运行中 🟢   │                                      │
├──────────────┼──────────────────────────────────────┤
│  组件明细     │  电费汇总                             │
│  CPU   65W ● │  今日  ¥0.34                         │
│  GPU  120W ● │  本周  ¥2.15                         │
│  内存   8W ○ │  本月  ¥12.80                        │
│  主板  20W ○ │                                      │
│  其他  21W ○ │                                      │
├──────────────┴──────────────────────────────────────┤
│  [开始/暂停] [导出CSV] [设置] [历史记录]               │
└─────────────────────────────────────────────────────┘
```

## 系统要求

- Windows 10/11 (x64)
- 需要**管理员权限**运行（读取硬件传感器需要）
- 无需安装 .NET SDK 或任何运行环境

## 快速开始

### 下载使用

1. 从 [Releases](https://github.com/Windy002/PowerMonitor/releases) 下载 `PowerMonitor.App.exe`
2. 右键 → **以管理员身份运行**
3. 系统托盘出现 ⚡ 图标，双击打开仪表盘

### 从源码构建

```bash
git clone https://github.com/Windy002/PowerMonitor.git
cd PowerMonitor
dotnet run --project PowerMonitor.App
```

### 发布打包

```bash
dotnet publish PowerMonitor.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## 技术栈

- .NET 8 + WPF
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) — 硬件传感器读取
- [OxyPlot](https://github.com/oxyplot/oxyplot) — 实时功率曲线
- Microsoft.Data.Sqlite — 本地数据存储
- xUnit — 单元测试

## 数据存储

所有数据存储在 `%LocalAppData%\PowerMonitor\power.db`（SQLite），纯本地，不上传任何数据。

## 许可证

MIT
