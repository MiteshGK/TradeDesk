using Microsoft.EntityFrameworkCore;
using TradeDesk.Core.Data;
using TradeDesk.Core.Domain;

namespace TradeDesk.Core.Services;

public interface ITradeService
{
    Task<IReadOnlyList<TradeDto>> GetTradesAsync(TradeQuery query, CancellationToken ct = default);
    Task<TradeDto> GetAsync(int id, CancellationToken ct = default);
    Task<TradeDto> BookAsync(BookTradeRequest request, CancellationToken ct = default);
    Task<TradeDto> SettleAsync(int id, CancellationToken ct = default);
    Task<TradeDto> MatureAsync(int id, CancellationToken ct = default);
    Task<TradeDto> CancelAsync(int id, CancellationToken ct = default);
    Task<BlotterSummary> GetSummaryAsync(CancellationToken ct = default);
}

public class TradeService(TradeDbContext db, TimeProvider clock) : ITradeService
{
    public const decimal MaxPrincipal = 10_000_000_000m;
    public const int MaxTenorDays = 365;

    private DateOnly Today => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    public async Task<IReadOnlyList<TradeDto>> GetTradesAsync(TradeQuery query, CancellationToken ct = default)
    {
        IQueryable<Trade> q = db.Trades.AsNoTracking();
        if (query.Status is { } status) q = q.Where(t => t.Status == status);
        if (query.Side is { } side) q = q.Where(t => t.Side == side);
        if (!string.IsNullOrWhiteSpace(query.Counterparty))
            q = q.Where(t => t.Counterparty.Contains(query.Counterparty.Trim()));

        var trades = await q.OrderByDescending(t => t.StartDate).ThenByDescending(t => t.Id).ToListAsync(ct);
        return trades.Select(TradeDto.From).ToList();
    }

    public async Task<TradeDto> GetAsync(int id, CancellationToken ct = default)
        => TradeDto.From(await FindAsync(id, ct));

    public async Task<TradeDto> BookAsync(BookTradeRequest r, CancellationToken ct = default)
    {
        Validate(r);

        var prefix = $"TD-{r.TradeDate:yyyyMMdd}-";
        var countToday = await db.Trades.CountAsync(t => t.TradeRef.StartsWith(prefix), ct);

        var trade = new Trade
        {
            TradeRef = $"{prefix}{countToday + 1:D4}",
            Counterparty = r.Counterparty.Trim(),
            Side = r.Side,
            Principal = r.Principal,
            RatePercent = r.RatePercent,
            TradeDate = r.TradeDate,
            StartDate = r.StartDate,
            MaturityDate = r.MaturityDate,
        };
        db.Trades.Add(trade);
        await db.SaveChangesAsync(ct);
        return TradeDto.From(trade);
    }

    public Task<TradeDto> SettleAsync(int id, CancellationToken ct = default)
        => TransitionAsync(id, TradeStatus.Booked, TradeStatus.Settled, ct);

    public Task<TradeDto> MatureAsync(int id, CancellationToken ct = default)
        => TransitionAsync(id, TradeStatus.Settled, TradeStatus.Matured, ct);

    public Task<TradeDto> CancelAsync(int id, CancellationToken ct = default)
        => TransitionAsync(id, TradeStatus.Booked, TradeStatus.Cancelled, ct);

    public async Task<BlotterSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        // Aggregated in memory: SQLite cannot SUM decimals server-side, and the open book is small.
        var open = await db.Trades.AsNoTracking()
            .Where(t => t.Status == TradeStatus.Booked || t.Status == TradeStatus.Settled)
            .ToListAsync(ct);

        var lent = open.Where(t => t.Side == TradeSide.Lend).ToList();
        var borrowed = open.Where(t => t.Side == TradeSide.Borrow).ToList();
        var totalLent = lent.Sum(t => t.Principal);
        var totalBorrowed = borrowed.Sum(t => t.Principal);
        var totalPrincipal = totalLent + totalBorrowed;

        var wavg = totalPrincipal == 0 ? 0 :
            Math.Round(open.Sum(t => t.Principal * t.RatePercent) / totalPrincipal, 4);

        var exposures = open
            .GroupBy(t => t.Counterparty)
            .Select(g => new CounterpartyExposure(
                g.Key,
                g.Sum(t => t.Side == TradeSide.Lend ? t.Principal : -t.Principal),
                g.Count()))
            .OrderByDescending(e => Math.Abs(e.NetLent))
            .Take(5)
            .ToList();

        return new BlotterSummary(
            open.Count, totalLent, totalBorrowed, totalLent - totalBorrowed, wavg,
            lent.Sum(t => t.Interest), borrowed.Sum(t => t.Interest), exposures);
    }

    private async Task<TradeDto> TransitionAsync(int id, TradeStatus from, TradeStatus to, CancellationToken ct)
    {
        var trade = await FindAsync(id, ct);
        if (trade.Status != from)
            throw new TradeStateException($"Trade {trade.TradeRef} is {trade.Status}; only {from} trades can be marked {to}.");

        if (to == TradeStatus.Settled && trade.StartDate > Today)
            throw new TradeStateException($"Trade {trade.TradeRef} cannot settle before its start date {trade.StartDate:yyyy-MM-dd}.");
        if (to == TradeStatus.Matured && trade.MaturityDate > Today)
            throw new TradeStateException($"Trade {trade.TradeRef} cannot mature before {trade.MaturityDate:yyyy-MM-dd}.");

        trade.Status = to;
        trade.UpdatedUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return TradeDto.From(trade);
    }

    private async Task<Trade> FindAsync(int id, CancellationToken ct)
        => await db.Trades.FindAsync([id], ct) ?? throw new TradeNotFoundException(id);

    private void Validate(BookTradeRequest r)
    {
        var errors = new Dictionary<string, string[]>();
        void Add(string field, string message) => errors[field] = [.. errors.GetValueOrDefault(field, []), message];

        if (string.IsNullOrWhiteSpace(r.Counterparty)) Add(nameof(r.Counterparty), "Counterparty is required.");
        else if (r.Counterparty.Trim().Length > 100) Add(nameof(r.Counterparty), "Counterparty must be 100 characters or fewer.");

        if (r.Principal <= 0) Add(nameof(r.Principal), "Principal must be greater than zero.");
        else if (r.Principal > MaxPrincipal) Add(nameof(r.Principal), $"Principal cannot exceed {MaxPrincipal:N0}.");
        else if (decimal.Round(r.Principal, 2) != r.Principal) Add(nameof(r.Principal), "Principal can have at most 2 decimal places.");

        if (r.RatePercent <= 0 || r.RatePercent >= 100) Add(nameof(r.RatePercent), "Rate must be between 0 and 100 percent.");

        if (r.TradeDate > Today) Add(nameof(r.TradeDate), "Trade date cannot be in the future.");
        if (r.StartDate < r.TradeDate) Add(nameof(r.StartDate), "Start date cannot be before the trade date.");

        var tenor = r.MaturityDate.DayNumber - r.StartDate.DayNumber;
        if (tenor <= 0) Add(nameof(r.MaturityDate), "Maturity date must be after the start date.");
        else if (tenor > MaxTenorDays) Add(nameof(r.MaturityDate), $"Tenor cannot exceed {MaxTenorDays} days.");

        if (errors.Count > 0) throw new TradeValidationException(errors);
    }
}
