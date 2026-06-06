using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PowerMonitor.Core.Services;

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

        _notifyIcon.Icon = GenerateTrayIcon(0);

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add("打开仪表盘", null, OnOpenDashboard!);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("暂停采样", null, OnTogglePause!);
        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        contextMenu.Items.Add("退出", null, OnExit!);
        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += OnOpenDashboard!;

        _powerService.SnapshotUpdated += OnSnapshotUpdated;
        _powerService.SensorWarning += OnSensorWarning;
    }

    private void OnSnapshotUpdated(object? sender, PowerSnapshot snapshot)
    {
        _notifyIcon.Text = $"总功率: {snapshot.TotalWatts:F0}W\n" +
                          $"今日电费: ¥{snapshot.DayBilling.Cost:F2}\n" +
                          $"状态: {(snapshot.State == SamplingState.Running ? "运行中" : "已暂停")}";
    }

    private void OnSensorWarning(object? sender, string message)
    {
        _notifyIcon.ShowBalloonTip(5000, "功耗监控警告", message, System.Windows.Forms.ToolTipIcon.Warning);
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
        _powerService.SensorWarning -= OnSensorWarning;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
