namespace PowerMonitor.Core.Services;

public class BillingCalculator : IBillingCalculator
{
    public double WattsToKwh(double watts, int intervalSeconds)
    {
        return watts * intervalSeconds / 3_600_000.0;
    }

    public double KwhToCost(double kwh, double pricePerKwh)
    {
        return kwh * pricePerKwh;
    }

    public BillingPeriod CalculateDayBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh)
    {
        var today = DateTime.Today;
        return CalculateBilling(samples, pricePerKwh, today, today.AddDays(1));
    }

    public BillingPeriod CalculateWeekBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh)
    {
        var today = DateTime.Today;
        int diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var monday = today.AddDays(-diff);
        return CalculateBilling(samples, pricePerKwh, monday, today.AddDays(1));
    }

    public BillingPeriod CalculateMonthBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh)
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);
        return CalculateBilling(samples, pricePerKwh, firstOfMonth, today.AddDays(1));
    }

    private BillingPeriod CalculateBilling(List<(DateTime Timestamp, double TotalWatts)> samples, double pricePerKwh, DateTime start, DateTime end)
    {
        var periodSamples = samples.Where(s => s.Timestamp >= start && s.Timestamp < end).ToList();
        double totalKwh = 0;

        for (int i = 0; i < periodSamples.Count; i++)
        {
            int intervalSeconds;
            if (i < periodSamples.Count - 1)
            {
                intervalSeconds = (int)(periodSamples[i + 1].Timestamp - periodSamples[i].Timestamp).TotalSeconds;
            }
            else
            {
                intervalSeconds = 5;
            }
            if (intervalSeconds <= 0) intervalSeconds = 5;
            if (intervalSeconds > 300) intervalSeconds = 5;

            totalKwh += WattsToKwh(periodSamples[i].TotalWatts, intervalSeconds);
        }

        return new BillingPeriod
        {
            Start = start,
            End = end,
            TotalKwh = Math.Round(totalKwh, 4),
            Cost = Math.Round(totalKwh * pricePerKwh, 2)
        };
    }
}
