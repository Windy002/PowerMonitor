using System.IO;
using System.Windows;
using PowerMonitor.Core.Services;
using PowerMonitor.App.Views;

namespace PowerMonitor.App;

public partial class MainWindow : Window
{
    private readonly IPowerMonitorService _powerService;

    public MainWindow(IPowerMonitorService powerService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _powerService = powerService;
        DashboardFrame.Content = new DashboardView(powerService);
        HistoryFrame.Content = new HistoryView(powerService);
        SettingsFrame.Content = new SettingsView(powerService);
        RestoreWindowPosition();
        Closed += OnClosed;
    }

    private void RestoreWindowPosition()
    {
        var snap = _powerService.GetLatestSnapshot();
        // Window position stored in config as "window_x,y,w,h"
        // We'll use a simple approach: just store in a local file
        var posFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerMonitor", "window.pos");
        if (File.Exists(posFile))
        {
            try
            {
                var parts = File.ReadAllText(posFile).Split(',');
                if (parts.Length == 4 &&
                    double.TryParse(parts[0], out var x) &&
                    double.TryParse(parts[1], out var y) &&
                    double.TryParse(parts[2], out var w) &&
                    double.TryParse(parts[3], out var h))
                {
                    Left = Math.Max(0, x);
                    Top = Math.Max(0, y);
                    Width = Math.Clamp(w, MinWidth, 1920);
                    Height = Math.Clamp(h, MinHeight, 1080);
                }
            }
            catch { }
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        var posFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerMonitor", "window.pos");
        try
        {
            var dir = Path.GetDirectoryName(posFile);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(posFile, $"{Left},{Top},{Width},{Height}");
        }
        catch { }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }
}
