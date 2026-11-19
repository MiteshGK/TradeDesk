namespace TradeDesk.Core.Domain;

public static class TradeCalculator
{
    /// <summary>Day-count basis used for INR money-market deals (Actual/365).</summary>
    public const int DayCountBasis = 365;

    /// <summary>Simple interest for the tenor, rounded to 2 decimals (half away from zero).</summary>
    public static decimal Interest(decimal principal, decimal ratePercent, int tenorDays)
    {
        if (principal <= 0 || tenorDays <= 0) return 0m;
        var raw = principal * ratePercent / 100m * tenorDays / DayCountBasis;
        return Math.Round(raw, 2, MidpointRounding.AwayFromZero);
    }
}
