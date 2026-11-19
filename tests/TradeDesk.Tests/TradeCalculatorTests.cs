using TradeDesk.Core.Domain;

namespace TradeDesk.Tests;

public class TradeCalculatorTests
{
    [Theory]
    [InlineData(100_000_000, 6.5, 1, 17_808.22)]      // overnight
    [InlineData(250_000_000, 6.45, 7, 309_246.58)]    // one week
    [InlineData(1_000_000, 7.0, 365, 70_000.00)]      // full year = full rate
    public void Interest_uses_actual_365_and_rounds_to_paise(decimal principal, decimal rate, int days, decimal expected)
        => Assert.Equal(expected, TradeCalculator.Interest(principal, rate, days));

    [Theory]
    [InlineData(0, 6.5, 7)]
    [InlineData(1_000_000, 6.5, 0)]
    [InlineData(-5, 6.5, 7)]
    public void Interest_is_zero_for_empty_principal_or_tenor(decimal principal, decimal rate, int days)
        => Assert.Equal(0m, TradeCalculator.Interest(principal, rate, days));

    [Fact]
    public void Trade_exposes_tenor_and_maturity_amount()
    {
        var trade = new Trade
        {
            Principal = 10_000_000m, RatePercent = 6.5m,
            StartDate = new DateOnly(2026, 1, 1), MaturityDate = new DateOnly(2026, 1, 15),
        };

        Assert.Equal(14, trade.TenorDays);
        Assert.Equal(24_931.51m, trade.Interest);
        Assert.Equal(10_024_931.51m, trade.MaturityAmount);
    }
}
