using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PowerMonitor.Core.Services;
using PowerMonitor.App.Views;

namespace PowerMonitor.App;

public partial class MainWindow : Window
{
    private readonly IPowerMonitorService _powerService;

    public MainWindow(IPowerMonitorService powerService, IServiceProvider? _ = null)
    {
        InitializeComponent();
        _powerService = powerService;

        DashboardFrame.Content = new DashboardView(powerService);
        HistoryFrame.Content = new HistoryView(powerService);
        SettingsFrame.Content = new SettingsView(powerService);

        RestoreWindowPosition();
        Closing += OnClosing;
        SelectTab(0);
    }

    private void RestoreWindowPosition()
    {
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

    private void OnClosing(object? sender, CancelEventArgs e)
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
        e.Cancel = true;
        Hide();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else
                DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string tag)
            SelectTab(int.Parse(tag));
    }

    private void SelectTab(int index)
    {
        // Reset all tabs
        DashBtn.ClearValue(BackgroundProperty);
        HistBtn.ClearValue(BackgroundProperty);
        SetBtn.ClearValue(BackgroundProperty);
        DashBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        HistBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        SetBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));

        // Highlight active
        var active = index switch { 0 => DashBtn, 1 => HistBtn, _ => SetBtn };
        active.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
        active.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

        // Switch content
        DashboardFrame.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryFrame.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        SettingsFrame.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized) Hide();
    }
}
