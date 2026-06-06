namespace PowerMonitor.Core.Services;

public enum SamplingState
{
    Stopped,
    Running,
    Paused
}

public interface ISamplingScheduler : IDisposable
{
    SamplingState State { get; }
    int IntervalSeconds { get; set; }
    event EventHandler? SampleTick;
    void Start();
    void Pause();
    void Resume();
    void Stop();
}
