using System.Collections.ObjectModel;
using System.Net.Http;
using TradeDesk.Core.Domain;
using TradeDesk.Desktop.Mvvm;
using TradeDesk.Desktop.Services;

namespace TradeDesk.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ITradeApi _api;

    public MainViewModel(ITradeApi api)
    {
        _api = api;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        BookCommand = new AsyncRelayCommand(BookAsync, () => CanBook);
        SettleCommand = new AsyncRelayCommand(() => TransitionAsync("settle"), () => SelectedTrade?.Status == TradeStatus.Booked);
        MatureCommand = new AsyncRelayCommand(() => TransitionAsync("mature"), () => SelectedTrade?.Status == TradeStatus.Settled);
        CancelCommand = new AsyncRelayCommand(() => TransitionAsync("cancel"), () => SelectedTrade?.Status == TradeStatus.Booked);
    }

    // ---- Blotter ------------------------------------------------------------

    public ObservableCollection<TradeDto> Trades { get; } = [];

    public IReadOnlyList<string> StatusFilters { get; } = ["All", .. Enum.GetNames<TradeStatus>()];

    private string _statusFilter = "All";
    public string StatusFilter
    {
        get => _statusFilter;
        set { if (SetProperty(ref _statusFilter, value)) RefreshCommand.Execute(null); }
    }

    private string _counterpartyFilter = string.Empty;
    public string CounterpartyFilter
    {
        get => _counterpartyFilter;
        set => SetProperty(ref _counterpartyFilter, value);
    }

    private TradeDto? _selectedTrade;
    public TradeDto? SelectedTrade
    {
        get => _selectedTrade;
        set => SetProperty(ref _selectedTrade, value);
    }

    private BlotterSummary? _summary;
    public BlotterSummary? Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    // ---- Booking ticket -----------------------------------------------------

    public IReadOnlyList<TradeSide> Sides { get; } = Enum.GetValues<TradeSide>();

    private string _counterparty = string.Empty;
    public string Counterparty { get => _counterparty; set => SetProperty(ref _counterparty, value); }

    private TradeSide _side = TradeSide.Lend;
    public TradeSide Side { get => _side; set => SetProperty(ref _side, value); }

    private decimal _principal = 100_000_000m;
    public decimal Principal { get => _principal; set { if (SetProperty(ref _principal, value)) UpdatePreview(); } }

    private decimal _ratePercent = 6.50m;
    public decimal RatePercent { get => _ratePercent; set { if (SetProperty(ref _ratePercent, value)) UpdatePreview(); } }

    private DateTime _startDate = DateTime.Today;
    public DateTime StartDate { get => _startDate; set { if (SetProperty(ref _startDate, value)) UpdatePreview(); } }

    private int _tenorDays = 1;
    public int TenorDays { get => _tenorDays; set { if (SetProperty(ref _tenorDays, value)) UpdatePreview(); } }

    public DateTime MaturityDate => StartDate.AddDays(TenorDays);
    public decimal PreviewInterest => TradeCalculator.Interest(Principal, RatePercent, TenorDays);
    public decimal PreviewMaturityAmount => Principal + PreviewInterest;

    private bool CanBook => !string.IsNullOrWhiteSpace(Counterparty) && Principal > 0 && RatePercent > 0 && TenorDays > 0;

    private void UpdatePreview()
    {
        OnPropertyChanged(nameof(MaturityDate));
        OnPropertyChanged(nameof(PreviewInterest));
        OnPropertyChanged(nameof(PreviewMaturityAmount));
    }

    // ---- Status bar ---------------------------------------------------------

    private string _statusMessage = "Connecting…";
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private bool _hasError;
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }

    // ---- Commands -----------------------------------------------------------

    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand BookCommand { get; }
    public AsyncRelayCommand SettleCommand { get; }
    public AsyncRelayCommand MatureCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    private async Task RefreshAsync() => await RunAsync(async () =>
    {
        TradeStatus? status = Enum.TryParse<TradeStatus>(StatusFilter, out var s) ? s : null;
        var selectedId = SelectedTrade?.Id;

        var trades = await _api.GetTradesAsync(status, CounterpartyFilter);
        Summary = await _api.GetSummaryAsync();

        Trades.Clear();
        foreach (var t in trades) Trades.Add(t);
        SelectedTrade = Trades.FirstOrDefault(t => t.Id == selectedId);
        return $"{Trades.Count} trade(s) loaded at {DateTime.Now:HH:mm:ss}";
    });

    private async Task BookAsync() => await RunAsync(async () =>
    {
        var start = DateOnly.FromDateTime(StartDate);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var trade = await _api.BookAsync(new BookTradeRequest(
            Counterparty, Side, Principal, RatePercent, start < today ? start : today, start, start.AddDays(TenorDays)));

        Counterparty = string.Empty;
        await ReloadAfterChangeAsync(trade.Id);
        return $"Booked {trade.TradeRef}: {trade.Side} {trade.Principal:N0} to {trade.Counterparty} at {trade.RatePercent}%";
    });

    private async Task TransitionAsync(string action) => await RunAsync(async () =>
    {
        var trade = await _api.TransitionAsync(SelectedTrade!.Id, action);
        await ReloadAfterChangeAsync(trade.Id);
        return $"{trade.TradeRef} is now {trade.Status}";
    });

    private async Task ReloadAfterChangeAsync(int id)
    {
        var status = Enum.TryParse<TradeStatus>(StatusFilter, out var s) ? s : (TradeStatus?)null;
        var trades = await _api.GetTradesAsync(status, CounterpartyFilter);
        Summary = await _api.GetSummaryAsync();
        Trades.Clear();
        foreach (var t in trades) Trades.Add(t);
        SelectedTrade = Trades.FirstOrDefault(t => t.Id == id);
    }

    private async Task RunAsync(Func<Task<string>> action)
    {
        try
        {
            StatusMessage = await action();
            HasError = false;
        }
        catch (ApiException ex) { Fail(ex.Message); }
        catch (HttpRequestException) { Fail("Cannot reach the TradeDesk API. Start it with: dotnet run --project src/TradeDesk.Api"); }
    }

    private void Fail(string message)
    {
        StatusMessage = message;
        HasError = true;
    }
}
