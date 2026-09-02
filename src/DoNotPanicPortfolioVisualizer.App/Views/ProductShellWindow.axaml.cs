// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Based on original work by Supratim Sanyal of SANYALnet Labs.
// Governed by the SANYALnet Labs Non-Commercial License in the root LICENSE file.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private const double UpstreamTickerTopOffset = 188d;
    private WindowState _windowStateBeforeFullScreen = WindowState.Maximized;
    private SystemDecorations _systemDecorationsBeforeFullScreen = SystemDecorations.Full;
    private PixelPoint _windowPositionBeforeFullScreen;
    private double _windowWidthBeforeFullScreen;
    private double _windowHeightBeforeFullScreen;
    private bool _isFullScreen;
    private MainWindow? _settingsWindow;
    private readonly BackgroundFrameLoader _backgroundFrameLoader = new();
    private readonly CancellationTokenSource _backgroundLoadCts = new();
    private Task _backgroundLoadA = Task.CompletedTask;
    private Task _backgroundLoadB = Task.CompletedTask;
    private int _backgroundGenerationA;
    private int _backgroundGenerationB;
    private double _backgroundPresentationOpacityA = BackgroundPresentationOpacityPolicy.FallbackOpacity;
    private double _backgroundPresentationOpacityB = BackgroundPresentationOpacityPolicy.FallbackOpacity;
    private bool _shutdownStarted;

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
        _systemDecorationsBeforeFullScreen = SystemDecorations;
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
        SystemDecorations = SystemDecorations.None;
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
        SystemDecorations = _systemDecorationsBeforeFullScreen;
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
            _backgroundLoadA = LoadBackgroundLayerAsync(scene.BackgroundSourceA, BackgroundLayerA, true, ++_backgroundGenerationA);
            _backgroundLoadB = LoadBackgroundLayerAsync(scene.BackgroundSourceB, BackgroundLayerB, false, ++_backgroundGenerationB);
            await Task.WhenAll(_backgroundLoadA, _backgroundLoadB);
        }
    }

    private void ApplyRestoredWindowBounds(Size? requestedSize)
    {
        Screen? screen = Screens.ScreenFromWindow(this);
        if (screen is null)
            return;

        double scale = Math.Max(1d, RenderScaling);
        double availableWidth = Math.Max(1d, (screen.WorkingArea.Width - 24d) / scale);
        double availableHeight = Math.Max(1d, (screen.WorkingArea.Height - 24d) / scale);
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
            // A rotation begins in the scene state before the next bitmap is decoded.
            // Keep a committed frame in the incoming layer during that asynchronous gap.
            if (layer.Source is null)
                layer.Source = isLayerA ? BackgroundLayerB.Source : BackgroundLayerA.Source;

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
                UpdateBackgroundLayerOpacities(scene);
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

        double tickerViewportWidth = Math.Max(1d, e.NewSize.Width - 180d);
        foreach (TickerLaneViewModel lane in scene.Lanes)
            lane.ConfigureViewport(tickerViewportWidth);

        scene.ConfigureGraphViewport(
            Math.Max(1d, e.NewSize.Width - 32d),
            Math.Max(1d, e.NewSize.Height - 22d));
        scene.ConfigureCinematicViewport(e.NewSize.Width);
        PositionTickerLanes(scene);
    }

    private void PositionTickerLanes(ProductSceneViewModel scene)
    {
        double laneHeight = TickerLanesHost.Bounds.Height;
        if (laneHeight <= 0d)
            laneHeight = scene.Lanes.Sum(static lane => lane.RowHeight);

        // The upstream scene reserves a 188-pixel lead-in on roomy displays.  On
        // compact working areas, center the four live lanes in their middle region
        // instead of letting that fixed offset push them into the lower overlays.
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
        scene.RenderSurfaceRecoveryRequested -= OnRenderSurfaceRecoveryRequested;
        scene.PropertyChanged -= OnScenePropertyChanged;
        _backgroundLoadCts.Cancel();
        try
        {
            await Task.WhenAll(_backgroundLoadA, _backgroundLoadB);
        }
        catch (OperationCanceledException)
        {
        }
        _backgroundFrameLoader.Dispose();
        _backgroundLoadCts.Dispose();
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

        if (DataContext is ProductSceneViewModel scene)
            scene.PauseCinematicPlayback();

        MainViewModel configuration = new();
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
