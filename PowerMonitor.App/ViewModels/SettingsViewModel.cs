using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PowerMonitor.Core.Services;

namespace PowerMonitor.App.ViewModels;

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly IPowerMonitorService _powerService;

    public SettingsViewModel(IPowerMonitorService powerService)
    {
        _powerService = powerService;
        _pricePerKwh = _powerService.GetPricePerKwh().ToString("F2");
        _interval = _powerService.GetInterval();
        _retentionDays = _powerService.GetRetentionDays();

        SaveCommand = new RelayCommand(_ => Save());
    }

    private string _pricePerKwh;
    public string PricePerKwh
    {
        get => _pricePerKwh;
        set { _pricePerKwh = value; OnPropertyChanged(); }
    }

    private int _interval;
    public int Interval
    {
        get => _interval;
        set { _interval = value; OnPropertyChanged(); }
    }

    private int _retentionDays;
    public int RetentionDays
    {
        get => _retentionDays;
        set { _retentionDays = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public ICommand SaveCommand { get; }

    private void Save()
    {
        if (double.TryParse(_pricePerKwh, out var price) && price > 0)
        {
            _powerService.SetPricePerKwh(price);
        }
        _powerService.SetInterval(Math.Clamp(_interval, 1, 60));
        _powerService.SetRetentionDays(Math.Clamp(_retentionDays, 7, 365));
        _powerService.CleanupOldData(_retentionDays);
        StatusMessage = "设置已保存 ✓";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action<object?> execute) => _execute = execute;
#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
}
