using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradeDesk.Core.Domain;

namespace TradeDesk.Desktop.Services;

public interface ITradeApi
{
    Task<IReadOnlyList<TradeDto>> GetTradesAsync(TradeStatus? status, string? counterparty);
    Task<BlotterSummary> GetSummaryAsync();
    Task<TradeDto> BookAsync(BookTradeRequest request);
    Task<TradeDto> TransitionAsync(int id, string action);
}

/// <summary>Typed HTTP client for the TradeDesk Web API.</summary>
public sealed class TradeApiClient(HttpClient http) : ITradeApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<TradeDto>> GetTradesAsync(TradeStatus? status, string? counterparty)
    {
        var query = new List<string>();
        if (status is not null) query.Add($"status={status}");
        if (!string.IsNullOrWhiteSpace(counterparty)) query.Add($"counterparty={Uri.EscapeDataString(counterparty)}");
        var url = "api/trades" + (query.Count > 0 ? "?" + string.Join('&', query) : "");
        return await http.GetFromJsonAsync<List<TradeDto>>(url, Json) ?? [];
    }

    public async Task<BlotterSummary> GetSummaryAsync()
        => await http.GetFromJsonAsync<BlotterSummary>("api/trades/summary", Json)
           ?? throw new ApiException("Empty summary response.");

    public async Task<TradeDto> BookAsync(BookTradeRequest request)
        => await ReadAsync(await http.PostAsJsonAsync("api/trades", request, Json));

    public async Task<TradeDto> TransitionAsync(int id, string action)
        => await ReadAsync(await http.PostAsync($"api/trades/{id}/{action}", null));

    private static async Task<TradeDto> ReadAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<TradeDto>(Json))!;

        // Turn RFC 7807 problem details into a readable message for the status bar.
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var message = problem.TryGetProperty("title", out var title) ? title.GetString() : response.ReasonPhrase;
        if (problem.TryGetProperty("errors", out var errors))
            message = string.Join(" ", errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray().Select(v => v.GetString())));
        throw new ApiException(message ?? "Request failed.");
    }
}

public sealed class ApiException(string message) : Exception(message);
