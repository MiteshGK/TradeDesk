namespace TradeDesk.Core.Domain;

public record BookTradeRequest(
    string Counterparty,
    TradeSide Side,
    decimal Principal,
    decimal RatePercent,
    DateOnly TradeDate,
    DateOnly StartDate,
    DateOnly MaturityDate);

public record TradeDto(
    int Id,
    string TradeRef,
    string Counterparty,
    TradeSide Side,
    decimal Principal,
    decimal RatePercent,
    DateOnly TradeDate,
    DateOnly StartDate,
    DateOnly MaturityDate,
    int TenorDays,
    decimal Interest,
    decimal MaturityAmount,
    TradeStatus Status)
{
    public static TradeDto From(Trade t) => new(
        t.Id, t.TradeRef, t.Counterparty, t.Side, t.Principal, t.RatePercent,
        t.TradeDate, t.StartDate, t.MaturityDate, t.TenorDays, t.Interest, t.MaturityAmount, t.Status);
}

public record CounterpartyExposure(string Counterparty, decimal NetLent, int OpenTrades);

public record BlotterSummary(
    int OpenTrades,
    decimal TotalLent,
    decimal TotalBorrowed,
    decimal NetPosition,
    decimal WeightedAverageRate,
    decimal InterestReceivable,
    decimal InterestPayable,
    IReadOnlyList<CounterpartyExposure> TopExposures);

public record TradeQuery(TradeStatus? Status = null, string? Counterparty = null, TradeSide? Side = null);
