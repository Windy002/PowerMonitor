using System.Windows.Controls;
using PowerMonitor.App.ViewModels;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.Views;

public partial class DashboardView : UserControl
{
    private readonly MainViewModel _viewModel;

    public DashboardView(IPowerMonitorService powerService)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(powerService);
        DataContext = _viewModel;
    }

    private void TogglePause_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.TogglePause();
    }

    private void ExportCsv_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.ExportCsv();
    }
}
