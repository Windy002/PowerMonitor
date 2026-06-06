using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PowerMonitor.Core.Models;
using PowerMonitor.Core.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace PowerMonitor.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IPowerMonitorService _powerService;
    private readonly List<DataPoint> _powerHistory = new();
    private const int MaxPoints = 720;

    public MainViewModel(IPowerMonitorService powerService)
    {
        _powerService = powerService;
        PlotModel = new PlotModel { Title = "实时功率 (W)" };
        PlotModel.Axes.Add(new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "HH:mm:ss",
            IntervalType = DateTimeIntervalType.Seconds
        });
        PlotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "W",
            Minimum = 0
        });
        var series = new LineSeries
        {
            Color = OxyColor.FromRgb(0, 200, 100),
            StrokeThickness = 2,
            LineStyle = LineStyle.Solid
        };
        PlotModel.Series.Add(series);

        _powerService.SnapshotUpdated += OnSnapshotUpdated;
    }

    private double _totalWatts;
    public double TotalWatts
    {
        get => _totalWatts;
        set { _totalWatts = value; OnPropertyChanged(); }
    }

    private string _stateText = "已停止";
    public string StateText
    {
        get => _stateText;
        set { _stateText = value; OnPropertyChanged(); }
    }

    private string _dayCost = "¥0.00";
    public string DayCost
    {
        get => _dayCost;
        set { _dayCost = value; OnPropertyChanged(); }
    }

    private string _weekCost = "¥0.00";
    public string WeekCost
    {
        get => _weekCost;
        set { _weekCost = value; OnPropertyChanged(); }
    }

    private string _monthCost = "¥0.00";
    public string MonthCost
    {
        get => _monthCost;
        set { _monthCost = value; OnPropertyChanged(); }
    }

    private string _priceInfo = "¥0.60/kWh";
    public string PriceInfo
    {
        get => _priceInfo;
        set { _priceInfo = value; OnPropertyChanged(); }
    }

    private string _intervalInfo = "采样: 5秒";
    public string IntervalInfo
    {
        get => _intervalInfo;
        set { _intervalInfo = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ComponentPower> Components { get; } = new();
    public PlotModel PlotModel { get; }

    public void TogglePause()
    {
        var snapshot = _powerService.GetLatestSnapshot();
        if (snapshot.State == SamplingState.Running)
            _powerService.Pause();
        else if (snapshot.State == SamplingState.Paused)
            _powerService.Resume();
    }

    public void ExportCsv()
    {
        var dateDialog = new Views.ExportDialog();
        dateDialog.Owner = System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive);
        dateDialog.ShowDialog();

        if (!dateDialog.Confirmed) return;

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = $"power_export_{dateDialog.StartDate:yyyyMMdd}_{dateDialog.EndDate.AddDays(-1):yyyyMMdd}.csv"
        };
        if (saveDialog.ShowDialog() == true)
        {
            _powerService.ExportCsv(dateDialog.StartDate, dateDialog.EndDate, saveDialog.FileName);
            System.Windows.MessageBox.Show("导出成功", "导出", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    private void OnSnapshotUpdated(object? sender, PowerSnapshot snapshot)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            TotalWatts = snapshot.TotalWatts;
            StateText = snapshot.State == SamplingState.Running ? "运行中 🟢" : "已暂停 🟡";
            DayCost = $"¥{snapshot.DayBilling.Cost:F2}";
            WeekCost = $"¥{snapshot.WeekBilling.Cost:F2}";
            MonthCost = $"¥{snapshot.MonthBilling.Cost:F2}";
            PriceInfo = $"¥{_powerService.GetPricePerKwh():F2}/kWh";
            IntervalInfo = $"采样: {_powerService.GetInterval()}秒";

            Components.Clear();
            foreach (var c in snapshot.Components)
                Components.Add(c);

            _powerHistory.Add(new DataPoint(
                DateTimeAxis.ToDouble(snapshot.Timestamp), snapshot.TotalWatts));
            while (_powerHistory.Count > MaxPoints)
                _powerHistory.RemoveAt(0);

            var series = PlotModel.Series[0] as LineSeries;
            if (series != null)
            {
                series.ItemsSource = _powerHistory.ToArray();
                PlotModel.InvalidatePlot(true);
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
