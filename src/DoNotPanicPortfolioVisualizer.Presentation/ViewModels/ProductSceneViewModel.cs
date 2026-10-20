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
using System.Collections.Concurrent;
using System.Diagnostics;
using DoNotPanicPortfolioVisualizer.Shared;
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
    public string RuntimeArchitectureFooterText => $"{PortfolioVersion.Version} | {RuntimeArchitecture.CurrentToken}";

    private static readonly (string Label, string Symbol, decimal Minimum, decimal Maximum)[] MacroSymbols =
    [
        ("VIX", "^VIX", -10m, 10m),
        ("NASDAQ", "^IXIC", -10m, 10m),
        ("UST10Y", "^TNX", -10m, 10m),
        ("UST3M", "^IRX", -10m, 10m),
        ("GOLD", "GC=F", -10m, 10m),
        ("CRUDE", "BZ=F", -10m, 10m),
        ("DXY", "DX-Y.NYB", -10m, 10m),
        ("BTC", "BTC-USD", -10m, 10m)
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
            ["avares://DoNotPanicPortfolioVisualizer.App/Assets/ExchangeBackgrounds/new-york-stock-exchange.jpg"] = "Jean-Christophe BENOIST, CC BY 3.0",
            ["avares://DoNotPanicPortfolioVisualizer.App/Assets/ExchangeBackgrounds/london-skyline-public-domain.jpg"] = "Rodrigo.Argenton, CC0",
            ["avares://DoNotPanicPortfolioVisualizer.App/Assets/ExchangeBackgrounds/shanghai-skyline-public-domain.jpg"] = "Pxfuel, CC0"
        };

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly object _sceneStartupGate = new();
    private readonly object _newsRefreshGate = new();
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
    private readonly YFinanceExchangeTimingService _exchangeTimingService = new();
    private readonly MarketSessionResolver _marketSessionResolver = new();
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
    private CancellationTokenSource? _activeNewsRefreshCts;
    private readonly RenderSurfaceHeartbeatController _renderHeartbeat = new();
    private readonly object _renderHeartbeatGate = new();
    private DateTimeOffset _nextBackgroundChangeUtc;
    private DateTimeOffset _nextWeatherRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _nextCalendarRefreshUtc = DateTimeOffset.MinValue;
    private readonly ExchangeCalendarSet _exchangeCalendars = new();
    private bool _hasNewYorkCalendarStatus;
    private readonly ConcurrentDictionary<string, QuoteSnapshot> _latestQuotes = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _lastNtpSyncUtc = DateTimeOffset.MinValue;
    private Task? _ntpRefreshTask;
    private TimeSpan? _ntpOffset;
    private double _graphViewportWidth = 1280d;
    private double _graphViewportHeight = 720d;
    private double _globalMarketsViewportWidth = 900d;
    private Task? _initialQuoteSequence;
    private bool _macroDialRangesLoaded;
    private Task? _deferredSceneLoops;
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
    private readonly HashSet<string> _productionImpulseSymbols = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _nextGraphFixtureImpulseUtc;
    private DateTimeOffset _nextCinematicTraceUtc = DateTimeOffset.MinValue;
    private long _lastMarketStatusSecond = long.MinValue;
    private MarketSession _lastTracedNewYorkSession = MarketSession.Unknown;
    private MarketSession _pendingNewYorkSession = MarketSession.Unknown;
    private int _pendingNewYorkSessionObservations;
    private NewsPlaybackPhase _lastTracedNewsPhase = NewsPlaybackPhase.Idle;
    private readonly object _degradedTraceGate = new();
    private readonly Dictionary<string, DateTimeOffset> _lastDegradedTraceUtc = new(StringComparer.Ordinal);
    private volatile bool _cinematicPlaybackActive = true;
    private volatile int _resolvedGraphCount;
    private readonly TaskCompletionSource _initialQuoteSequenceCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [ObservableProperty]
    private string _marketStatusText = "Market: New York -- loading";

    [ObservableProperty]
    private string _updatedTickerFieldText = string.Empty;

    [ObservableProperty]
    private string _dataFreshnessText = string.Empty;

    [ObservableProperty]
    private string _freshnessBrush = "#D4DEE5";

    [ObservableProperty]
    private string _clockDateText = DateTimeOffset.UtcNow.ToString("ddd dd-MMM-yyyy").ToUpperInvariant();

    [ObservableProperty]
    private string _clockText = DateTimeOffset.UtcNow.ToString("HH:mm:ss 'UTC'");

    public string RuntimeArchitectureToken => RuntimeArchitecture.CurrentToken;

    [ObservableProperty]
    private string _backgroundSourceA = "avares://DoNotPanicPortfolioVisualizer.App/Assets/ExchangeBackgrounds/new-york-stock-exchange.jpg";

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
                item.Minimum,
                item.Maximum)));
        CalendarReferenceGlobalMarket = GlobalMarkets[0];
        GlobalMarketTrackItems = [];
        ConfigureGlobalMarketViewport(_globalMarketsViewportWidth);
        _newsPlayback.SetHeadlines(["Loading finance news headlines..."]);
        LoadSettings();
    }

    public ObservableCollection<TickerLaneViewModel> Lanes { get; }
    public ObservableCollection<MacroQuoteViewModel> MacroQuotes { get; }
    public ObservableCollection<FloatingGraphViewModel> Graphs { get; }
    public ObservableCollection<GlobalMarketViewModel> GlobalMarkets { get; }
    public GlobalMarketViewModel CalendarReferenceGlobalMarket { get; }
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
            _renderHeartbeatLoop ??= RunRenderHeartbeatLoopAsync(_lifetimeCts.Token);
        }
        return Task.CompletedTask;
    }

    private void LoadSettings(AppSettings? settings = null)
    {
        AppSettings loadedSettings = (settings ?? _settingsService.Load()).Clone();
        lock (_newsRefreshGate)
        {
            _activeNewsRefreshCts?.Cancel();
            _settings = loadedSettings;
        }
        Lanes.Clear();
        foreach (TickerGroup group in _settings.Groups.Where(static group => group.Enabled).Take(4))
            Lanes.Add(new TickerLaneViewModel(group));

        string? selectedFolder = _settings.UseCustomBackgroundImageFolder
            ? _settings.CustomBackgroundImageFolder
            : _settings.BackgroundImageFolder;
        _backgrounds = _backgroundService.GetImages(selectedFolder, _settings.BackgroundIncludeSubfolders);
        string[] builtInBackgrounds =
        [
            "avares://DoNotPanicPortfolioVisualizer.App/Assets/ExchangeBackgrounds/new-york-stock-exchange.jpg",
            "avares://DoNotPanicPortfolioVisualizer.App/Assets/ExchangeBackgrounds/london-skyline-public-domain.jpg",
            "avares://DoNotPanicPortfolioVisualizer.App/Assets/ExchangeBackgrounds/shanghai-skyline-public-domain.jpg"
        ];
        IEnumerable<string> backgroundCatalog = _settings.UseCustomBackgroundImageFolder
            ? _backgrounds.Concat(builtInBackgrounds)
            : builtInBackgrounds.Concat(_backgrounds);
        _backgrounds = backgroundCatalog
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

    public void ReloadSettingsFromDisk()
    {
        if (_sceneDisposalStarted || _lifetimeCts.IsCancellationRequested)
            return;

        try
        {
            LoadSettings();
            _graphBuildCache.Clear();
            _resolvedGraphCount = 0;
            if (!_settings.EnableFloatingGraphs)
            {
                foreach (FloatingGraphViewModel graph in Graphs.ToArray())
                    _graphMotion?.BeginFadeOut(graph);
            }

            if (_initialQuoteSequenceCompleted.Task.IsCompleted)
                _ = RefreshAfterSettingsAppliedAsync(_lifetimeCts.Token);
        }
        catch (Exception exception)
        {
            TraceLog.WarnState(
                "App.Configuration",
                "LiveSettingsApplyFailed",
                [new("exception_type", exception.GetType().Name)]);
        }
    }

    private async Task RefreshAfterSettingsAppliedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshPortfolioQuotesAsync(cancellationToken).ConfigureAwait(false);
            await RefreshGraphsAsync(cancellationToken).ConfigureAwait(false);
            await RefreshNewsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            TraceLog.WarnState(
                "App.Configuration",
                "LiveSettingsRefreshFailed",
                [new("exception_type", exception.GetType().Name)]);
        }
    }

    public void AdvanceTopLeftStatusFrame(TimeSpan elapsed)
    {
        if (!_cinematicPlaybackActive)
            return;

        if (DateTimeOffset.UtcNow - _lastNtpSyncUtc >= TimeSpan.FromMinutes(10))
            StartNtpRefresh(_lifetimeCts.Token);
        DateTimeOffset now = GetReferenceUtcNow();
        string dateText = now.ToString("ddd dd-MMM-yyyy").ToUpperInvariant();
        string clockText = now.ToString("HH:mm:ss 'UTC'");
        if (!string.Equals(ClockDateText, dateText, StringComparison.Ordinal))
            ClockDateText = dateText;
        if (!string.Equals(ClockText, clockText, StringComparison.Ordinal))
            ClockText = clockText;
        long marketStatusSecond = now.ToUnixTimeSeconds();
        if (marketStatusSecond == _lastMarketStatusSecond)
            return;

        _lastMarketStatusSecond = marketStatusSecond;
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
        ApplyExchangeCalendarStatuses(now);
    }

    public void AdvanceTickerLaneFrame(TimeSpan elapsed)
    {
        if (!_cinematicPlaybackActive)
            return;
        foreach (TickerLaneViewModel lane in Lanes)
            lane.Step(elapsed);
    }

    public void AdvanceGraphCardFrame(TimeSpan elapsed)
    {
        if (!_cinematicPlaybackActive)
            return;
        DateTimeOffset now = GetReferenceUtcNow();
        TriggerGraphImpulseFixture(now);
        _graphMotion?.Step(Graphs, elapsed);
        FloatingGraphViewModel[] fadedOutGraphs = Graphs
            .Where(static graph => graph.FadeOutComplete)
            .ToArray();
        foreach (FloatingGraphViewModel graph in fadedOutGraphs)
            Graphs.Remove(graph);
        if (fadedOutGraphs.Length > 0)
            _resolvedGraphCount = Graphs.Count(static graph => !graph.IsFadingOut);
        TraceCompletedProductionImpulses();
        TraceCompletedGraphFixtureImpulses();
    }

    public void AdvanceWorldMarketTapeFrame(TimeSpan elapsed)
    {
        if (!_cinematicPlaybackActive)
            return;
        _globalMarketsMotion.Step(elapsed);
        GlobalMarketsTrackOffset = _globalMarketsMotion.Offset;
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

    public void AdvanceFinanceNewsFrame(TimeSpan elapsed)
    {
        if (!_cinematicPlaybackActive)
            return;
        _newsPlayback.Step(elapsed);
        if (!string.Equals(NewsText, _newsPlayback.DisplayText, StringComparison.Ordinal))
            NewsText = _newsPlayback.DisplayText;
        if (NewsVerticalOffset != _newsPlayback.VerticalOffset)
            NewsVerticalOffset = _newsPlayback.VerticalOffset;
        string phase = _newsPlayback.Phase.ToString();
        if (!string.Equals(NewsPhaseText, phase, StringComparison.Ordinal))
            NewsPhaseText = phase;
        TraceCinematicPlayback(GetReferenceUtcNow());
    }

    public void AdvanceBackgroundCinemaFrame(TimeSpan elapsed)
    {
        if (!_cinematicPlaybackActive)
            return;
        DateTimeOffset now = GetReferenceUtcNow();
        _backgroundCinema?.Step(elapsed);
        if (_backgroundCinema is not null && now >= _nextBackgroundChangeUtc)
        {
            _backgroundCinema.PrepareRotation();
            _nextBackgroundChangeUtc = now.AddSeconds(_settings.BackgroundChangeSeconds);
        }
        ApplyBackgroundCinemaState();
    }

    public void AdvanceMacroDialFrame(TimeSpan elapsed)
    {
        if (!_cinematicPlaybackActive)
            return;
        foreach (MacroQuoteViewModel macro in MacroQuotes)
            macro.Step(elapsed);
    }

    private async Task RunRenderHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            RenderSurfaceHeartbeatResult heartbeat;
            lock (_renderHeartbeatGate)
                heartbeat = _renderHeartbeat.Inspect(DateTimeOffset.UtcNow, _cinematicPlaybackActive);
            if (heartbeat.Signal != RenderSurfaceHeartbeatSignal.RecoveryRequested)
                continue;

            TraceRenderHeartbeat(heartbeat);
            await InvokeOnUiAsync(
                () => RenderSurfaceRecoveryRequested?.Invoke(),
                cancellationToken,
                "render-recovery");
        }
    }

    private async Task RunPortfolioQuoteLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await RefreshPortfolioQuotesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceDegradedLane("portfolio-quotes", ex);
            }
            await Task.Delay(QuoteRefreshPolicy.GetRefreshPollingInterval(_settings, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunMacroQuoteLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await RefreshMacroQuotesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceDegradedLane("macro-quotes", ex);
            }
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunWorldMarketsLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await RefreshGlobalMarketsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceDegradedLane("global-markets", ex);
            }
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunGraphRefreshLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromSeconds(12), cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_settings.EnableFloatingGraphs)
            {
                await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);
                continue;
            }

            int desiredGraphCount = Math.Min(
                16,
                Math.Max(1, _settings.MaxFloatingGraphsPerTape * Math.Max(1, Lanes.Count)));
            for (int attempt = 0; attempt < 6 && _resolvedGraphCount < desiredGraphCount; attempt++)
            {
                await WaitUntilCinematicPlaybackActiveAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await RefreshGraphsAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The room watched the price, while the price watched the room.
                    TraceDegradedLane("graph-history", ex);
                }

                if (_resolvedGraphCount < desiredGraphCount && attempt < 5)
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunNewsRefreshLoopAsync(CancellationToken cancellationToken)
    {
        await _initialQuoteSequenceCompleted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await RefreshNewsAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                TraceDegradedLane("news-refresh", ex);
            }
            int refreshMinutes = Math.Clamp(_settings.NewsRefreshMinutes, 30, 240);
            await Task.Delay(TimeSpan.FromMinutes(refreshMinutes), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WaitUntilCinematicPlaybackActiveAsync(CancellationToken cancellationToken)
    {
        while (!_cinematicPlaybackActive)
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
    }

    private async Task RunInitialQuoteSequenceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await WaitUntilCinematicPlaybackActiveAsync(cancellationToken).ConfigureAwait(false);
            WriteCinematicTrace("STARTUP;SIGNAL=BOOTSTRAP_READY");

            // An explanation was offered. It had excellent shoes and no map.
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
            // The market changed its mind, then denied having one.
            TraceLog.WarnState("SceneStartup", "InitialQuoteSequenceFailed", [new("fallback", "recurring_lanes_retry_optional_sources")]);
        }
        finally
        {
            _initialQuoteSequenceCompleted.TrySetResult();
            // Hope sat down beside arithmetic. Arithmetic asked for more room.
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

        QuoteSnapshot[] updatedQuotes = result.UpdatedQuotes.ToArray();
        foreach (QuoteSnapshot quote in result.UpdatedQuotes)
        {
            if (!updatedQuotes.Contains(quote))
                _latestQuotes[quote.Symbol] = quote;
        }
        for (int quoteIndex = 0; quoteIndex < updatedQuotes.Length; quoteIndex++)
        {
            QuoteSnapshot quote = updatedQuotes[quoteIndex];
            await InvokeOnUiAsync(() =>
            {
                if (targets.TryGetValue(quote.Symbol, out List<Action<QuoteSnapshot>>? applyActions))
                {
                    foreach (Action<QuoteSnapshot> apply in applyActions)
                        apply(quote);
                }

                _latestQuotes[quote.Symbol] = quote;
                ApplyQuoteToGraph(quote);
                UpdatedTickerFieldText = TickerFormatter.FormatUpdatedSymbol(quote);
                bool hardStale = QuoteRefreshPolicy.IsHardStale(quote, _settings, DateTimeOffset.UtcNow);
                DataFreshnessText = hardStale ? "DELAYED - cached market data" : "LIVE quote feed";
                FreshnessBrush = hardStale ? "#F4C95D" : "#39E75F";

                if (!_hasNewYorkCalendarStatus &&
                    string.Equals(quote.Symbol, CalendarReferenceGlobalMarket.Symbol, StringComparison.OrdinalIgnoreCase))
                    ApplyNewYorkFallbackStatus(DateTimeOffset.UtcNow);
            }, cancellationToken, "portfolio-ticker-batch");
            if (quoteIndex < updatedQuotes.Length - 1)
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }

        if (result.NetworkFailureReported && updatedQuotes.Length == 0)
        {
            await InvokeOnUiAsync(() =>
            {
                DataFreshnessText = "OFFLINE - YFinance network failure";
                FreshnessBrush = "#FF8A55";
            }, cancellationToken, "portfolio-offline");
        }
    }

    private async Task RefreshMacroQuotesAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<QuoteSnapshot> quotes = await SingleSymbolQuoteRefresh.FetchAsync(
                _quoteProvider,
                MacroQuotes.Select(static macro => macro.Symbol),
                cancellationToken);
            List<(MacroQuoteViewModel Macro, QuoteSnapshot Quote)> publishable = [];
            foreach (MacroQuoteViewModel macro in MacroQuotes)
            {
                QuoteSnapshot? quote = quotes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Symbol, macro.Symbol, StringComparison.OrdinalIgnoreCase));
                if (quote is null)
                    continue;

                publishable.Add((macro, quote));
            }

            if (publishable.Count > 0)
            {
                await InvokeOnUiAsync(() =>
                {
                    foreach ((MacroQuoteViewModel macro, QuoteSnapshot quote) in publishable)
                    {
                        macro.Apply(quote);
                        _latestQuotes[quote.Symbol] = quote;
                        UpdatedTickerFieldText = TickerFormatter.FormatUpdatedSymbol(quote);
                    }
                }, cancellationToken, "macro-quotes-batch");
            }

            if (!_macroDialRangesLoaded)
                await LoadMacroDialRangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A bargain appeared, carrying a note that said “probably”.
            TraceDegradedLane("macro-quotes", ex);
        }
    }

    private async Task LoadMacroDialRangesAsync(CancellationToken cancellationToken)
    {
        _macroDialRangesLoaded = true;
        try
        {
            IReadOnlyList<TickerHistorySnapshot> histories = await _historicalProvider.GetHistoryAsync(
                MacroQuotes.Select(static macro => macro.Symbol),
                365,
                cancellationToken);
            Dictionary<string, (decimal Minimum, decimal Maximum)> ranges = new(StringComparer.OrdinalIgnoreCase);
            foreach (TickerHistorySnapshot history in histories)
            {
                decimal[] values = history.Points
                    .Select(static point => point.Close)
                    .Where(static value => value > 0m)
                    .ToArray();
                if (values.Length < 2)
                    continue;

                decimal[] dailyChanges = values.Zip(values.Skip(1), static (previous, current) => (current - previous) / previous * 100m).ToArray();
                decimal low = Math.Min(0m, dailyChanges.Min());
                decimal high = Math.Max(0m, dailyChanges.Max());
                decimal padding = Math.Max((high - low) * 0.08m, 0.1m);
                ranges[history.Symbol] = (low - padding, high + padding);
            }

            if (ranges.Count > 0)
            {
                await InvokeOnUiAsync(() =>
                {
                    foreach (MacroQuoteViewModel macro in MacroQuotes)
                    {
                        if (ranges.TryGetValue(macro.Symbol, out (decimal Minimum, decimal Maximum) range))
                            macro.SetHistoricalRange(range.Minimum, range.Maximum);
                        if (_latestQuotes.TryGetValue(macro.Symbol, out QuoteSnapshot? quote))
                            macro.Apply(quote);
                    }
                }, cancellationToken, "macro-dial-historical-ranges");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TraceDegradedLane("macro-dial-history", ex);
        }
    }

    private async Task RefreshGlobalMarketsAsync(CancellationToken cancellationToken, bool refreshWeather = true)
    {
        try
        {
            await RefreshExchangeCalendarsAsync(cancellationToken);
            IReadOnlyList<QuoteSnapshot> quotes = await SingleSymbolQuoteRefresh.FetchAsync(
                _quoteProvider,
                GlobalMarkets
                    .Select(static market => market.Symbol)
                    .Where(symbol => !IsClosedClockMarketQuoteFresh(symbol, DateTimeOffset.UtcNow)),
                cancellationToken);
            List<(GlobalMarketViewModel Market, QuoteSnapshot Quote)> publishable = [];
            foreach (GlobalMarketViewModel market in GlobalMarkets)
            {
                QuoteSnapshot? quote = quotes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Symbol, market.Symbol, StringComparison.OrdinalIgnoreCase));
                if (quote is null)
                    continue;

                publishable.Add((market, quote));
            }

            if (publishable.Count > 0)
            {
                await InvokeOnUiAsync(() =>
                {
                    foreach ((GlobalMarketViewModel market, QuoteSnapshot quote) in publishable)
                    {
                        market.ApplyQuote(quote);
                        _latestQuotes[quote.Symbol] = quote;
                        UpdatedTickerFieldText = TickerFormatter.FormatUpdatedSymbol(quote);
                    }
                }, cancellationToken, "global-markets-batch");
            }
            await InvokeOnUiAsync(
                () => ApplyExchangeCalendarStatuses(DateTimeOffset.UtcNow),
                cancellationToken,
                "market-status");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The market made a noise; somebody called it a signal.
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
        }, cancellationToken, "world-weather");
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
        List<(FloatingGraphViewModel Graph, decimal? Last, decimal? ChangePercent, bool IsStale, MarketSession Session)> resolvedGraphs = [];
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
                    quote.ChangePercent,
                    quote.IsStale,
                    quote.MarketSession));
            }
        }

        const int graphPublishChunkSize = 1;
        HashSet<string> selectedKeys = resolvedGraphs
            .Select(static item => GetGraphKey(item.Graph))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<FloatingGraphViewModel> enteringGraphs = [];
        List<string> staleGraphKeys = Graphs
            .Where(graph => !selectedKeys.Contains(GetGraphKey(graph)) && !graph.IsFadingOut)
            .Select(GetGraphKey)
            .ToList();
        foreach (string staleGraphKey in staleGraphKeys)
        {
            await InvokeOnUiAsync(
                () =>
                {
                    FloatingGraphViewModel? staleGraph = Graphs.FirstOrDefault(graph =>
                        string.Equals(GetGraphKey(graph), staleGraphKey, StringComparison.OrdinalIgnoreCase));
                    if (staleGraph is not null)
                        _graphMotion?.BeginFadeOut(staleGraph);
                },
                cancellationToken,
                "graph-prune");
        }

        for (int chunkStart = 0; chunkStart < resolvedGraphs.Count; chunkStart += graphPublishChunkSize)
        {
            List<(FloatingGraphViewModel Graph, decimal? Last, decimal? ChangePercent, bool IsStale, MarketSession Session)> chunk = resolvedGraphs
                .Skip(chunkStart)
                .Take(graphPublishChunkSize)
                .ToList();
            await InvokeOnUiAsync(() =>
            {
                foreach ((FloatingGraphViewModel graph, decimal? last, decimal? changePercent, bool isStale, MarketSession session) in chunk)
                {
                    graph.MarketSession = session;
                    FloatingGraphViewModel? existing = Graphs.FirstOrDefault(candidate =>
                        string.Equals(GetGraphKey(candidate), GetGraphKey(graph), StringComparison.OrdinalIgnoreCase));
                    if (existing is not null)
                    {
                        if (existing.IsFadingOut)
                            _graphMotion?.BeginFadeIn(existing);
                        existing.CopyContentFrom(graph);
                        ApplyProductionGraphQuote(existing, last, changePercent, isStale, "HISTORICAL_REFRESH");
                        continue;
                    }

                    _graphMotion?.ApplyQuote(graph, last, changePercent, suppressMotionCue: true);
                    _graphMotion?.PrepareForEntry(graph);
                    Graphs.Add(graph);
                    enteringGraphs.Add(graph);
                }
            }, cancellationToken, "graph-refresh");
        }

        await InvokeOnUiAsync(() =>
        {
            _graphMotion?.ConfigureViewport(_graphViewportWidth, _graphViewportHeight, Graphs);
            foreach (FloatingGraphViewModel graph in enteringGraphs)
                _graphMotion?.BeginFadeIn(graph);
            _resolvedGraphCount = Graphs.Count(static graph => !graph.IsFadingOut);
            ArmGraphImpulseFixture();
        }, cancellationToken, "graph-viewport");
    }

    private async Task RefreshNewsAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource operationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        AppSettings settings;
        lock (_newsRefreshGate)
        {
            settings = _settings.Clone();
            _activeNewsRefreshCts?.Cancel();
            _activeNewsRefreshCts = operationCts;
        }
        CancellationToken operationToken = operationCts.Token;

        try
        {
            if (_forceNewsFailure)
                throw new HttpRequestException("Controlled news-provider failure.");

            RssPlaybackSnapshot playback = await _newsService.GetRssPlaybackSnapshotAsync(settings, operationToken);
            RssFeedFreshnessSnapshot freshness = playback.Freshness;
            string latestPublication = freshness.LatestPublicationUtc?.ToString("O") ?? "NONE";
            WriteCinematicTrace(
                $"NEWS_SOURCE;STATE={freshness.State};LATEST_UTC={latestPublication}");
            await InvokeOnUiAsync(() => _newsPlayback.SetHeadlines(playback.Headlines), operationToken, "news-rss");
            WriteCinematicTrace($"NEWS_PLAYBACK_PUBLISHED;SOURCE=RSS;HEADLINE_COUNT={playback.Headlines.Count}");

            // The future sent no details, but the room filled in several.
            if (settings.NewsScrollerMode == NewsScrollerMode.SummarizedFinancialNews)
            {
                RssPlaybackSnapshot aiPlayback = await _newsService.ApplyAiSummaryAsync(
                    settings,
                    playback,
                    operationToken,
                    (headlines, progressToken) => InvokeOnUiAsync(
                        () => _newsPlayback.SetHeadlinesPreservingCurrent(headlines),
                        progressToken,
                        "news-ai-progress"));
                if (!ReferenceEquals(aiPlayback, playback))
                {
                    await InvokeOnUiAsync(
                        () => _newsPlayback.SetHeadlinesPreservingCurrent(aiPlayback.Headlines),
                        operationToken,
                        "news-ai");
                    WriteCinematicTrace($"NEWS_PLAYBACK_PUBLISHED;SOURCE=AI;HEADLINE_COUNT={aiPlayback.Headlines.Count}");
                }
            }
        }
        catch (OperationCanceledException) when (
            operationToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // A newer settings snapshot has taken the reins; this request must leave the stage before publishing yesterday's headlines.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            WriteCinematicTrace($"NEWS_SOURCE;STATE=UNAVAILABLE;LATEST_UTC=NONE;ERROR={ex.GetType().Name}");
            await InvokeOnUiAsync(
                () => _newsPlayback.SetHeadlines(["Finance news headlines are temporarily unavailable"]),
                operationToken,
                "news-fallback");
        }
        finally
        {
            lock (_newsRefreshGate)
            {
                if (ReferenceEquals(_activeNewsRefreshCts, operationCts))
                    _activeNewsRefreshCts = null;
            }
            operationCts.Dispose();
        }
    }

    private Task InvokeOnUiAsync(Action action, CancellationToken cancellationToken, string lane)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ReferenceEquals(SynchronizationContext.Current, _uiContext))
        {
            action();
            return Task.CompletedTask;
        }

        long posted = Stopwatch.GetTimestamp();
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _uiContext.Post(_ =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                long started = Stopwatch.GetTimestamp();
                action();
                TimeSpan queueElapsed = Stopwatch.GetElapsedTime(posted, started);
                TimeSpan uiElapsed = Stopwatch.GetElapsedTime(started);
                if (queueElapsed >= TimeSpan.FromMilliseconds(100) || uiElapsed >= TimeSpan.FromMilliseconds(25))
                {
                    WriteCinematicTrace(
                        $"UI_CALLBACK;LANE={lane};QUEUE_MS={queueElapsed.TotalMilliseconds:0.0};UI_MS={uiElapsed.TotalMilliseconds:0.0}");
                }
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }, null);
        return completion.Task;
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

    private void StartNtpRefresh(CancellationToken cancellationToken)
    {
        if (_ntpRefreshTask is { IsCompleted: false })
            return;

        _lastNtpSyncUtc = DateTimeOffset.UtcNow;
        _ntpRefreshTask = RefreshNtpAsync(cancellationToken);
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

    public void ConfigureTickerLaneViewport(double width)
    {
        double viewportWidth = Math.Max(1d, width);
        foreach (TickerLaneViewModel lane in Lanes)
            lane.ConfigureViewport(viewportWidth);
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
        {
            graph.MarketSession = quote.MarketSession;
            ApplyProductionGraphQuote(graph, quote.Last ?? quote.PreviousClose, quote.ChangePercent, quote.IsStale, "LIVE_QUOTE");
        }
    }

    private async Task RefreshExchangeCalendarsAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (now < _nextCalendarRefreshUtc)
            return;

        bool networkAvailable = await _networkProbe.IsInternetAvailableAsync(cancellationToken).ConfigureAwait(false);
        if (networkAvailable)
        {
            IReadOnlyList<ExchangeCalendarRequest> requests = WorldMarkets.Select(static market => new ExchangeCalendarRequest
            {
                CityKey = market.Key,
                ExchangeCode = market.Key switch
                {
                    "NewYork" => "NASDAQ",
                    "London" => "LSE",
                    "Paris" => "EURONEXT",
                    "Tokyo" => "TSE",
                    "HongKong" => "HKEX",
                    "Mumbai" => "NSE",
                    "Sydney" => "ASX",
                    "SaoPaulo" => "B3",
                    _ => market.Exchange
                },
                ExchangeName = market.Exchange,
                ExchangeSymbol = market.Symbol,
                TimeZoneId = market.TimeZone,
                AlternateTimeZoneId = market.TimeZone
            }).ToArray();

            ExchangeCalendarSet calendars = await _exchangeTimingService
                .GetCalendarSetAsync(requests, networkAvailable, cancellationToken)
                .ConfigureAwait(false);
            if (calendars.CalendarsByCityKey.Count > 0)
                _exchangeCalendars.Overlay(calendars);
        }

        _nextCalendarRefreshUtc = now.AddMinutes(10);
    }

    private void ApplyExchangeCalendarStatuses(DateTimeOffset referenceUtc)
    {
        foreach (GlobalMarketViewModel market in GlobalMarkets)
        {
            ExchangeTradingCalendar? calendar = _exchangeCalendars.TryGetByCityKey(market.Key);
            if (calendar is null)
            {
                // The price climbed a little ladder and looked surprised at the view.
                if (ReferenceEquals(market, CalendarReferenceGlobalMarket) && !_hasNewYorkCalendarStatus)
                    ApplyNewYorkFallbackStatus(referenceUtc);
                continue;
            }

            ExchangeCalendarStatus status = _exchangeTimingService.ResolveStatus(calendar, referenceUtc);
            market.ApplyCalendarStatus(status.Session, _exchangeTimingService.FormatCompactStatus(status));
            if (ReferenceEquals(market, CalendarReferenceGlobalMarket))
            {
                _hasNewYorkCalendarStatus = true;
                string countdown = status.HasCountdown ? _exchangeTimingService.FormatCompactStatus(status) : string.Empty;
                ApplyNewYorkStatus(status.Session, countdown, "YFINANCE_CALENDAR");
            }
        }
    }

    private void ApplyNewYorkFallbackStatus(DateTimeOffset referenceUtc)
    {
        MarketSession session = _marketSessionResolver.Resolve(referenceUtc);
        ApplyNewYorkStatus(session, string.Empty, "FALLBACK_CLOCK");
    }

    private void ApplyNewYorkStatus(MarketSession session, string countdown, string source)
    {
        if (session == MarketSession.Unknown)
            return;

        // The price slipped on a banana skin nobody had budgeted for.
        bool transitioned = false;
        if (session != _lastTracedNewYorkSession)
        {
            if (_pendingNewYorkSession != session)
            {
                _pendingNewYorkSession = session;
                _pendingNewYorkSessionObservations = 1;
            }
            else
            {
                _pendingNewYorkSessionObservations++;
            }

            if (_lastTracedNewYorkSession != MarketSession.Unknown && _pendingNewYorkSessionObservations < 2)
            {
                WriteCinematicTrace($"MARKET_STATUS_TRANSITION_PENDING;SOURCE={source};FROM={_lastTracedNewYorkSession};TO={session}");
                return;
            }

            _lastTracedNewYorkSession = session;
            _pendingNewYorkSession = MarketSession.Unknown;
            _pendingNewYorkSessionObservations = 0;
            transitioned = true;
        }

        CalendarReferenceGlobalMarket.ApplyCalendarStatus(session, session switch
        {
            MarketSession.PreMarket => "PRE --",
            MarketSession.Regular => "OPEN --",
            MarketSession.AfterHours => "POST --",
            MarketSession.Closed => "CLOSED --",
            _ => "--"
        });
        string displaySession = session switch
        {
            MarketSession.Regular => "Open",
            MarketSession.PreMarket => "Pre-Market",
            MarketSession.AfterHours => "After Hours",
            MarketSession.Closed => "Closed",
            _ => "--"
        };
        MarketStatusText = countdown.Length > 0
            ? $"Market: New York {displaySession}\n{countdown}"
            : $"Market: New York {displaySession}";
        if (transitioned)
            WriteCinematicTrace($"MARKET_STATUS;SOURCE={source};SESSION={session}");
    }

    private bool IsClosedClockMarketQuoteFresh(string symbol, DateTimeOffset nowUtc)
    {
        if (!GlobalMarkets.Any(market => string.Equals(market.Symbol, symbol, StringComparison.OrdinalIgnoreCase)))
            return false;

        return _latestQuotes.TryGetValue(symbol, out QuoteSnapshot? quote) &&
               quote.MarketSession == MarketSession.Closed &&
               quote.FetchTimestampUtc != DateTimeOffset.MinValue &&
               nowUtc - quote.FetchTimestampUtc < TimeSpan.FromMinutes(10);
    }

    private void ApplyProductionGraphQuote(
        FloatingGraphViewModel graph,
        decimal? last,
        decimal? changePercent,
        bool isStale,
        string source)
    {
        if (_graphMotion?.ApplyQuote(graph, last, changePercent, isStale: isStale) != true)
            return;

        if (!graph.IsRefreshTravelFlashActive)
        {
            WriteCinematicTrace(
                $"GRAPH_REFRESH_CUE;STATE=STARTED;SOURCE={source};MODE=NEUTRAL;SYMBOL={graph.Symbol}");
            return;
        }

        string direction = graph.RefreshTravelDirection < 0 ? "UP" : "DOWN";
        WriteCinematicTrace(
            $"GRAPH_IMPULSE;STATE=STARTED;SOURCE={source};SYMBOL={graph.Symbol};DIRECTION={direction};TARGET=BOUNDARY");
        _productionImpulseSymbols.Add(graph.Symbol);
    }

    private void TraceCompletedProductionImpulses()
    {
        if (_productionImpulseSymbols.Count == 0)
            return;

        foreach (FloatingGraphViewModel graph in Graphs
                     .Where(graph => _productionImpulseSymbols.Contains(graph.Symbol) &&
                                     !graph.IsRefreshTravelFlashActive)
                     .ToArray())
        {
            WriteCinematicTrace(
                $"GRAPH_IMPULSE;STATE=COMPLETED;SOURCE=PRODUCTION_SCENE;SYMBOL={graph.Symbol};TARGET=BOUNDARY");
            _productionImpulseSymbols.Remove(graph.Symbol);
        }
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

    public void NotifyBackgroundImageLoaded(string source, bool layerIsA)
    {
        if (_backgroundCinema?.StartPreparedRotation(source, layerIsA) == true)
            ApplyBackgroundCinemaState();
    }

    public void NotifyBackgroundImageFailed(string source, bool layerIsA)
    {
        if (_backgroundCinema?.CancelPreparedRotation(source, layerIsA) == true)
            ApplyBackgroundCinemaState();
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
            loops = new Task?[]
            {
                _initialQuoteSequence,
                _deferredSceneLoops,
                _renderHeartbeatLoop
            };
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
