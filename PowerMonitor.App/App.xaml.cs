using System.IO;
using System.Windows;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using PowerMonitor.Core.Services;
using PowerMonitor.App.Controls;

namespace PowerMonitor.App;

public partial class App : Application
{
    private static readonly Mutex AppMutex = new(true, "PowerMonitor_SingleInstance");
    private ServiceProvider? _serviceProvider;
    private TrayIconManager? _trayManager;

    public App()
    {
        if (!AppMutex.WaitOne(TimeSpan.Zero, true))
        {
            MessageBox.Show("PowerMonitor 已经在运行中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerMonitor", "power.db");

        services.AddSingleton<ISensorService, SensorService>();
        services.AddSingleton<IPowerEstimationEngine, PowerEstimationEngine>();
        services.AddSingleton<IBillingCalculator, BillingCalculator>();
        services.AddSingleton<ISamplingScheduler, SamplingScheduler>();
        services.AddSingleton<IPowerMonitorService>(sp =>
            new PowerMonitorService(
                sp.GetRequiredService<ISensorService>(),
                sp.GetRequiredService<IPowerEstimationEngine>(),
                sp.GetRequiredService<IBillingCalculator>(),
                sp.GetRequiredService<ISamplingScheduler>(),
                dbPath));
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var powerService = _serviceProvider!.GetRequiredService<IPowerMonitorService>();
        _trayManager = new TrayIconManager(powerService, _serviceProvider!);

        try
        {
            powerService.Start();
            _trayManager.OpenDashboard();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayManager?.Dispose();
        var powerService = _serviceProvider!.GetRequiredService<IPowerMonitorService>();
        powerService.Stop();
        _serviceProvider!.Dispose();
        base.OnExit(e);
    }
}
