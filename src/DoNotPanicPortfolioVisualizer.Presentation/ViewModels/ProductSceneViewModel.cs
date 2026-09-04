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
using DoNotPanicPortfolioVisualizer.Core.Services;
using DoNotPanicPortfolioVisualizer.Data.Interfaces;
using DoNotPanicPortfolioVisualizer.Data.Providers;
using DoNotPanicPortfolioVisualizer.Data.Runtime;
using DoNotPanicPortfolioVisualizer.Data.Services;
using DoNotPanicPortfolioVisualizer.Media.Services;
using DoNotPanicPortfolioVisualizer.Presentation.Services;
using DoNotPanicPortfolioVisualizer.Render.Services;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;
using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.Presentation.ViewModels;

public sealed partial class ProductSceneViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly (string Label, string Symbol, decimal Maximum, bool InvertRiskColors)[] MacroSymbols =
    [
        ("VIX", "^VIX", 60m, true),
        ("NASDAQ", "^IXIC", 25000m, false),
        ("UST10Y", "^TNX", 6m, false),
        ("UST3M", "^IRX", 6m, false),
        ("GOLD", "GC=F", 4000m, false),
        ("CRUDE", "BZ=F", 160m, false),
        ("DXY", "DX-Y.NYB", 120m, true),
        ("BTC", "BTC-USD", 200000m, false)
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

    private static readonly IReadOnlyDictionary<string, string> BackgroundAttributions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/Assets/ExchangeBackgrounds/new-york-stock-exchange.jpg"] = "Jean-Christophe BENOIST, CC BY 3.0",
            ["/Assets/ExchangeBackgrounds/london-skyline-public-domain.jpg"] = "Rodrigo.Argenton, CC0",
            ["/Assets/ExchangeBackgrounds/shanghai-skyline-public-domain.jpg"] = "Pxfuel, CC0"
        };

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly object _sceneStartupGate = new();
    private readonly SettingsFileService _settingsService;
    private readonly IQuoteProvider _quoteProvider;
    private readonly ProgressiveQuoteRefreshPipeline _portfolioQuotePipeline = new();
    private readonly HybridHistoricalDataProvider _historicalProvider;
    private readonly YFinanceProtocolRuntimeClient _protocolClient;
    private readonly YFinanceServerProcessManager _serverManager;
    private readonly FinanceNewsService _newsService = new();
    private readonly WorldWeatherService _weatherService = new();
    private readonly NtpTimeService _ntpTimeService = new();
    private readonly InternetProbeService _networkProbe = new();
    private readonly BackgroundImageService _backgroundService = new();
    private readonly HistoricalGraphBuildCache _graphBuildCache = new();
    private readonly StagedSceneStartupCoordinator _sceneStartupCoordinator = new();
    private readonly SynchronizationContext _uiContext;
    private AppSettings _settings = new();
    private IReadOnlyList<string> _backgrounds = [];
    private BackgroundCinemaController? _backgroundCinema;
    private FloatingGraphMotionController? _graphMotion;
    private readonly GlobalMarketsMotionController _globalMarketsMotion = new();
    private readonly NewsPlaybackController _newsPlayback = new();
    private readonly RenderSurfaceHeartbeatController _renderHeartbeat = new();
    private readonly object _renderHeartbeatGate = new();
    private DateTimeOffset _nextBackgroundChangeUtc;
    private DateTimeOffset _nextWeatherRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastNtpSyncUtc = DateTimeOffset.MinValue;
    private TimeSpan? _ntpOffset;
    private double _graphViewportWidth = 1280d;
    private double _graphViewportHeight = 720d;
    private double _globalMarketsViewportWidth = 900d;
    private Task? _initialQuoteSequence;
    private Task? _deferredSceneLoops;
    private Task? _tickerMotionLoop;
    private Task? _newsPlaybackLoop;
    private Task? _ambientLoop;
    private Task? _renderHeartbeatLoop;
    private bool _deferredSceneLoopsStarted;
    private bool _sceneDisposalStarted;
    private readonly bool _graphImpulseFixtureEnabled =
        string.Equals(Environment.GetEnvironmentVariable("DNPPV_GRAPH_IMPULSE_FIXTURE"), "1", StringComparison.Ordinal);
    private readonly bool _renderHeartbeatFixtureEnabled =
        string.Equals(Environment.GetEnvironmentVariable("DNPPV_RENDER_HEARTBEAT_FIXTURE"), "1", StringComparison.Ordinal);
    private readonly bool _forceNewsFailure =
        string.Equals(Environment.GetEnvironmentVariable("DNPPV_FORCE_NEWS_FAILURE"), "1", StringComparison.Ordinal);
    private DateTimeOffset _renderHeartbeatFixtureStartedUtc;
    private readonly HashSet<string> _fixtureActiveSymbols = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _nextGraphFixtureImpulseUtc;
    private DateTimeOffset _nextCinematicTraceUtc = DateTimeOffset.MinValue;
    private NewsPlaybackPhase _lastTracedNewsPhase = NewsPlaybackPhase.Idle;
    private readonly object _degradedTraceGate = new();
    private readonly Dictionary<string, DateTimeOffset> _lastDegradedTraceUtc = new(StringComparer.Ordinal);
    private volatile bool _cinematicPlaybackActive = true;
    private volatile int _resolvedGraphCount;
    private readonly TaskCompletionSource _initialQuoteSequenceCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
    private string _newsText = "Loading finance news headlines...";

    [ObservableProperty]
    private double _newsVerticalOffset;

    [ObservableProperty]
    private string _newsPhaseText = NewsPlaybackPhase.Idle.ToString();

    [ObservableProperty]
    private double _globalMarketsTrackOffset;

    [ObservableProperty]
    private double _globalMarketsTrackWidth;

    [ObservableProperty]
    private string _backgroundAttributionText = "© Supratim Sanyal. SANYALnet Labs.";

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
            MacroSymbols.Select(static item => new MacroQuoteViewModel(
                item.Label,
                item.Symbol,
                item.Maximum,
                item.InvertRiskColors)));
        PinnedGlobalMarket = GlobalMarkets[0];
        GlobalMarketTrackItems = [];
        ConfigureGlobalMarketViewport(_globalMarketsViewportWidth);
        _newsPlayback.SetHeadlines(["Loading finance news headlines..."]);
        LoadSettings();
    }

    public ObservableCollection<TickerLaneViewModel> Lanes { get; }
    public ObservableCollection<MacroQuoteViewModel> MacroQuotes { get; }
    public ObservableCollection<FloatingGraphViewModel> Graphs { get; }
    public ObservableCollection<GlobalMarketViewModel> GlobalMarkets { get; }
    public GlobalMarketViewModel PinnedGlobalMarket { get; }
    public ObservableCollection<GlobalMarketViewModel> GlobalMarketTrackItems { get; }
    public event Action? RenderSurfaceRecoveryRequested;

    public static ProductSceneViewModel CreateDefault()
    {
        YFinanceServerProcessManager manager = new(
            new YFinanceServerProcessManagerOptions
            {
                DiagnosticSink = message => TraceLog.WarnState(
                    "YFinanceServerProcessManager",
                    "ServerLaunchFailed",
                    [new("message", message)])
            });
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
        lock (_sceneStartupGate)
        {
            if (_sceneDisposalStarted)
                return Task.CompletedTask;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            lock (_renderHeartbeatGate)
                _renderHeartbeat.Start(now);
            _renderHeartbeatFixtureStartedUtc = now;
            _initialQuoteSequence ??= RunInitialQuoteSequenceAsync(_lifetimeCts.Token);
            // The upstream scene first presents its bootstrap state and completes its
            // initial quote ordering before its independent background lanes fan out.
            _tickerMotionLoop ??= RunTickerMotionLoopAsync(_lifetimeCts.Token);
            _newsPlaybackLoop ??= RunNewsPlaybackLoopAsync(_lifetimeCts.Token);
            _ambientLoop ??= RunAmbientLoopAsync(_lifetimeCts.Token);
            _renderHeartbeatLoop ??= RunRenderHeartbeatLoopAsync(_lifetimeCts.Token);
        }
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
                if (DateTimeOffset.UtcNow - _lastNtpSyncUtc >= TimeSpan.FromMinutes(10))
                    await RefreshNtpAsync(cancellationToken);
                await InvokeOnUiAsync(() => UpdateClockAndMotion(elapsed), cancellationToken);
                DateTimeOffset acceptedAt = DateTimeOffset.UtcNow;
                TimeSpan fixtureElapsed = acceptedAt - _renderHeartbeatFixtureStartedUtc;
                bool suppressFixtureFrame = _renderHeartbeatFixtureEnabled &&
                    fixtureElapsed >= TimeSpan.FromSeconds(12) &&
                    fixtureElapsed < TimeSpan.FromSeconds(19);
                if (!suppressFixtureFrame)
                {
                    RenderSurfaceHeartbeatResult heartbeat;
                    lock (_renderHeartbeatGate)
                        heartbeat = _renderHeartbeat.AcceptFrame(acceptedAt);
                    TraceRenderHeartbeat(heartbeat);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Keep clocks and motion alive if one optional background is malformed.
                TraceDegradedLane("ambient", ex);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    private async Task RunTickerMotionLoopAsync(CancellationToken cancellationToken)
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
                await InvokeOnUiAsync(() =>
                {
                    foreach (TickerLaneViewModel lane in Lanes)
                        lane.Step(elapsed);
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A single lane failure must not permanently stop tape motion.
                TraceDegradedLane("ticker-motion", ex);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(33), cancellationToken);
        }
    }

    private async Task RunNewsPlaybackLoopAsync(CancellationToken cancellationToken)
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
                await InvokeOnUiAsync(() =>
                {
                    _newsPlayback.Step(elapsed);
                    NewsText = _newsPlayback.DisplayText;
                    NewsVerticalOffset = _newsPlayback.VerticalOffset;
                    NewsPhaseText = _newsPlayback.Phase.ToString();
                    TraceCinematicPlayback(DateTimeOffset.UtcNow);
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Playback recovers on the next tick without affecting other scene lanes.
                TraceDegradedLane("news-playback", ex);
            }
            await Task.Delay(TimeSpan.FromMilliseconds(40), cancellationToken);
        }
    }

    private async Task RunRenderHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            RenderSurfaceHeartbeatResult heartbeat;
            lock (_renderHeartbeatGate)
                heartbeat = _renderHeartbeat.Inspect(DateTimeOffset.UtcNow, _cinematicPlaybackActive);
            if (heartbeat.Signal != RenderSurfaceHeartbeatSignal.RecoveryRequested)
                continue;

            TraceRenderHeartbeat(heartbeat);
            await InvokeOnUiAsync(
                () => RenderSurfaceRecoveryRequested?.Invoke(),
                cancellationToken);
        }
    }

    private async Task RunPortfolioQuoteLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            try
            {
                await RefreshPortfolioQuotesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceDegradedLane("portfolio-quotes", ex);
            }
            await Task.Delay(QuoteRefreshPolicy.GetRefreshPollingInterval(_settings, DateTimeOffset.UtcNow), cancellationToken);
        }
    }

    private async Task RunMacroQuoteLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            try
            {
                await RefreshMacroQuotesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceDegradedLane("macro-quotes", ex);
            }
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private async Task RunWorldMarketsLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            try
            {
                await RefreshGlobalMarketsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceDegradedLane("global-markets", ex);
            }
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    private async Task RunGraphRefreshLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(12), cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_settings.EnableFloatingGraphs)
            {
                await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
                continue;
            }

            int desiredGraphCount = Math.Min(
                16,
                Math.Max(1, _settings.MaxFloatingGraphsPerTape * Math.Max(1, Lanes.Count)));
            for (int attempt = 0; attempt < 6 && _resolvedGraphCount < desiredGraphCount; attempt++)
            {
                await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
                try
                {
                    await RefreshGraphsAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // History failure degrades the graph lane without terminating its scheduler.
                    TraceDegradedLane("graph-history", ex);
                }

                if (_resolvedGraphCount < desiredGraphCount && attempt < 5)
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken);
        }
    }

    private async Task RunNewsRefreshLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            try
            {
                await RefreshNewsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceDegradedLane("news-refresh", ex);
            }
            int refreshMinutes = Math.Clamp(_settings.NewsRefreshMinutes, 30, 240);
            await Task.Delay(TimeSpan.FromMinutes(refreshMinutes), cancellationToken);
        }
    }

    private async Task WaitUntilCinematicPlaybackActiveAsync(CancellationToken cancellationToken)
    {
        while (!_cinematicPlaybackActive)
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
    }

    private async Task RunInitialQuoteSequenceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken);
            WriteCinematicTrace("STARTUP;SIGNAL=BOOTSTRAP_READY");

            // The upstream live scene primes macro, world-market, then user-tape
            // symbols. Do this once before history, news, and recurring lanes fan out.
            await _sceneStartupCoordinator.RunAsync(async (stage, token) =>
            {
                WriteCinematicTrace($"STARTUP;STAGE={stage};SIGNAL=STARTED");
                switch (stage)
                {
                    case SceneStartupStage.MacroQuotes:
                        await RefreshMacroQuotesAsync(token);
                        break;
                    case SceneStartupStage.WorldMarketQuotes:
                        await RefreshGlobalMarketsAsync(token, refreshWeather: false);
                        break;
                    case SceneStartupStage.PortfolioQuotes:
                        await RefreshPortfolioQuotesAsync(token);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported scene startup stage: {stage}.");
                }

                WriteCinematicTrace($"STARTUP;STAGE={stage};SIGNAL=COMPLETED");
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            // Each recurring loop remains responsible for retrying its own optional source.
        }
        finally
        {
            _initialQuoteSequenceCompleted.TrySetResult();
            // This method owns the cancellation/disposal decision under the
            // same lock used by DisposeAsync when it snapshots every started task.
            StartDeferredSceneLoops();
        }
    }

    private void StartDeferredSceneLoops()
    {
        lock (_sceneStartupGate)
        {
            if (_sceneDisposalStarted || _lifetimeCts.IsCancellationRequested || _deferredSceneLoopsStarted)
                return;

            _deferredSceneLoopsStarted = true;
            _deferredSceneLoops = Task.WhenAll(
                RunPortfolioQuoteLoopAsync(_lifetimeCts.Token),
                RunMacroQuoteLoopAsync(_lifetimeCts.Token),
                RunWorldMarketsLoopAsync(_lifetimeCts.Token),
                RunGraphRefreshLoopAsync(_lifetimeCts.Token),
                RunNewsRefreshLoopAsync(_lifetimeCts.Token));
            WriteCinematicTrace("STARTUP;SIGNAL=DEFERRED_LANES_STARTED");
        }
    }

    private async Task RefreshPortfolioQuotesAsync(CancellationToken cancellationToken)
    {
        await InvokeOnUiAsync(() =>
        {
            ClockDateText = DateTimeOffset.Now.ToString("ddd dd-MMM-yyyy").ToUpperInvariant();
            ClockText = DateTimeOffset.UtcNow.ToString("HH:mm:ss 'UTC'");
            DataFreshnessText = "LOADING - waiting for data";
            FreshnessBrush = "#D8E9F8";
        }, cancellationToken);

        Dictionary<string, List<Action<QuoteSnapshot>>> targets = Lanes
            .SelectMany(static lane => lane.Quotes)
            .GroupBy(static ticker => ticker.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(ticker => (Action<QuoteSnapshot>)ticker.Apply).ToList(),
                StringComparer.OrdinalIgnoreCase);
        ProgressiveQuoteRefreshResult result = await _portfolioQuotePipeline.RefreshAsync(
            targets.Keys,
            _quoteProvider,
            cancellationToken);

        foreach (QuoteSnapshot quote in result.UpdatedQuotes)
        {
            if (!targets.TryGetValue(quote.Symbol, out List<Action<QuoteSnapshot>>? applyActions))
                continue;

            await InvokeOnUiAsync(() =>
            {
                foreach (Action<QuoteSnapshot> apply in applyActions)
                    apply(quote);

                ApplyQuoteToGraph(quote);
                LastUpdatedText = "Last Updated: " + TickerFormatter.FormatUpdatedSymbol(quote);
                MarketStatusText = "Market: New York " + FormatMarketSession(quote.MarketSession);
                bool hardStale = QuoteRefreshPolicy.IsHardStale(quote, _settings, DateTimeOffset.UtcNow);
                DataFreshnessText = hardStale ? "DELAYED - cached market data" : "LIVE quote feed";
                FreshnessBrush = hardStale ? "#F4C95D" : "#39E75F";
            }, cancellationToken);
        }

        if (!result.ProviderHealth.IsHealthy && result.CachedQuotes.Count == 0)
        {
            await InvokeOnUiAsync(() =>
            {
                DataFreshnessText = "OFFLINE - waiting for local market service";
                FreshnessBrush = "#FF8A55";
                MarketStatusText = "Market: New York -- unavailable";
            }, cancellationToken);
        }
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
            TraceDegradedLane("macro-quotes", ex);
        }
    }

    private async Task RefreshGlobalMarketsAsync(CancellationToken cancellationToken, bool refreshWeather = true)
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
            TraceDegradedLane("global-markets", ex);
        }

        if (!refreshWeather || DateTimeOffset.UtcNow < _nextWeatherRefreshUtc)
            return;

        IReadOnlyDictionary<string, WeatherSnapshot> weatherSnapshots;
        try
        {
            bool networkAvailable = await _networkProbe.IsInternetAvailableAsync(cancellationToken).ConfigureAwait(false);
            weatherSnapshots = await _weatherService.GetWeatherAsync(GlobalMarkets, networkAvailable, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TraceDegradedLane("weather", ex);
            weatherSnapshots = new Dictionary<string, WeatherSnapshot>(StringComparer.OrdinalIgnoreCase);
        }
        await InvokeOnUiAsync(() =>
        {
            foreach (GlobalMarketViewModel market in GlobalMarkets)
            {
                if (weatherSnapshots.TryGetValue(market.Key, out WeatherSnapshot? snapshot))
                    market.WeatherText = $"{WorldWeatherService.GetGlyph(snapshot.WeatherCode, snapshot.IsDay)} {snapshot.TemperatureCelsius:0}C";
            }
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
                    _graphBuildCache.GetOrBuild(
                        tapeName,
                        history,
                        quote.ChangePercent,
                        _settings.EnableBouncingGraphCards,
                        () => builder.Build(tapeName, history, quote.ChangePercent, index)),
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
            _resolvedGraphCount = Graphs.Count;
            ArmGraphImpulseFixture();
        }, cancellationToken);
    }

    private async Task RefreshNewsAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_forceNewsFailure)
                throw new HttpRequestException("Controlled news-provider failure.");

            RssPlaybackSnapshot playback = await _newsService.GetRssPlaybackSnapshotAsync(_settings, cancellationToken);
            RssFeedFreshnessSnapshot freshness = playback.Freshness;
            string latestPublication = freshness.LatestPublicationUtc?.ToString("O") ?? "NONE";
            WriteCinematicTrace(
                $"NEWS_SOURCE;STATE={freshness.State};LATEST_UTC={latestPublication}");
            await InvokeOnUiAsync(() => _newsPlayback.SetHeadlines(playback.Headlines), cancellationToken);
            WriteCinematicTrace($"NEWS_PLAYBACK_PUBLISHED;SOURCE=RSS;HEADLINE_COUNT={playback.Headlines.Count}");

            // Publish usable RSS immediately. Optional AI generation may be slow or
            // rate-limited, and must replace RSS only after it actually succeeds.
            if (_settings.NewsScrollerMode == NewsScrollerMode.SummarizedFinancialNews)
            {
                RssPlaybackSnapshot aiPlayback = await _newsService.ApplyAiSummaryAsync(
                    _settings,
                    playback,
                    cancellationToken);
                if (!ReferenceEquals(aiPlayback, playback))
                {
                    await InvokeOnUiAsync(() => _newsPlayback.SetHeadlines(aiPlayback.Headlines), cancellationToken);
                    WriteCinematicTrace($"NEWS_PLAYBACK_PUBLISHED;SOURCE=AI;HEADLINE_COUNT={aiPlayback.Headlines.Count}");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WriteCinematicTrace($"NEWS_SOURCE;STATE=UNAVAILABLE;LATEST_UTC=NONE;ERROR={ex.GetType().Name}");
            await InvokeOnUiAsync(
                () => _newsPlayback.SetHeadlines(["Finance news headlines are temporarily unavailable"]),
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
        DateTimeOffset now = GetReferenceUtcNow();
        ClockDateText = now.ToLocalTime().ToString("ddd dd-MMM-yyyy").ToUpperInvariant();
        ClockText = now.ToString("HH:mm:ss 'UTC'");
        foreach (GlobalMarketViewModel market in GlobalMarkets)
        {
            try
            {
                TimeZoneInfo zone = ExchangeTimeZoneResolver.Resolve(market.TimeZoneId);
                market.TimeText = TimeZoneInfo.ConvertTime(now, zone).ToString("HH:mm");
            }
            catch (InvalidTimeZoneException)
            {
                market.TimeText = "--:--";
            }
        }

        TriggerGraphImpulseFixture(now);
        _graphMotion?.Step(Graphs, elapsed);
        TraceCompletedGraphFixtureImpulses();

        _globalMarketsMotion.Step(elapsed);
        GlobalMarketsTrackOffset = _globalMarketsMotion.Offset;

        _backgroundCinema?.Step(elapsed);
        if (_backgroundCinema is not null && now >= _nextBackgroundChangeUtc)
        {
            _backgroundCinema.BeginRotation();
            _nextBackgroundChangeUtc = now.AddSeconds(_settings.BackgroundChangeSeconds);
        }
        ApplyBackgroundCinemaState();
    }

    private async Task RefreshNtpAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset attemptUtc = DateTimeOffset.UtcNow;
        try
        {
            if (!await _networkProbe.IsInternetAvailableAsync(cancellationToken).ConfigureAwait(false))
            {
                _ntpOffset = null;
                _lastNtpSyncUtc = attemptUtc;
                return;
            }

            NtpSyncResult result = await _ntpTimeService.TryGetUtcNowAsync(cancellationToken).ConfigureAwait(false);
            _ntpOffset = result.Success ? result.UtcNow - DateTimeOffset.UtcNow : null;
            _lastNtpSyncUtc = attemptUtc;
            TraceLog.InfoState(
                "NtpTimeService",
                result.Success ? "SyncSucceeded" : "LocalClockFallback",
                [new("source", result.Source), new("success", result.Success), new("offset_ms", _ntpOffset?.TotalMilliseconds ?? 0d)]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _ntpOffset = null;
            _lastNtpSyncUtc = attemptUtc;
            TraceLog.WarnState("NtpTimeService", "RefreshFailed", [new("exception_type", exception.GetType().Name)]);
        }
    }

    private DateTimeOffset GetReferenceUtcNow()
        => _ntpOffset.HasValue && DateTimeOffset.UtcNow - _lastNtpSyncUtc <= TimeSpan.FromMinutes(20)
            ? DateTimeOffset.UtcNow + _ntpOffset.Value
            : DateTimeOffset.UtcNow;

    public void ConfigureGraphViewport(double width, double height)
    {
        _graphViewportWidth = Math.Max(1d, width);
        _graphViewportHeight = Math.Max(1d, height);
        _graphMotion?.ConfigureViewport(_graphViewportWidth, _graphViewportHeight, Graphs);
    }

    public void PauseCinematicPlayback()
    {
        _cinematicPlaybackActive = false;
        lock (_renderHeartbeatGate)
            _renderHeartbeat.Pause();
    }

    public void ResumeCinematicPlayback()
    {
        lock (_renderHeartbeatGate)
            _renderHeartbeat.Resume(DateTimeOffset.UtcNow);
        _cinematicPlaybackActive = true;
    }

    public void ConfigureCinematicViewport(double width)
    {
        ConfigureGlobalMarketViewport(Math.Max(1d, width - 376d));
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
        => TraceLog.Info("ProductScene.GraphFixture", message);

    private void TraceCinematicPlayback(DateTimeOffset now)
    {
        bool phaseChanged = _newsPlayback.Phase != _lastTracedNewsPhase;
        if (!phaseChanged && now < _nextCinematicTraceUtc)
            return;

        _lastTracedNewsPhase = _newsPlayback.Phase;
        _nextCinematicTraceUtc = now.AddSeconds(1);
        WriteCinematicTrace(
            $"NEWS;PHASE={_newsPlayback.Phase};HEADLINE={_newsPlayback.HeadlineIndex};SEGMENT={_newsPlayback.SegmentIndex};Y={_newsPlayback.VerticalOffset:0.00};TEXT_LENGTH={_newsPlayback.DisplayText.Length}");
        WriteCinematicTrace(
            $"MARKETS;X={_globalMarketsMotion.Offset:0.00};SEQUENCE_WIDTH={_globalMarketsMotion.SequenceWidth:0.00};COPIES={_globalMarketsMotion.RequiredCopies};GRAPH_COUNT={_resolvedGraphCount}");
    }

    private void WriteCinematicTrace(string message)
        => TraceLog.Info("ProductScene.Cinematic", message);

    private void TraceDegradedLane(string lane, Exception exception)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (_degradedTraceGate)
        {
            if (_lastDegradedTraceUtc.TryGetValue(lane, out DateTimeOffset last) &&
                now - last < TimeSpan.FromSeconds(5))
                return;

            _lastDegradedTraceUtc[lane] = now;
        }

        WriteCinematicTrace($"DEGRADED;LANE={lane};ERROR={exception.GetType().Name}");
    }

    private void TraceRenderHeartbeat(RenderSurfaceHeartbeatResult heartbeat)
    {
        if (heartbeat.Signal == RenderSurfaceHeartbeatSignal.None)
            return;

        WriteCinematicTrace(
            $"RENDER;SIGNAL={heartbeat.Signal};ELAPSED_SECONDS={heartbeat.ElapsedSinceFrame.TotalSeconds:0.00};FRAMES={heartbeat.AcceptedFrameCount};RECOVERY_COUNT={heartbeat.RecoveryCount};EPISODE_ATTEMPT={heartbeat.EpisodeAttempt}");
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
        BackgroundAttributionText = BackgroundAttributions.TryGetValue(
            _backgroundCinema.CurrentSource,
            out string? attribution)
            ? $"© Supratim Sanyal. SANYALnet Labs. | Image: {attribution}"
            : "© Supratim Sanyal. SANYALnet Labs.";
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
        lock (_sceneStartupGate)
            _sceneDisposalStarted = true;
        lock (_renderHeartbeatGate)
            _renderHeartbeat.Stop();
        await _lifetimeCts.CancelAsync();
        Task?[] loops;
        lock (_sceneStartupGate)
        {
            loops =
            [
                _initialQuoteSequence,
                _deferredSceneLoops,
                _tickerMotionLoop,
                _newsPlaybackLoop,
                _ambientLoop,
                _renderHeartbeatLoop
            ];
        }
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
        _portfolioQuotePipeline.Dispose();
        if (_quoteProvider is IDisposable disposableQuoteProvider)
            disposableQuoteProvider.Dispose();
        _serverManager.Dispose();
        _lifetimeCts.Dispose();
    }
}
