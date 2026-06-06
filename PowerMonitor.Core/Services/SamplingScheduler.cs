using PowerMonitor.Core.Logging;
using System.Timers;

namespace PowerMonitor.Core.Services;

public class SamplingScheduler : ISamplingScheduler, IDisposable
{
    private System.Timers.Timer? _timer;
    private int _intervalSeconds = 5;

    public SamplingState State { get; private set; } = SamplingState.Stopped;
    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set
        {
            _intervalSeconds = Math.Clamp(value, 1, 60);
            if (_timer != null)
                _timer.Interval = _intervalSeconds * 1000;
        }
    }

    public event EventHandler? SampleTick;

    public void Start()
    {
        if (State == SamplingState.Running) return;
        _timer = new System.Timers.Timer(_intervalSeconds * 1000);
        _timer.Elapsed += OnTick;
        _timer.AutoReset = true;
        _timer.Start();
        State = SamplingState.Running;
        Logger.Info("Sampling started");
    }

    public void Pause()
    {
        if (State != SamplingState.Running) return;
        _timer?.Stop();
        State = SamplingState.Paused;
        Logger.Info("Sampling paused");
    }

    public void Resume()
    {
        if (State != SamplingState.Paused) return;
        _timer?.Start();
        State = SamplingState.Running;
        Logger.Info("Sampling resumed");
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        State = SamplingState.Stopped;
        Logger.Info("Sampling stopped");
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        SampleTick?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
