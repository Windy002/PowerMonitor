using System.Windows.Controls;
using PowerMonitor.App.ViewModels;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.Views;

public partial class HistoryView : UserControl
{
    private readonly HistoryViewModel _viewModel;

    public HistoryView(IPowerMonitorService powerService)
    {
        InitializeComponent();
        _viewModel = new HistoryViewModel(powerService);
        DataContext = _viewModel;
    }

    private void Day_Click(object sender, System.Windows.RoutedEventArgs e) => _viewModel.LoadDay();
    private void Week_Click(object sender, System.Windows.RoutedEventArgs e) => _viewModel.LoadWeek();
    private void Month_Click(object sender, System.Windows.RoutedEventArgs e) => _viewModel.LoadMonth();
}
