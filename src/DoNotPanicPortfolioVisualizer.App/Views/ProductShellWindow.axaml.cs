// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Based on original work by Supratim Sanyal of SANYALnet Labs.
// Governed by the SANYALnet Labs Non-Commercial License in the root LICENSE file.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using DoNotPanicPortfolioVisualizer.App.ViewModels;
using DoNotPanicPortfolioVisualizer.App.Services;
using DoNotPanicPortfolioVisualizer.Presentation.Services;
using DoNotPanicPortfolioVisualizer.Presentation.ViewModels;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;

namespace DoNotPanicPortfolioVisualizer.App.Views;

public partial class ProductShellWindow : Window
{
    private const double RestoredWindowWidth = 1180d;
    private const double RestoredWindowHeight = 720d;
    private const double WindowChromeWidthAllowancePixels = 48d;
    private const double WindowChromeHeightAllowancePixels = 96d;
    private const double UpstreamTickerTopOffset = 188d;
    private WindowState _windowStateBeforeFullScreen = WindowState.Maximized;
    private WindowDecorations _windowDecorationsBeforeFullScreen = WindowDecorations.Full;
    private PixelPoint _windowPositionBeforeFullScreen;
    private double _windowWidthBeforeFullScreen;
    private double _windowHeightBeforeFullScreen;
    private bool _isFullScreen;
    private MainWindow? _settingsWindow;
    private readonly BackgroundFrameLoader _backgroundFrameLoader = new();
    private readonly CancellationTokenSource _backgroundLoadCts = new();
    private CancellationTokenSource? _captureCts;
    private Task _captureTask = Task.CompletedTask;
    private Task _backgroundLoadA = Task.CompletedTask;
    private Task _backgroundLoadB = Task.CompletedTask;
    private int _backgroundGenerationA;
    private int _backgroundGenerationB;
    private double _backgroundPresentationOpacityA = BackgroundPresentationOpacityPolicy.FallbackOpacity;
    private double _backgroundPresentationOpacityB = BackgroundPresentationOpacityPolicy.FallbackOpacity;
    private bool _shutdownStarted;
    private bool _cinematicFramesActive;
    private TimeSpan? _lastTopDialFrame;
    private TimeSpan? _lastTopLeftStatusFrame;
    private TimeSpan? _lastTickerLaneFrame;
    private TimeSpan? _lastGraphCardFrame;
    private TimeSpan? _lastWorldMarketTapeFrame;
    private TimeSpan? _lastFinanceNewsFrame;
    private TimeSpan? _lastBackgroundCinemaFrame;
    private DateTimeOffset _lastTopDialFrameGapTraceUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastTopLeftStatusFrameGapTraceUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastTickerLaneFrameGapTraceUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastGraphCardFrameGapTraceUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastWorldMarketTapeFrameGapTraceUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastFinanceNewsFrameGapTraceUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastBackgroundCinemaFrameGapTraceUtc = DateTimeOffset.MinValue;
    private AudioPlayerViewModel? _radioPlayer;

    public ProductShellWindow()
    {
        InitializeComponent();
    }

    public bool IsFullScreen => _isFullScreen;

    public Size? WindowedStartupSize { get; set; }

    public void ToggleFullScreen()
    {
        if (IsFullScreen)
        {
            ExitFullScreen();
            return;
        }

        EnterFullScreen();
    }

    public void EnterFullScreen()
    {
        if (IsFullScreen)
            return;

        _windowStateBeforeFullScreen = WindowState;
        _windowDecorationsBeforeFullScreen = WindowDecorations;
        _windowPositionBeforeFullScreen = Position;
        _windowWidthBeforeFullScreen = Width;
        _windowHeightBeforeFullScreen = Height;
        MainMenu.IsVisible = false;
        _isFullScreen = true;

        Screen? screen = Screens.ScreenFromWindow(this);
        if (screen is null)
        {
            WindowState = WindowState.FullScreen;
            return;
        }

        double scale = Math.Max(1d, RenderScaling);
        PixelRect bounds = screen.Bounds;
        WindowState = WindowState.Normal;
        WindowDecorations = WindowDecorations.None;
        MaxWidth = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;
        Position = new PixelPoint(bounds.X, bounds.Y);
        Width = bounds.Width / scale;
        Height = bounds.Height / scale;
        TraceLog.InfoState("ProductShell", "FullScreenBoundsApplied", [
            new("monitor", bounds),
            new("scale", scale),
            new("width", Width),
            new("height", Height)
        ]);
    }

