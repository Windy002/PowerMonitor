namespace PowerMonitor.Core.Models;

public class PowerSample
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public double TotalWatts { get; set; }
    public string SensorJson { get; set; } = "[]";
    public double PricePerKwh { get; set; }
}
