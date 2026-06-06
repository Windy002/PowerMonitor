using PowerMonitor.Core.Services;

namespace PowerMonitor.Tests;

public class BillingCalculatorTests
{
    [Fact]
    public void WattsToKwh_ConvertsCorrectly()
    {
        var calc = new BillingCalculator();
        var kwh = calc.WattsToKwh(200, 5);
        Assert.Equal(200.0 * 5 / 3_600_000.0, kwh);
    }

    [Fact]
    public void WattsToKwh_ZeroForZeroWatts()
    {
        var calc = new BillingCalculator();
        var kwh = calc.WattsToKwh(0, 10);
        Assert.Equal(0, kwh);
    }

    [Fact]
    public void KwhToCost_CalculatesCorrectly()
    {
        var calc = new BillingCalculator();
        var cost = calc.KwhToCost(10, 0.6);
        Assert.Equal(6.0, cost);
    }

    [Fact]
    public void CalculateDayBilling_OnlyIncludesTodaySamples()
    {
        var calc = new BillingCalculator();
        var today = DateTime.Today;
        var samples = new List<(DateTime, double)>
        {
            (today.AddHours(10), 200),
            (today.AddHours(10).AddSeconds(5), 200),
            (today.AddDays(-1).AddHours(10), 100) // yesterday - excluded
        };

        var result = calc.CalculateDayBilling(samples, 0.6);
        Assert.True(result.TotalKwh > 0);
        Assert.Equal(today, result.Start.Date);
    }

    [Fact]
    public void CalculateWeekBilling_UsesMondayAsStart()
    {
        var calc = new BillingCalculator();
        var samples = new List<(DateTime, double)>();
        var result = calc.CalculateWeekBilling(samples, 0.6);
        Assert.Equal(DayOfWeek.Monday, result.Start.DayOfWeek);
    }

    [Fact]
    public void CalculateMonthBilling_UsesFirstOfMonthAsStart()
    {
        var calc = new BillingCalculator();
        var samples = new List<(DateTime, double)>();
        var result = calc.CalculateMonthBilling(samples, 0.6);
        Assert.Equal(1, result.Start.Day);
    }
}
