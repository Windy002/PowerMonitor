namespace PowerMonitor.Core.Services;

public class BillingPeriod
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double TotalKwh { get; set; }
    public double Cost { get; set; }
}

public interface IBillingCalculator
{
    double WattsToKwh(double watts, int intervalSeconds);
    double KwhToCost(double kwh, double pricePerKwh);
    BillingPeriod CalculateDayBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh);
    BillingPeriod CalculateWeekBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh);
    BillingPeriod CalculateMonthBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh);
}