    public void ExitFullScreen()
    {
        if (!IsFullScreen)
            return;

        _isFullScreen = false;
        MainMenu.IsVisible = true;
        WindowDecorations = _windowDecorationsBeforeFullScreen;
        Position = _windowPositionBeforeFullScreen;
        Width = _windowWidthBeforeFullScreen;
        Height = _windowHeightBeforeFullScreen;
        WindowState = _windowStateBeforeFullScreen;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        ApplyRestoredWindowBounds(WindowedStartupSize);

        if (DataContext is ProductSceneViewModel scene)
        {
            scene.RenderSurfaceRecoveryRequested += OnRenderSurfaceRecoveryRequested;
            scene.PropertyChanged += OnScenePropertyChanged;
            await scene.InitializeAsync();
            StartCinematicAnimationFrames();
            _backgroundLoadA = LoadBackgroundLayerAsync(scene.BackgroundSourceA, BackgroundLayerA, true, ++_backgroundGenerationA);
            _backgroundLoadB = LoadBackgroundLayerAsync(scene.BackgroundSourceB, BackgroundLayerB, false, ++_backgroundGenerationB);
            await Task.WhenAll(_backgroundLoadA, _backgroundLoadB);
        }

        _radioPlayer = AudioPlayerViewModel.TryCreate();
        if (_radioPlayer is not null)
        {
            RadioControls.DataContext = _radioPlayer;
            RadioControls.IsVisible = true;
            _radioPlayer.StartAutomatically();
        }

        string? capturePath = Environment.GetEnvironmentVariable("DNPPV_PRODUCT_CAPTURE_PATH");
        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            int captureIntervalMinutes = ParseCaptureIntervalMinutes();
            _captureCts = new CancellationTokenSource();
            _captureTask = CaptureProductWindowAsync(capturePath, captureIntervalMinutes, _captureCts.Token);
        }
    }

    private void StartCinematicAnimationFrames()
    {
        _cinematicFramesActive = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnTopDialAnimationFrame);
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnTopLeftStatusAnimationFrame);
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnTickerLaneAnimationFrame);
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnGraphCardAnimationFrame);
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnWorldMarketTapeAnimationFrame);
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFinanceNewsAnimationFrame);
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnBackgroundCinemaAnimationFrame);
    }

    private void OnTopDialAnimationFrame(TimeSpan timestamp)
    {
        if (!_cinematicFramesActive || DataContext is not ProductSceneViewModel scene)
            return;
        TimeSpan elapsed = GetFrameElapsed(timestamp, ref _lastTopDialFrame);
        TraceAnimationFrameGap("top-dials", elapsed, ref _lastTopDialFrameGapTraceUtc);
        try { scene.AdvanceMacroDialFrame(elapsed); }
        catch (Exception exception) { TraceAnimationFrameFailure("top-dials", exception); }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnTopDialAnimationFrame);
    }

    private void OnTopLeftStatusAnimationFrame(TimeSpan timestamp)
    {
        if (!_cinematicFramesActive || DataContext is not ProductSceneViewModel scene)
            return;
        TimeSpan elapsed = GetFrameElapsed(timestamp, ref _lastTopLeftStatusFrame);
        TraceAnimationFrameGap("top-left-status", elapsed, ref _lastTopLeftStatusFrameGapTraceUtc);
        try { scene.AdvanceTopLeftStatusFrame(elapsed); }
        catch (Exception exception) { TraceAnimationFrameFailure("top-left-status", exception); }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnTopLeftStatusAnimationFrame);
    }

    private void OnTickerLaneAnimationFrame(TimeSpan timestamp)
    {
        if (!_cinematicFramesActive || DataContext is not ProductSceneViewModel scene)
            return;
        TimeSpan elapsed = GetFrameElapsed(timestamp, ref _lastTickerLaneFrame);
        TraceAnimationFrameGap("main-ticker-lanes", elapsed, ref _lastTickerLaneFrameGapTraceUtc);
        try { scene.AdvanceTickerLaneFrame(elapsed); }
        catch (Exception exception) { TraceAnimationFrameFailure("main-ticker-lanes", exception); }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnTickerLaneAnimationFrame);
    }

    private void OnGraphCardAnimationFrame(TimeSpan timestamp)
    {
        if (!_cinematicFramesActive || DataContext is not ProductSceneViewModel scene)
            return;
        TimeSpan elapsed = GetFrameElapsed(timestamp, ref _lastGraphCardFrame);
        TraceAnimationFrameGap("graph-cards", elapsed, ref _lastGraphCardFrameGapTraceUtc);
        try { scene.AdvanceGraphCardFrame(elapsed); }
        catch (Exception exception) { TraceAnimationFrameFailure("graph-cards", exception); }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnGraphCardAnimationFrame);
    }

    private void OnWorldMarketTapeAnimationFrame(TimeSpan timestamp)
    {
        if (!_cinematicFramesActive || DataContext is not ProductSceneViewModel scene)
            return;
        TimeSpan elapsed = GetFrameElapsed(timestamp, ref _lastWorldMarketTapeFrame);
        TraceAnimationFrameGap("world-market-tape", elapsed, ref _lastWorldMarketTapeFrameGapTraceUtc);
        try { scene.AdvanceWorldMarketTapeFrame(elapsed); }
        catch (Exception exception) { TraceAnimationFrameFailure("world-market-tape", exception); }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnWorldMarketTapeAnimationFrame);
    }

    private void OnFinanceNewsAnimationFrame(TimeSpan timestamp)
    {
        if (!_cinematicFramesActive || DataContext is not ProductSceneViewModel scene)
            return;
        TimeSpan elapsed = GetFrameElapsed(timestamp, ref _lastFinanceNewsFrame);
        TraceAnimationFrameGap("finance-news", elapsed, ref _lastFinanceNewsFrameGapTraceUtc);
        try { scene.AdvanceFinanceNewsFrame(elapsed); }
        catch (Exception exception) { TraceAnimationFrameFailure("finance-news", exception); }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFinanceNewsAnimationFrame);
    }

    private void OnBackgroundCinemaAnimationFrame(TimeSpan timestamp)
    {
        if (!_cinematicFramesActive || DataContext is not ProductSceneViewModel scene)
            return;
        TimeSpan elapsed = GetFrameElapsed(timestamp, ref _lastBackgroundCinemaFrame);
        TraceAnimationFrameGap("background-cinema", elapsed, ref _lastBackgroundCinemaFrameGapTraceUtc);
        try { scene.AdvanceBackgroundCinemaFrame(elapsed); }
        catch (Exception exception) { TraceAnimationFrameFailure("background-cinema", exception); }
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnBackgroundCinemaAnimationFrame);
    }

    private static void TraceAnimationFrameFailure(string component, Exception exception)
        => TraceLog.WarnState("ProductShell", "CinematicFrameFailed", [
            new("component", component),
            new("exception_type", exception.GetType().Name)
        ]);

    private static void TraceAnimationFrameGap(string component, TimeSpan elapsed, ref DateTimeOffset lastTraceUtc)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (elapsed < TimeSpan.FromMilliseconds(50) || now - lastTraceUtc < TimeSpan.FromSeconds(1))
            return;

        lastTraceUtc = now;
        TraceLog.InfoState("ProductScene.Cinematic", "FrameGap", [
            new("component", component),
            new("elapsed_ms", elapsed.TotalMilliseconds)
        ]);
    }

    private static TimeSpan GetFrameElapsed(TimeSpan timestamp, ref TimeSpan? previousTimestamp)
    {
        TimeSpan elapsed = previousTimestamp.HasValue
            ? timestamp - previousTimestamp.Value
            : TimeSpan.Zero;
        previousTimestamp = timestamp;
        return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
    }

    private static int ParseCaptureIntervalMinutes()
    {
        string? value = Environment.GetEnvironmentVariable("DNPPV_PRODUCT_CAPTURE_INTERVAL_MINUTES");
        return int.TryParse(value, out int interval) && interval > 0 ? interval : 0;
    }

    private async Task CaptureProductWindowAsync(string capturePath, int intervalMinutes, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(true);
            do
            {
                if (!IsVisible || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                    throw new InvalidOperationException("Product window is not visible at capture time.");

                string outputPath = intervalMinutes > 0
                    ? Path.Combine(capturePath, $"product-scene-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.png")
                    : capturePath;
                string? directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                double scale = Math.Max(1d, RenderScaling);
                PixelSize pixelSize = new(
                    (int)Math.Ceiling(ClientSize.Width * scale),
                    (int)Math.Ceiling(ClientSize.Height * scale));
                using RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d * scale, 96d * scale));
                bitmap.Render(this);
                bitmap.Save(outputPath, PngBitmapEncoderOptions.Default);
                TraceLog.InfoState("ProductShell", "ProductWindowCapture", [
                    new("path", outputPath),
                    new("width", pixelSize.Width),
                    new("height", pixelSize.Height)
                ]);

                if (intervalMinutes <= 0)
                    break;
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), cancellationToken).ConfigureAwait(true);
            }
            while (!cancellationToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            TraceLog.ErrorState("ProductShell", "ProductWindowCaptureFailed", [], ex);
        }
    }

    private void ApplyRestoredWindowBounds(Size? requestedSize)
    {
        Screen? screen = Screens.ScreenFromWindow(this);
        if (screen is null)
            return;

        double scale = Math.Max(1d, RenderScaling);
        double availableWidth = Math.Max(1d, (screen.WorkingArea.Width - WindowChromeWidthAllowancePixels) / scale);
        double availableHeight = Math.Max(1d, (screen.WorkingArea.Height - WindowChromeHeightAllowancePixels) / scale);
        Size target = requestedSize ?? new Size(RestoredWindowWidth, RestoredWindowHeight);

        WindowState = WindowState.Normal;
        MaxWidth = availableWidth;
        MaxHeight = availableHeight;
        Width = Math.Clamp(target.Width, Math.Min(MinWidth, availableWidth), availableWidth);
        Height = Math.Clamp(target.Height, Math.Min(MinHeight, availableHeight), availableHeight);
        TraceLog.InfoState("ProductShell", "WindowedBoundsApplied", [
            new("requested_width", target.Width),
            new("requested_height", target.Height),
            new("available_width", availableWidth),
            new("available_height", availableHeight),
            new("width", Width),
            new("height", Height),
            new("working_area", screen.WorkingArea)
        ]);
        if (requestedSize is not null)
        {
            TraceLog.InfoState("ProductShell", "WindowedStartupApplied", [
                new("state", WindowState),
                new("width", Width),
                new("height", Height),
                new("client_width", ClientSize.Width),
                new("client_height", ClientSize.Height)
            ]);
        }
    }

    private void OnScenePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ProductSceneViewModel scene)
            return;

        if (e.PropertyName == nameof(ProductSceneViewModel.BackgroundSourceA))
            _backgroundLoadA = LoadBackgroundLayerAsync(scene.BackgroundSourceA, BackgroundLayerA, true, ++_backgroundGenerationA);
        else if (e.PropertyName == nameof(ProductSceneViewModel.BackgroundSourceB))
            _backgroundLoadB = LoadBackgroundLayerAsync(scene.BackgroundSourceB, BackgroundLayerB, false, ++_backgroundGenerationB);

        if (e.PropertyName is nameof(ProductSceneViewModel.BackgroundOpacityA) or
            nameof(ProductSceneViewModel.BackgroundOpacityB))
        {
            UpdateBackgroundLayerOpacities(scene);
        }
    }

    private async Task LoadBackgroundLayerAsync(string? source, Image layer, bool isLayerA, int generation)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;

        try
        {
            BackgroundFrame frame = await _backgroundFrameLoader.LoadAsync(source, _backgroundLoadCts.Token);
            if (_backgroundLoadCts.IsCancellationRequested ||
                generation != (isLayerA ? _backgroundGenerationA : _backgroundGenerationB))
            {
                return;
            }

            layer.Source = frame.Bitmap;
            if (isLayerA)
                _backgroundPresentationOpacityA = frame.PresentationOpacity;
            else
                _backgroundPresentationOpacityB = frame.PresentationOpacity;

            if (DataContext is ProductSceneViewModel scene)
            {
                UpdateBackgroundLayerOpacities(scene);
                scene.NotifyBackgroundImageLoaded(source, isLayerA);
            }
            TraceLog.InfoState("ProductShell", "BackgroundCommitted", [
                new("layer", isLayerA ? "A" : "B"),
                new("source", source),
                new("presentation_opacity", frame.PresentationOpacity)
            ]);
        }
        catch (OperationCanceledException) when (_backgroundLoadCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (generation == (isLayerA ? _backgroundGenerationA : _backgroundGenerationB) &&
                DataContext is ProductSceneViewModel scene)
            {
                scene.NotifyBackgroundImageFailed(source, isLayerA);
            }
            TraceLog.WarnState("ProductShell", "BackgroundRetained", [
                new("layer", isLayerA ? "A" : "B"),
                new("source", source),
                new("error", ex.GetType().Name)
            ]);
        }
    }

    private void UpdateBackgroundLayerOpacities(ProductSceneViewModel scene)
    {
        BackgroundLayerA.Opacity = ScaleBackgroundOpacity(scene.BackgroundOpacityA, _backgroundPresentationOpacityA);
        BackgroundLayerB.Opacity = ScaleBackgroundOpacity(scene.BackgroundOpacityB, _backgroundPresentationOpacityB);
    }

    private static double ScaleBackgroundOpacity(double transitionOpacity, double presentationOpacity)
        => Math.Clamp(
            presentationOpacity * transitionOpacity / BackgroundPresentationOpacityPolicy.FallbackOpacity,
            0d,
            1d);

    private void OnRenderSurfaceRecoveryRequested() => SceneRoot.InvalidateVisual();

    private void OnSceneRootSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is not ProductSceneViewModel scene)
            return;

        ConfigureSceneViewports(scene, e.NewSize.Width, e.NewSize.Height);
        PositionTickerLanes(scene);
    }

    private void ConfigureSceneViewports(ProductSceneViewModel scene, double width, double height)
    {
        scene.ConfigureTickerLaneViewport(width - 32d);
        scene.ConfigureGraphViewport(width - 32d, height - 22d);
        scene.ConfigureCinematicViewport(width);
    }

    private void PositionTickerLanes(ProductSceneViewModel scene)
    {
        double laneHeight = TickerLanesHost.Bounds.Height;
        if (laneHeight <= 0d)
            laneHeight = scene.Lanes.Sum(static lane => lane.RowHeight);

        // A falling price is arithmetic arriving before the tea.
        double laneRowTop = TickerLanesHost.Bounds.Y - TickerLanesHost.Margin.Top;
        double laneRowHeight = GlobalMarketsHost.Bounds.Y - laneRowTop;
        if (laneRowHeight <= 0d || laneHeight <= 0d)
            return;

        double topOffset = Math.Min(
            UpstreamTickerTopOffset,
            Math.Max(0d, (laneRowHeight - laneHeight) / 2d));
        if (Math.Abs(TickerLanesHost.Margin.Top - topOffset) <= 0.5d)
            return;

        TickerLanesHost.Margin = new Thickness(8d, topOffset, 8d, 0d);
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_shutdownStarted || DataContext is not ProductSceneViewModel scene)
            return;

        e.Cancel = true;
        _shutdownStarted = true;
        _cinematicFramesActive = false;
        scene.RenderSurfaceRecoveryRequested -= OnRenderSurfaceRecoveryRequested;
        scene.PropertyChanged -= OnScenePropertyChanged;
        _backgroundLoadCts.Cancel();
        _captureCts?.Cancel();
        if (_radioPlayer is not null)
        {
            await _radioPlayer.DisposeAsync();
            _radioPlayer = null;
        }
        try
        {
            await Task.WhenAll(_backgroundLoadA, _backgroundLoadB);
        }
        catch (OperationCanceledException)
        {
        }
        try
        {
            await _captureTask;
        }
        catch (OperationCanceledException)
        {
        }
        // The forecast was wrong with admirable confidence.
        BackgroundLayerA.Source = null;
        BackgroundLayerB.Source = null;
        _backgroundFrameLoader.Dispose();
        _backgroundLoadCts.Dispose();
        _captureCts?.Dispose();
        await scene.DisposeAsync();
        Close();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && IsFullScreen)
        {
            ExitFullScreen();
            e.Handled = true;
        }
    }

    private void OnWindowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (IsInteractiveInputTarget(e.Source))
            return;

        ToggleFullScreen();
        e.Handled = true;
    }

    private static bool IsInteractiveInputTarget(object? source)
    {
        for (Visual? current = source as Visual; current is not null; current = current.GetVisualParent())
        {
            if (current is Menu or MenuItem or Button or TextBox or
                SelectingItemsControl or Slider or ScrollBar or ToggleButton)
            {
                return true;
            }
        }

        return false;
    }

    private void OnFullScreenClick(object? sender, RoutedEventArgs e) => ToggleFullScreen();

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        if (DataContext is not ProductSceneViewModel scene)
            return;

        scene.PauseCinematicPlayback();

        MainViewModel configuration = new();
        configuration.SettingsApplied += () =>
        {
            scene.ReloadSettingsFromDisk();
            ConfigureSceneViewports(scene, SceneRoot.Bounds.Width, SceneRoot.Bounds.Height);
            PositionTickerLanes(scene);
        };
        _settingsWindow = new MainWindow
        {
            DataContext = configuration,
        };
        configuration.CloseRequested += () => _settingsWindow?.Close();
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            if (DataContext is ProductSceneViewModel activeScene)
                activeScene.ResumeCinematicPlayback();
        };
        _settingsWindow.Show(this);
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        AboutWindow dialog = new();
        await dialog.ShowDialog(this);
    }
}
