using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PowerMonitor.Core.Services;
using PowerMonitor.App.Views;

namespace PowerMonitor.App;

public partial class MainWindow : Window
{
    public MainWindow(IPowerMonitorService powerService, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        DashboardFrame.Content = new DashboardView(powerService);
        HistoryFrame.Content = new HistoryView(powerService);
        SettingsFrame.Content = new SettingsView(powerService);
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }
}
