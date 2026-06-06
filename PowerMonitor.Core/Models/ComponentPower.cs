namespace PowerMonitor.Core.Models;

public enum PowerSource
{
    Sensor,
    Estimated
}

public class ComponentPower
{
    public string Name { get; set; } = string.Empty;
    public double Watts { get; set; }
    public PowerSource Source { get; set; }
}
