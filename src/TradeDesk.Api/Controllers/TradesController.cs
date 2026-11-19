using Microsoft.AspNetCore.Mvc;
using TradeDesk.Core.Domain;
using TradeDesk.Core.Services;

namespace TradeDesk.Api.Controllers;

[ApiController]
[Route("api/trades")]
[Produces("application/json")]
public class TradesController(ITradeService trades) : ControllerBase
{
    /// <summary>Blotter: all trades, optionally filtered by status, side or counterparty.</summary>
    [HttpGet]
    public Task<IReadOnlyList<TradeDto>> List([FromQuery] TradeStatus? status, [FromQuery] TradeSide? side,
        [FromQuery] string? counterparty, CancellationToken ct)
        => trades.GetTradesAsync(new TradeQuery(status, counterparty, side), ct);

    [HttpGet("{id:int}")]
    public Task<TradeDto> Get(int id, CancellationToken ct) => trades.GetAsync(id, ct);

    [HttpGet("summary")]
    public Task<BlotterSummary> Summary(CancellationToken ct) => trades.GetSummaryAsync(ct);

    [HttpPost]
    [ProducesResponseType<TradeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TradeDto>> Book(BookTradeRequest request, CancellationToken ct)
    {
        var trade = await trades.BookAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = trade.Id }, trade);
    }

    [HttpPost("{id:int}/settle")]
    public Task<TradeDto> Settle(int id, CancellationToken ct) => trades.SettleAsync(id, ct);

    [HttpPost("{id:int}/mature")]
    public Task<TradeDto> Mature(int id, CancellationToken ct) => trades.MatureAsync(id, ct);

    [HttpPost("{id:int}/cancel")]
    public Task<TradeDto> Cancel(int id, CancellationToken ct) => trades.CancelAsync(id, ct);
}
