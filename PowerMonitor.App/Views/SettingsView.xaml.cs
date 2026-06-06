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
}
