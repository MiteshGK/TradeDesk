namespace TradeDesk.Core.Domain;

public enum TradeSide { Lend, Borrow }

public enum TradeStatus { Booked, Settled, Matured, Cancelled }

/// <summary>
/// A collateralised money-market deal (repo-style): one side lends cash for a tenor
/// and receives principal plus interest on the maturity date.
/// </summary>
public class Trade
{
    public int Id { get; set; }
    public string TradeRef { get; set; } = string.Empty;
    public string Counterparty { get; set; } = string.Empty;
    public TradeSide Side { get; set; }
    public decimal Principal { get; set; }
    public decimal RatePercent { get; set; }
    public DateOnly TradeDate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly MaturityDate { get; set; }
    public TradeStatus Status { get; set; } = TradeStatus.Booked;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedUtc { get; set; }

    public int TenorDays => MaturityDate.DayNumber - StartDate.DayNumber;
    public decimal Interest => TradeCalculator.Interest(Principal, RatePercent, TenorDays);
    public decimal MaturityAmount => Principal + Interest;
}
