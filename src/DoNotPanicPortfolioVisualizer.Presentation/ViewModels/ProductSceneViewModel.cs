// ============================================================================
// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Proprietary rights reserved except as expressly licensed herein.
//
// DO NOT PANIC PORTFOLIO VISUALIZER
// This file is governed by the SANYALnet Labs Non-Commercial License in the
// root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
// for AI/ML model training are prohibited unless separately authorized.
//
// Attribution is required: "Based on original work by Supratim Sanyal of
// SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
// patent, trademark, and governing-law provisions.
// ============================================================================
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Data.Interfaces;
using DoNotPanicPortfolioVisualizer.Data.Providers;
using DoNotPanicPortfolioVisualizer.Data.Runtime;
using DoNotPanicPortfolioVisualizer.Data.Services;
using DoNotPanicPortfolioVisualizer.Media.Services;
using DoNotPanicPortfolioVisualizer.Presentation.Services;
using DoNotPanicPortfolioVisualizer.Render.Services;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;
using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.Presentation.ViewModels;

public sealed partial class ProductSceneViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly (string Label, string Symbol)[] MacroSymbols =
    [
        ("VIX", "^VIX"),
        ("NASDAQ", "^IXIC"),
        ("US10Y", "^TNX"),
        ("GOLD", "GC=F"),
        ("CRUDE", "CL=F"),
        ("DXY", "DX-Y.NYB"),
        ("BTC", "BTC-USD")
    ];

    private static readonly (string Key, string City, string Exchange, string Symbol, string TimeZone, double Latitude, double Longitude)[] WorldMarkets =
    [
        ("NewYork", "New York", "NASDAQ", "^IXIC", "America/New_York", 40.7128, -74.0060),
        ("London", "London", "FTSE 100", "^FTSE", "Europe/London", 51.5072, -0.1276),
        ("Paris", "Paris", "EURO STOXX", "^STOXX50E", "Europe/Paris", 48.8566, 2.3522),
        ("Tokyo", "Tokyo", "NIKKEI 225", "^N225", "Asia/Tokyo", 35.6762, 139.6503),
        ("HongKong", "Hong Kong", "HANG SENG", "^HSI", "Asia/Hong_Kong", 22.3193, 114.1694),
        ("Mumbai", "Mumbai", "NIFTY 50", "^NSEI", "Asia/Kolkata", 19.0760, 72.8777),
        ("Sydney", "Sydney", "ASX 200", "^AXJO", "Australia/Sydney", -33.8688, 151.2093),
        ("SaoPaulo", "Sao Paulo", "IBOVESPA", "^BVSP", "America/Sao_Paulo", -23.5505, -46.6333)
    ];

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SettingsFileService _settingsService;
    private readonly IQuoteProvider _quoteProvider;
    private readonly HybridHistoricalDataProvider _historicalProvider;
    private readonly YFinanceProtocolRuntimeClient _protocolClient;
    private readonly YFinanceServerProcessManager _serverManager;
    private readonly FinanceNewsService _newsService = new();
    private readonly WorldWeatherService _weatherService = new();
    private readonly BackgroundImageService _backgroundService = new();
    private readonly Random _random = new(1979);
    private readonly SynchronizationContext _uiContext;
    private AppSettings _settings = new();
    private IReadOnlyList<string> _backgrounds = [];
    private int _backgroundIndex;
    private DateTimeOffset _nextBackgroundChangeUtc;
    private Task? _refreshLoop;
    private Task? _ambientLoop;

    [ObservableProperty]
    private string _marketStatusText = "Market: New York -- loading";

    [ObservableProperty]
    private string _lastUpdatedText = "Last Updated: waiting for live quote";

    [ObservableProperty]
    private string _dataFreshnessText = "LOADING - initializing local market service";

    [ObservableProperty]
    private string _freshnessBrush = "#D4DEE5";

    [ObservableProperty]
    private string _clockDateText = DateTimeOffset.Now.ToString("ddd dd-MMM-yyyy").ToUpperInvariant();

    [ObservableProperty]
    private string _clockText = DateTimeOffset.UtcNow.ToString("HH:mm:ss 'UTC'");

    [ObservableProperty]
    private string _currentBackgroundSource = "/Assets/ExchangeBackgrounds/new-york-stock-exchange.jpg";

    [ObservableProperty]
    private string _newsText = "Loading France 24 business headlines...";

    [ObservableProperty]
    private double _newsOffset;

    private ProductSceneViewModel(
        SettingsFileService settingsService,
        IQuoteProvider quoteProvider,
        HybridHistoricalDataProvider historicalProvider,
        YFinanceProtocolRuntimeClient protocolClient,
        YFinanceServerProcessManager serverManager,
        SynchronizationContext uiContext)
    {
        _settingsService = settingsService;
        _quoteProvider = quoteProvider;
        _historicalProvider = historicalProvider;
        _protocolClient = protocolClient;
        _serverManager = serverManager;
        _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
        Lanes = [];
        Graphs = [];
        GlobalMarkets = new ObservableCollection<GlobalMarketViewModel>(WorldMarkets.Select(static market => new GlobalMarketViewModel
        {
            Key = market.Key,
            City = market.City,
            ExchangeName = market.Exchange,
            Symbol = market.Symbol,
            TimeZoneId = market.TimeZone,
            Latitude = market.Latitude,
            Longitude = market.Longitude
        }));
        MacroQuotes = new ObservableCollection<MacroQuoteViewModel>(
            MacroSymbols.Select(static item => new MacroQuoteViewModel(item.Label, item.Symbol)));
        LoadSettings();
    }

    public ObservableCollection<TickerLaneViewModel> Lanes { get; }
    public ObservableCollection<MacroQuoteViewModel> MacroQuotes { get; }
    public ObservableCollection<FloatingGraphViewModel> Graphs { get; }
    public ObservableCollection<GlobalMarketViewModel> GlobalMarkets { get; }

    public static ProductSceneViewModel CreateDefault()
    {
        YFinanceServerProcessManager manager = new();
        YFinanceProtocolRuntimeClient protocolClient = new();
        ManagedYFinanceRuntimeClient runtimeClient = new(manager, protocolClient, "DNPPV-2.0-Scene");
        YahooFinanceQuoteProvider quoteProvider = new(runtimeClient, throwOnPartial: false);
        HistoricalCacheService historicalCache = new();
        HybridHistoricalDataProvider historicalProvider = new(historicalCache, runtimeClient, disposeCache: true);
        SynchronizationContext uiContext = SynchronizationContext.Current
            ?? throw new InvalidOperationException("The product scene must be created on the Avalonia UI thread.");
        return new ProductSceneViewModel(
            new SettingsFileService(),
            quoteProvider,
            historicalProvider,
            protocolClient,
            manager,
            uiContext);
    }

    public Task InitializeAsync()
    {
        _refreshLoop ??= RunRefreshLoopAsync(_lifetimeCts.Token);
        _ambientLoop ??= RunAmbientLoopAsync(_lifetimeCts.Token);
        return Task.CompletedTask;
    }

    private void LoadSettings()
    {
        _settings = _settingsService.Load();
        Lanes.Clear();
        foreach (TickerGroup group in _settings.Groups.Where(static group => group.Enabled).Take(4))
            Lanes.Add(new TickerLaneViewModel(group));

        string? selectedFolder = _settings.UseCustomBackgroundImageFolder
            ? _settings.CustomBackgroundImageFolder
            : _settings.BackgroundImageFolder;
        _backgrounds = _backgroundService.GetImages(selectedFolder, _settings.BackgroundIncludeSubfolders);
        _nextBackgroundChangeUtc = DateTimeOffset.UtcNow.AddSeconds(_settings.BackgroundChangeSeconds);
    }

    private async Task RunAmbientLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await InvokeOnUiAsync(UpdateClockAndMotion, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Keep clocks and motion alive if one optional background is malformed.
            }
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await RefreshClockAndQuotesAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }
    }

    private async Task RefreshClockAndQuotesAsync(CancellationToken cancellationToken)
    {
        await InvokeOnUiAsync(() =>
        {
            ClockDateText = DateTimeOffset.Now.ToString("ddd dd-MMM-yyyy").ToUpperInvariant();
            ClockText = DateTimeOffset.UtcNow.ToString("HH:mm:ss 'UTC'");
            DataFreshnessText = "LIVE DATA - connecting to local YFinance service";
            FreshnessBrush = "#F4C95D";
        }, cancellationToken);

        List<(string Symbol, Action<QuoteSnapshot> Apply)> targets = [];
        targets.AddRange(MacroQuotes.Select(macro => (macro.Symbol, (Action<QuoteSnapshot>)macro.Apply)));
        targets.AddRange(Lanes.SelectMany(static lane => lane.Quotes)
            .Select(ticker => (ticker.Symbol, (Action<QuoteSnapshot>)ticker.Apply)));

        int successCount = 0;
        foreach ((string symbol, Action<QuoteSnapshot> apply) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<QuoteSnapshot> quotes = await _quoteProvider.GetQuotesAsync([symbol], cancellationToken);
                QuoteSnapshot? quote = quotes.FirstOrDefault();
                if (quote is null)
                    continue;

                await InvokeOnUiAsync(() =>
                {
                    apply(quote);
                    LastUpdatedText = "Last Updated: " + TickerFormatter.FormatUpdatedSymbol(quote);
                    MarketStatusText = "Market: New York " + FormatMarketSession(quote.MarketSession);
                    DataFreshnessText = quote.IsStale ? "DELAYED - cached market data" : "LIVE quote feed";
                    FreshnessBrush = quote.IsStale ? "#F4C95D" : "#39E75F";
                }, cancellationToken);
                successCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await InvokeOnUiAsync(() =>
                {
                    DataFreshnessText = successCount == 0
                        ? "OFFLINE - waiting for local market service"
                        : $"DEGRADED - {successCount} symbols updated";
                    FreshnessBrush = "#FF8A55";
                }, cancellationToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        if (successCount == 0)
            await InvokeOnUiAsync(() => MarketStatusText = "Market: New York -- unavailable", cancellationToken);

        await RefreshGlobalMarketsAsync(cancellationToken);
        await RefreshGraphsAsync(cancellationToken);
        await RefreshNewsAsync(cancellationToken);
    }

    private async Task RefreshGlobalMarketsAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<QuoteSnapshot> quotes = await _quoteProvider.GetQuotesAsync(
                GlobalMarkets.Select(static market => market.Symbol),
                cancellationToken);
            await InvokeOnUiAsync(() =>
            {
                foreach (GlobalMarketViewModel market in GlobalMarkets)
                {
                    QuoteSnapshot? quote = quotes.FirstOrDefault(candidate =>
                        string.Equals(candidate.Symbol, market.Symbol, StringComparison.OrdinalIgnoreCase));
                    if (quote is not null)
                        market.ApplyQuote(quote);
                }
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }

        Task<(GlobalMarketViewModel Market, string Text)>[] weatherTasks = GlobalMarkets.Select(async market =>
        {
            try
            {
                string text = await _weatherService.GetWeatherAsync(market, cancellationToken);
                return (market, text);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (market, "weather --");
            }
        }).ToArray();
        (GlobalMarketViewModel Market, string Text)[] weather = await Task.WhenAll(weatherTasks);
        await InvokeOnUiAsync(() =>
        {
            foreach ((GlobalMarketViewModel market, string text) in weather)
                market.WeatherText = text;
        }, cancellationToken);
    }

    private async Task RefreshGraphsAsync(CancellationToken cancellationToken)
    {
        if (!_settings.EnableFloatingGraphs)
            return;

        List<(string TapeName, TickerQuoteViewModel Quote)> movers = Lanes
            .SelectMany(lane => lane.Quotes.Select(quote => (lane.Title, Quote: quote)))
            .Where(static item => item.Quote.ChangePercent.HasValue)
            .OrderByDescending(static item => Math.Abs(item.Quote.ChangePercent!.Value))
            .Take(Math.Min(16, Math.Max(1, _settings.MaxFloatingGraphsPerTape * Math.Max(1, Lanes.Count))))
            .ToList();
        if (movers.Count == 0)
            return;

        IReadOnlyList<TickerHistorySnapshot> histories = await _historicalProvider.GetHistoryAsync(
            movers.Select(static item => item.Quote.Symbol),
            _settings.HistoricalLookbackDays,
            cancellationToken);
        HistoricalGraphBuilder builder = new();
        List<FloatingGraphViewModel> resolvedGraphs = [];
        for (int index = 0; index < movers.Count; index++)
        {
            (string tapeName, TickerQuoteViewModel quote) = movers[index];
            TickerHistorySnapshot? history = histories.FirstOrDefault(candidate =>
                string.Equals(candidate.Symbol, quote.Symbol, StringComparison.OrdinalIgnoreCase));
            if (history is not null)
                resolvedGraphs.Add(builder.Build(tapeName, history, quote.ChangePercent, index));
        }

        await InvokeOnUiAsync(() =>
        {
            Graphs.Clear();
            foreach (FloatingGraphViewModel graph in resolvedGraphs)
                Graphs.Add(graph);
        }, cancellationToken);
    }

    private async Task RefreshNewsAsync(CancellationToken cancellationToken)
    {
        try
        {
            string text = await _newsService.GetNewsTextAsync(_settings, cancellationToken);
            await InvokeOnUiAsync(() => NewsText = text, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await InvokeOnUiAsync(
                () => NewsText = "France 24 business headlines are temporarily unavailable",
                cancellationToken);
        }
    }

    private Task InvokeOnUiAsync(Action action, CancellationToken cancellationToken)
    {
        if (ReferenceEquals(SynchronizationContext.Current, _uiContext))
        {
            action();
            return Task.CompletedTask;
        }

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                action();
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }, null);
        return completion.Task;
    }

    private void UpdateClockAndMotion()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ClockDateText = now.ToLocalTime().ToString("ddd dd-MMM-yyyy").ToUpperInvariant();
        ClockText = now.ToString("HH:mm:ss 'UTC'");
        foreach (GlobalMarketViewModel market in GlobalMarkets)
        {
            try
            {
                TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(market.TimeZoneId);
                market.TimeText = TimeZoneInfo.ConvertTime(now, zone).ToString("HH:mm");
            }
            catch (TimeZoneNotFoundException)
            {
                market.TimeText = "--:--";
            }
        }

        foreach (FloatingGraphViewModel graph in Graphs)
        {
            double nextX = graph.X + graph.VelocityX;
            double nextY = graph.Y + graph.VelocityY;
            if (nextX < graph.AnchorX - 3 || nextX > graph.AnchorX + 3)
            {
                graph.VelocityX = -graph.VelocityX;
                nextX = graph.X + graph.VelocityX;
            }
            if (nextY < graph.AnchorY - 1 || nextY > graph.AnchorY + 1)
            {
                graph.VelocityY = -graph.VelocityY;
                nextY = graph.Y + graph.VelocityY;
            }
            graph.X = Math.Clamp(nextX, graph.AnchorX - 3, graph.AnchorX + 3);
            graph.Y = Math.Clamp(nextY, graph.AnchorY - 1, graph.AnchorY + 1);
        }

        NewsOffset = NewsOffset < -1800 ? 1000 : NewsOffset - 22;

        if (_backgrounds.Count > 0 && now >= _nextBackgroundChangeUtc)
        {
            _backgroundIndex = _settings.ShuffleBackgrounds
                ? _random.Next(_backgrounds.Count)
                : (_backgroundIndex + 1) % _backgrounds.Count;
            CurrentBackgroundSource = _backgrounds[_backgroundIndex];
            _nextBackgroundChangeUtc = now.AddSeconds(_settings.BackgroundChangeSeconds);
        }
    }

    private static string FormatMarketSession(MarketSession session)
        => session switch
        {
            MarketSession.PreMarket => "Pre-Market",
            MarketSession.Regular => "Open",
            MarketSession.AfterHours => "After Hours",
            MarketSession.Closed => "Closed",
            _ => "Status Unknown"
        };

    public async ValueTask DisposeAsync()
    {
        await _lifetimeCts.CancelAsync();
        if (_refreshLoop is not null)
        {
            try
            {
                await _refreshLoop;
            }
            catch (Exception)
            {
            }
        }

        if (_ambientLoop is not null)
        {
            try
            {
                await _ambientLoop;
            }
            catch (Exception)
            {
            }
        }

        await _serverManager.StopOwnedServerAsync();
        await _protocolClient.DisposeAsync();
        _newsService.Dispose();
        _weatherService.Dispose();
        _historicalProvider.Dispose();
        if (_quoteProvider is IDisposable disposableQuoteProvider)
            disposableQuoteProvider.Dispose();
        _serverManager.Dispose();
        _lifetimeCts.Dispose();
    }
}
