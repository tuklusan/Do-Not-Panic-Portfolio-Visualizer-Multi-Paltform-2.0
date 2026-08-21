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
using System.Diagnostics;
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
    private readonly SynchronizationContext _uiContext;
    private AppSettings _settings = new();
    private IReadOnlyList<string> _backgrounds = [];
    private BackgroundCinemaController? _backgroundCinema;
    private FloatingGraphMotionController? _graphMotion;
    private readonly GlobalMarketsMotionController _globalMarketsMotion = new();
    private readonly NewsPlaybackController _newsPlayback = new();
    private DateTimeOffset _nextBackgroundChangeUtc;
    private DateTimeOffset _nextWeatherRefreshUtc = DateTimeOffset.MinValue;
    private double _graphViewportWidth = 1280d;
    private double _graphViewportHeight = 720d;
    private double _globalMarketsViewportWidth = 900d;
    private Task? _portfolioQuoteLoop;
    private Task? _macroQuoteLoop;
    private Task? _worldMarketsLoop;
    private Task? _graphRefreshLoop;
    private Task? _newsRefreshLoop;
    private Task? _ambientLoop;
    private readonly bool _graphImpulseFixtureEnabled =
        string.Equals(Environment.GetEnvironmentVariable("DNPPV_GRAPH_IMPULSE_FIXTURE"), "1", StringComparison.Ordinal);
    private readonly string? _graphImpulseTracePath =
        Environment.GetEnvironmentVariable("DNPPV_GRAPH_IMPULSE_TRACE");
    private readonly string? _cinematicTracePath =
        Environment.GetEnvironmentVariable("DNPPV_CINEMATIC_TRACE");
    private readonly HashSet<string> _fixtureActiveSymbols = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _nextGraphFixtureImpulseUtc;
    private DateTimeOffset _nextCinematicTraceUtc = DateTimeOffset.MinValue;
    private NewsPlaybackPhase _lastTracedNewsPhase = NewsPlaybackPhase.Idle;
    private volatile bool _cinematicPlaybackActive = true;

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
    private string _backgroundSourceA = "/Assets/ExchangeBackgrounds/new-york-stock-exchange.jpg";

    [ObservableProperty]
    private string? _backgroundSourceB;

    [ObservableProperty]
    private double _backgroundOpacityA = 0.45d;

    [ObservableProperty]
    private double _backgroundOpacityB;

    [ObservableProperty]
    private double _backgroundScaleA = 1.01d;

    [ObservableProperty]
    private double _backgroundScaleB = 1.01d;

    [ObservableProperty]
    private double _sceneDimOpacity = 0.55d;

    [ObservableProperty]
    private string _newsText = "Loading France 24 business headlines...";

    [ObservableProperty]
    private double _newsVerticalOffset;

    [ObservableProperty]
    private string _newsPhaseText = NewsPlaybackPhase.Idle.ToString();

    [ObservableProperty]
    private double _globalMarketsTrackOffset;

    [ObservableProperty]
    private double _globalMarketsTrackWidth;

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
        PinnedGlobalMarket = GlobalMarkets[0];
        GlobalMarketTrackItems = [];
        ConfigureGlobalMarketViewport(_globalMarketsViewportWidth);
        _newsPlayback.SetHeadlines(["Loading France 24 business headlines..."]);
        LoadSettings();
    }

    public ObservableCollection<TickerLaneViewModel> Lanes { get; }
    public ObservableCollection<MacroQuoteViewModel> MacroQuotes { get; }
    public ObservableCollection<FloatingGraphViewModel> Graphs { get; }
    public ObservableCollection<GlobalMarketViewModel> GlobalMarkets { get; }
    public GlobalMarketViewModel PinnedGlobalMarket { get; }
    public ObservableCollection<GlobalMarketViewModel> GlobalMarketTrackItems { get; }

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
        _portfolioQuoteLoop ??= RunPortfolioQuoteLoopAsync(_lifetimeCts.Token);
        _macroQuoteLoop ??= RunMacroQuoteLoopAsync(_lifetimeCts.Token);
        _worldMarketsLoop ??= RunWorldMarketsLoopAsync(_lifetimeCts.Token);
        _graphRefreshLoop ??= RunGraphRefreshLoopAsync(_lifetimeCts.Token);
        _newsRefreshLoop ??= RunNewsRefreshLoopAsync(_lifetimeCts.Token);
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
        _backgrounds = new[]
            {
                "/Assets/ExchangeBackgrounds/new-york-stock-exchange.jpg",
                "/Assets/ExchangeBackgrounds/london-skyline-public-domain.jpg",
                "/Assets/ExchangeBackgrounds/shanghai-skyline-public-domain.jpg"
            }
            .Concat(_backgrounds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _backgroundCinema = new BackgroundCinemaController(_backgrounds, _settings.ShuffleBackgrounds);
        _graphMotion = new FloatingGraphMotionController(
            _settings.FloatingGraphVelocityMin,
            _settings.FloatingGraphVelocityMax,
            _settings.EnableBouncingGraphCards);
        _graphMotion.ConfigureViewport(_graphViewportWidth, _graphViewportHeight, Graphs);
        SceneDimOpacity = Math.Clamp(_settings.DimOpacity, 0d, 1d);
        ApplyBackgroundCinemaState();
        _nextBackgroundChangeUtc = DateTimeOffset.UtcNow.AddSeconds(_settings.BackgroundChangeSeconds);
    }

    private async Task RunAmbientLoopAsync(CancellationToken cancellationToken)
    {
        Stopwatch clock = Stopwatch.StartNew();
        TimeSpan prior = clock.Elapsed;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_cinematicPlaybackActive)
            {
                prior = clock.Elapsed;
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                continue;
            }

            TimeSpan current = clock.Elapsed;
            TimeSpan elapsed = current - prior;
            prior = current;
            try
            {
                await InvokeOnUiAsync(() => UpdateClockAndMotion(elapsed), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Keep clocks and motion alive if one optional background is malformed.
            }
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    private async Task RunPortfolioQuoteLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            await RefreshPortfolioQuotesAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private async Task RunMacroQuoteLoopAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            await RefreshMacroQuotesAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private async Task RunWorldMarketsLoopAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            await RefreshGlobalMarketsAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    private async Task RunGraphRefreshLoopAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(12), cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            try
            {
                await RefreshGraphsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // History failure degrades the graph lane without terminating its scheduler.
            }
            await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
        }
    }

    private async Task RunNewsRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            await RefreshNewsAsync(cancellationToken);
            int refreshMinutes = Math.Clamp(_settings.NewsRefreshMinutes, 30, 240);
            await Task.Delay(TimeSpan.FromMinutes(refreshMinutes), cancellationToken);
        }
    }

    private async Task WaitUntilCinematicPlaybackActiveAsync(CancellationToken cancellationToken)
    {
        while (!_cinematicPlaybackActive)
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
    }

    private async Task RefreshPortfolioQuotesAsync(CancellationToken cancellationToken)
    {
        await InvokeOnUiAsync(() =>
        {
            ClockDateText = DateTimeOffset.Now.ToString("ddd dd-MMM-yyyy").ToUpperInvariant();
            ClockText = DateTimeOffset.UtcNow.ToString("HH:mm:ss 'UTC'");
            DataFreshnessText = "LIVE DATA - connecting to local YFinance service";
            FreshnessBrush = "#F4C95D";
        }, cancellationToken);

        List<(string Symbol, Action<QuoteSnapshot> Apply)> targets = Lanes.SelectMany(static lane => lane.Quotes)
            .Select(ticker => (ticker.Symbol, (Action<QuoteSnapshot>)ticker.Apply))
            .ToList();

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
                    ApplyQuoteToGraph(quote);
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
    }

    private async Task RefreshMacroQuotesAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<QuoteSnapshot> quotes = await _quoteProvider.GetQuotesAsync(
                MacroQuotes.Select(static macro => macro.Symbol),
                cancellationToken);
            await InvokeOnUiAsync(() =>
            {
                foreach (MacroQuoteViewModel macro in MacroQuotes)
                {
                    QuoteSnapshot? quote = quotes.FirstOrDefault(candidate =>
                        string.Equals(candidate.Symbol, macro.Symbol, StringComparison.OrdinalIgnoreCase));
                    if (quote is not null)
                        macro.Apply(quote);
                }
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The macro lane degrades independently from portfolio quote playback.
        }
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
            // World-market quote failure does not stop clocks, weather, or other scene lanes.
        }

        if (DateTimeOffset.UtcNow < _nextWeatherRefreshUtc)
            return;

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
            _nextWeatherRefreshUtc = DateTimeOffset.UtcNow.AddMinutes(10);
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
        List<(FloatingGraphViewModel Graph, decimal? Last, decimal? ChangePercent)> resolvedGraphs = [];
        for (int index = 0; index < movers.Count; index++)
        {
            (string tapeName, TickerQuoteViewModel quote) = movers[index];
            TickerHistorySnapshot? history = histories.FirstOrDefault(candidate =>
                string.Equals(candidate.Symbol, quote.Symbol, StringComparison.OrdinalIgnoreCase));
            if (history is not null)
            {
                resolvedGraphs.Add((
                    builder.Build(tapeName, history, quote.ChangePercent, index),
                    quote.Last,
                    quote.ChangePercent));
            }
        }

        await InvokeOnUiAsync(() =>
        {
            HashSet<string> selectedKeys = resolvedGraphs
                .Select(static item => GetGraphKey(item.Graph))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (int index = Graphs.Count - 1; index >= 0; index--)
            {
                if (!selectedKeys.Contains(GetGraphKey(Graphs[index])))
                    Graphs.RemoveAt(index);
            }

            foreach ((FloatingGraphViewModel graph, decimal? last, decimal? changePercent) in resolvedGraphs)
            {
                FloatingGraphViewModel? existing = Graphs.FirstOrDefault(candidate =>
                    string.Equals(GetGraphKey(candidate), GetGraphKey(graph), StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    existing.CopyContentFrom(graph);
                    _graphMotion?.ApplyQuote(existing, last, changePercent, suppressMotionCue: false);
                    continue;
                }

                _graphMotion?.ApplyQuote(graph, last, changePercent, suppressMotionCue: true);
                Graphs.Add(graph);
            }

            _graphMotion?.ConfigureViewport(_graphViewportWidth, _graphViewportHeight, Graphs);
            ArmGraphImpulseFixture();
        }, cancellationToken);
    }

    private async Task RefreshNewsAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<string> headlines = await _newsService.GetPlaybackHeadlinesAsync(_settings, cancellationToken);
            await InvokeOnUiAsync(() => _newsPlayback.SetHeadlines(headlines), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await InvokeOnUiAsync(
                () => _newsPlayback.SetHeadlines(["France 24 business headlines are temporarily unavailable"]),
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

    private void UpdateClockAndMotion(TimeSpan elapsed)
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

        foreach (TickerLaneViewModel lane in Lanes)
            lane.Step(elapsed);

        TriggerGraphImpulseFixture(now);
        _graphMotion?.Step(Graphs, elapsed);
        TraceCompletedGraphFixtureImpulses();

        _globalMarketsMotion.Step(elapsed);
        GlobalMarketsTrackOffset = _globalMarketsMotion.Offset;

        _newsPlayback.Step(elapsed);
        NewsText = _newsPlayback.DisplayText;
        NewsVerticalOffset = _newsPlayback.VerticalOffset;
        NewsPhaseText = _newsPlayback.Phase.ToString();
        TraceCinematicPlayback(now);

        _backgroundCinema?.Step(elapsed);
        if (_backgroundCinema is not null && now >= _nextBackgroundChangeUtc)
        {
            _backgroundCinema.BeginRotation();
            _nextBackgroundChangeUtc = now.AddSeconds(_settings.BackgroundChangeSeconds);
        }
        ApplyBackgroundCinemaState();
    }

    public void ConfigureGraphViewport(double width, double height)
    {
        _graphViewportWidth = Math.Max(1d, width);
        _graphViewportHeight = Math.Max(1d, height);
        _graphMotion?.ConfigureViewport(_graphViewportWidth, _graphViewportHeight, Graphs);
    }

    public void PauseCinematicPlayback() => _cinematicPlaybackActive = false;

    public void ResumeCinematicPlayback() => _cinematicPlaybackActive = true;

    public void ConfigureCinematicViewport(double width)
    {
        ConfigureGlobalMarketViewport(Math.Max(1d, width - 356d));
        _newsPlayback.ConfigureViewport(Math.Max(1d, width - 220d));
    }

    private void ConfigureGlobalMarketViewport(double width)
    {
        _globalMarketsViewportWidth = Math.Max(1d, width);
        int scrollingMarketCount = Math.Max(0, GlobalMarkets.Count - 1);
        if (_globalMarketsMotion.Configure(_globalMarketsViewportWidth, scrollingMarketCount))
        {
            GlobalMarketTrackItems.Clear();
            IReadOnlyList<GlobalMarketViewModel> scrollingMarkets = GlobalMarkets.Skip(1).ToList();
            for (int copy = 0; copy < _globalMarketsMotion.RequiredCopies; copy++)
            {
                foreach (GlobalMarketViewModel market in scrollingMarkets)
                    GlobalMarketTrackItems.Add(market);
            }
        }

        GlobalMarketsTrackWidth = _globalMarketsMotion.TrackWidth;
        GlobalMarketsTrackOffset = _globalMarketsMotion.Offset;
    }

    private void ApplyQuoteToGraph(QuoteSnapshot quote)
    {
        FloatingGraphViewModel? graph = Graphs.FirstOrDefault(candidate =>
            string.Equals(candidate.Symbol, quote.Symbol, StringComparison.OrdinalIgnoreCase));
        if (graph is not null)
            _graphMotion?.ApplyQuote(graph, quote.Last ?? quote.PreviousClose, quote.ChangePercent);
    }

    private static string GetGraphKey(FloatingGraphViewModel graph)
        => graph.TapeName + "\u001f" + graph.Symbol;

    private void ArmGraphImpulseFixture()
    {
        if (!_graphImpulseFixtureEnabled || Graphs.Count < 2 || _nextGraphFixtureImpulseUtc.HasValue)
            return;

        _nextGraphFixtureImpulseUtc = DateTimeOffset.UtcNow.AddSeconds(2);
        WriteGraphFixtureTrace($"READY;GRAPH_COUNT={Graphs.Count}");
    }

    private void TriggerGraphImpulseFixture(DateTimeOffset now)
    {
        if (!_graphImpulseFixtureEnabled || Graphs.Count < 2 ||
            !_nextGraphFixtureImpulseUtc.HasValue || now < _nextGraphFixtureImpulseUtc.Value)
        {
            return;
        }

        FloatingGraphViewModel rising = Graphs[0];
        FloatingGraphViewModel falling = Graphs[1];
        if (rising.IsRefreshTravelFlashActive || falling.IsRefreshTravelFlashActive)
            return;

        decimal risingStart = rising.RawLastValue ?? 100m;
        decimal fallingStart = falling.RawLastValue ?? 100m;
        _graphMotion?.ApplyQuote(rising, risingStart + 1m, 1m, suppressMotionCue: rising.RawLastValue is null);
        _graphMotion?.ApplyQuote(falling, fallingStart - 1m, -1m, suppressMotionCue: falling.RawLastValue is null);
        if (!rising.IsRefreshTravelFlashActive || !falling.IsRefreshTravelFlashActive)
        {
            _graphMotion?.ApplyQuote(rising, risingStart + 2m, 1m);
            _graphMotion?.ApplyQuote(falling, fallingStart - 2m, -1m);
        }

        TraceFixtureImpulseStart(rising, "UP");
        TraceFixtureImpulseStart(falling, "DOWN");
        _fixtureActiveSymbols.Add(rising.Symbol);
        _fixtureActiveSymbols.Add(falling.Symbol);
        _nextGraphFixtureImpulseUtc = now.AddSeconds(6);
    }

    private void TraceCompletedGraphFixtureImpulses()
    {
        if (_fixtureActiveSymbols.Count == 0)
            return;

        foreach (FloatingGraphViewModel graph in Graphs.Where(graph =>
                     _fixtureActiveSymbols.Contains(graph.Symbol) && !graph.IsRefreshTravelFlashActive).ToArray())
        {
            WriteGraphFixtureTrace(
                $"COMPLETE;SYMBOL={graph.Symbol};Y={graph.Y:0.00};VX={graph.VelocityX:0.00};VY={graph.VelocityY:0.00}");
            _fixtureActiveSymbols.Remove(graph.Symbol);
        }
    }

    private void TraceFixtureImpulseStart(FloatingGraphViewModel graph, string direction)
        => WriteGraphFixtureTrace(
            $"START;SYMBOL={graph.Symbol};DIRECTION={direction};Y={graph.Y:0.00};TARGET_Y={graph.RefreshTravelTargetY:0.00};MIN_VELOCITY={FloatingGraphMotionController.RefreshTravelMinimumVelocity:0}");

    private void WriteGraphFixtureTrace(string message)
    {
        if (string.IsNullOrWhiteSpace(_graphImpulseTracePath))
            return;

        try
        {
            File.AppendAllText(
                _graphImpulseTracePath,
                $"{DateTimeOffset.UtcNow:O};{message}{Environment.NewLine}");
        }
        catch
        {
            // Acceptance tracing cannot interfere with the product scene.
        }
    }

    private void TraceCinematicPlayback(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(_cinematicTracePath))
            return;

        bool phaseChanged = _newsPlayback.Phase != _lastTracedNewsPhase;
        if (!phaseChanged && now < _nextCinematicTraceUtc)
            return;

        _lastTracedNewsPhase = _newsPlayback.Phase;
        _nextCinematicTraceUtc = now.AddSeconds(1);
        WriteCinematicTrace(
            $"NEWS;PHASE={_newsPlayback.Phase};HEADLINE={_newsPlayback.HeadlineIndex};SEGMENT={_newsPlayback.SegmentIndex};Y={_newsPlayback.VerticalOffset:0.00};TEXT_LENGTH={_newsPlayback.DisplayText.Length}");
        WriteCinematicTrace(
            $"MARKETS;X={_globalMarketsMotion.Offset:0.00};SEQUENCE_WIDTH={_globalMarketsMotion.SequenceWidth:0.00};COPIES={_globalMarketsMotion.RequiredCopies}");
    }

    private void WriteCinematicTrace(string message)
    {
        try
        {
            File.AppendAllText(
                _cinematicTracePath!,
                $"{DateTimeOffset.UtcNow:O};{message}{Environment.NewLine}");
        }
        catch
        {
            // Acceptance tracing cannot interfere with the product scene.
        }
    }

    private void ApplyBackgroundCinemaState()
    {
        if (_backgroundCinema is null)
            return;

        BackgroundSourceA = _backgroundCinema.SourceA;
        BackgroundSourceB = _backgroundCinema.SourceB;
        BackgroundOpacityA = _backgroundCinema.OpacityA;
        BackgroundOpacityB = _backgroundCinema.OpacityB;
        BackgroundScaleA = _backgroundCinema.ScaleA;
        BackgroundScaleB = _backgroundCinema.ScaleB;
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
        Task?[] loops =
        [
            _portfolioQuoteLoop,
            _macroQuoteLoop,
            _worldMarketsLoop,
            _graphRefreshLoop,
            _newsRefreshLoop,
            _ambientLoop
        ];
        foreach (Task loop in loops.Where(static loop => loop is not null).Cast<Task>())
        {
            try
            {
                await loop;
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
