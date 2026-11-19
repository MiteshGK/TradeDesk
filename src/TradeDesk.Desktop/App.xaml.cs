using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Markup;
using TradeDesk.Desktop.Services;
using TradeDesk.Desktop.ViewModels;

namespace TradeDesk.Desktop;

public partial class App : Application
{
    private HttpClient? _http;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Indian digit grouping (1,00,00,000) in bindings, matching the web client.
        var culture = CultureInfo.GetCultureInfo("en-IN");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
        FrameworkContentElement.LanguageProperty.OverrideMetadata(typeof(System.Windows.Documents.TextElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        // Composition root: the API address can be overridden with TRADEDESK_API.
        var baseUrl = Environment.GetEnvironmentVariable("TRADEDESK_API") ?? "http://localhost:5152/";
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };

        var viewModel = new MainViewModel(new TradeApiClient(_http));
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        viewModel.RefreshCommand.Execute(null);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _http?.Dispose();
        base.OnExit(e);
    }
}
