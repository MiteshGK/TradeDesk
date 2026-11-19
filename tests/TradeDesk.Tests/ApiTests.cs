using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TradeDesk.Core.Domain;

namespace TradeDesk.Tests;

public class ApiTests(ApiTests.Factory factory) : IClassFixture<ApiTests.Factory>
{
    public class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tradedesk-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => builder.UseSetting("ConnectionStrings:TradeDesk", $"Data Source={_dbPath};Pooling=False");

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            File.Delete(_dbPath);
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _client = factory.CreateClient();
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public async Task Blotter_returns_seeded_trades()
    {
        var trades = await _client.GetFromJsonAsync<List<TradeDto>>("/api/trades", Json);
        Assert.NotNull(trades);
        Assert.True(trades.Count >= 6);
    }

    [Fact]
    public async Task Booking_returns_201_with_location()
    {
        var response = await _client.PostAsJsonAsync("/api/trades",
            new BookTradeRequest("Fintrust Bank", TradeSide.Lend, 5_000_000m, 6.5m, Today, Today, Today.AddDays(3)), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var trade = await response.Content.ReadFromJsonAsync<TradeDto>(Json);
        Assert.Equal(2_671.23m, trade!.Interest);
    }

    [Fact]
    public async Task Invalid_booking_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/api/trades",
            new BookTradeRequest("", TradeSide.Lend, 0m, 6.5m, Today, Today, Today), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Principal", body);
        Assert.Contains("Counterparty", body);
    }

    [Fact]
    public async Task Unknown_trade_returns_404_and_bad_transition_returns_409()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/trades/99999")).StatusCode);

        var matured = (await _client.GetFromJsonAsync<List<TradeDto>>("/api/trades?status=Matured", Json))!.First();
        var response = await _client.PostAsync($"/api/trades/{matured.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
