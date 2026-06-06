using System.Windows;
using System.Windows.Controls;
using PowerMonitor.App.ViewModels;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(IPowerMonitorService powerService)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(powerService);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog();
        dialog.Owner = Window.GetWindow(this);
        dialog.ShowDialog();
    }
}
