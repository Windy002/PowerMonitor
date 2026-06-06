using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.ViewModels;

public class HistoryViewModel : INotifyPropertyChanged
{
    private readonly IPowerMonitorService _powerService;

    public HistoryViewModel(IPowerMonitorService powerService)
    {
        _powerService = powerService;
        LoadDay();
    }

    public ObservableCollection<HistoryRow> Rows { get; } = new();

    private string _title = "历史用电";
    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public void LoadDay()
    {
        Title = "今日用电记录";
        var today = DateTime.Today;
        Load(today, today.AddDays(1));
    }

    public void LoadWeek()
    {
        Title = "本周用电记录";
        int diff = (7 + (int)DateTime.Today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var monday = DateTime.Today.AddDays(-diff);
        Load(monday, DateTime.Now);
    }

    public void LoadMonth()
    {
        Title = "本月用电记录";
        var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        Load(firstOfMonth, DateTime.Now);
    }

    private void Load(DateTime from, DateTime to)
    {
        Rows.Clear();
        var samples = _powerService.GetHistory(from, to);
        var grouped = samples
            .GroupBy(s => new DateTime(s.Timestamp.Year, s.Timestamp.Month, s.Timestamp.Day, s.Timestamp.Hour, 0, 0))
            .OrderBy(g => g.Key);

        var interval = _powerService.GetInterval();
        foreach (var group in grouped)
        {
            var pricePerKwh = group.First().PricePerKwh;
            Rows.Add(new HistoryRow
            {
                Time = group.Key.ToString("MM-dd HH:mm"),
                AvgWatts = $"{group.Average(s => s.TotalWatts):F1}",
                MinWatts = $"{group.Min(s => s.TotalWatts):F1}",
                MaxWatts = $"{group.Max(s => s.TotalWatts):F1}",
                Kwh = $"{group.Sum(s => s.TotalWatts) * interval / 3_600_000.0:F4}",
                Cost = $"¥{group.Sum(s => s.TotalWatts) * interval / 3_600_000.0 * pricePerKwh:F2}"
            });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class HistoryRow
{
    public string Time { get; set; } = "";
    public string AvgWatts { get; set; } = "";
    public string MinWatts { get; set; } = "";
    public string MaxWatts { get; set; } = "";
    public string Kwh { get; set; } = "";
    public string Cost { get; set; } = "";
}
