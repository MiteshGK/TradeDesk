using TradeDesk.Core.Domain;
using TradeDesk.Core.Services;

namespace TradeDesk.Tests;

public class TradeServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 19);
    private readonly TestDb _db = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 11, 19, 10, 0, 0, TimeSpan.Zero));
    private readonly TradeService _service;

    public TradeServiceTests() => _service = new TradeService(_db.Context, _clock);

    public void Dispose() => _db.Dispose();

    private static BookTradeRequest Request(
        string cpty = "Alpha Bank", TradeSide side = TradeSide.Lend, decimal principal = 100_000_000m,
        decimal rate = 6.5m, int startOffset = 0, int tenor = 7)
        => new(cpty, side, principal, rate, Today, Today.AddDays(startOffset), Today.AddDays(startOffset + tenor));

    [Fact]
    public async Task Book_assigns_sequential_refs_and_computes_amounts()
    {
        var first = await _service.BookAsync(Request());
        var second = await _service.BookAsync(Request(cpty: "  Beacon Fund  "));

        Assert.Equal("TD-20261119-0001", first.TradeRef);
        Assert.Equal("TD-20261119-0002", second.TradeRef);
        Assert.Equal("Beacon Fund", second.Counterparty);
        Assert.Equal(TradeStatus.Booked, first.Status);
        Assert.Equal(7, first.TenorDays);
        Assert.Equal(124_657.53m, first.Interest);
    }

    [Fact]
    public async Task Book_rejects_invalid_requests_with_every_error()
    {
        var bad = new BookTradeRequest("", TradeSide.Lend, -1m, 0m, Today.AddDays(1), Today, Today);

        var ex = await Assert.ThrowsAsync<TradeValidationException>(() => _service.BookAsync(bad));

        Assert.Equal(
            ["Counterparty", "MaturityDate", "Principal", "RatePercent", "StartDate", "TradeDate"],
            ex.Errors.Keys.Order());
    }

    [Theory]
    [InlineData("10000000001")]
    [InlineData("100.005")]
    public async Task Book_rejects_out_of_range_principal(string principal)
    {
        var ex = await Assert.ThrowsAsync<TradeValidationException>(
            () => _service.BookAsync(Request(principal: decimal.Parse(principal, System.Globalization.CultureInfo.InvariantCulture))));
        Assert.Contains("Principal", ex.Errors.Keys);
    }

    [Fact]
    public async Task Book_rejects_tenor_over_one_year()
    {
        var ex = await Assert.ThrowsAsync<TradeValidationException>(() => _service.BookAsync(Request(tenor: 366)));
        Assert.Contains("MaturityDate", ex.Errors.Keys);
    }

    [Fact]
    public async Task Lifecycle_moves_booked_to_settled_to_matured()
    {
        var trade = await _service.BookAsync(Request(tenor: 1));

        var settled = await _service.SettleAsync(trade.Id);
        Assert.Equal(TradeStatus.Settled, settled.Status);

        _clock.Now = _clock.Now.AddDays(1);
        var matured = await _service.MatureAsync(trade.Id);
        Assert.Equal(TradeStatus.Matured, matured.Status);
    }

    [Fact]
    public async Task Cannot_settle_forward_start_trade_early()
    {
        var trade = await _service.BookAsync(Request(startOffset: 2));
        await Assert.ThrowsAsync<TradeStateException>(() => _service.SettleAsync(trade.Id));
    }

    [Fact]
    public async Task Cannot_mature_before_maturity_date()
    {
        var trade = await _service.BookAsync(Request(tenor: 7));
        await _service.SettleAsync(trade.Id);
        await Assert.ThrowsAsync<TradeStateException>(() => _service.MatureAsync(trade.Id));
    }

    [Fact]
    public async Task Cannot_cancel_settled_trade()
    {
        var trade = await _service.BookAsync(Request());
        await _service.SettleAsync(trade.Id);
        await Assert.ThrowsAsync<TradeStateException>(() => _service.CancelAsync(trade.Id));
    }

    [Fact]
    public async Task Unknown_trade_throws_not_found()
        => await Assert.ThrowsAsync<TradeNotFoundException>(() => _service.GetAsync(999));

    [Fact]
    public async Task Summary_nets_open_positions_and_ignores_closed_trades()
    {
        await _service.BookAsync(Request("Alpha Bank", TradeSide.Lend, 300_000_000m, 6.0m));
        await _service.BookAsync(Request("Alpha Bank", TradeSide.Borrow, 100_000_000m, 7.0m));
        await _service.BookAsync(Request("Crest Insurance", TradeSide.Borrow, 50_000_000m, 6.0m));
        var cancelled = await _service.BookAsync(Request("Delta Securities", TradeSide.Lend, 999_000_000m));
        await _service.CancelAsync(cancelled.Id);

        var s = await _service.GetSummaryAsync();

        Assert.Equal(3, s.OpenTrades);
        Assert.Equal(300_000_000m, s.TotalLent);
        Assert.Equal(150_000_000m, s.TotalBorrowed);
        Assert.Equal(150_000_000m, s.NetPosition);
        Assert.Equal(6.2222m, s.WeightedAverageRate);
        Assert.Equal("Alpha Bank", s.TopExposures[0].Counterparty);
        Assert.Equal(200_000_000m, s.TopExposures[0].NetLent);
        Assert.DoesNotContain(s.TopExposures, e => e.Counterparty == "Delta Securities");
    }

    [Fact]
    public async Task Filters_by_status_side_and_counterparty()
    {
        await _service.BookAsync(Request("Alpha Bank", TradeSide.Lend));
        await _service.BookAsync(Request("Beacon Fund", TradeSide.Borrow));
        var t = await _service.BookAsync(Request("Alpha Bank", TradeSide.Borrow));
        await _service.SettleAsync(t.Id);

        Assert.Equal(2, (await _service.GetTradesAsync(new(Counterparty: "Alpha"))).Count);
        Assert.Equal(2, (await _service.GetTradesAsync(new(Side: TradeSide.Borrow))).Count);
        Assert.Single(await _service.GetTradesAsync(new(Status: TradeStatus.Settled)));
    }
}
