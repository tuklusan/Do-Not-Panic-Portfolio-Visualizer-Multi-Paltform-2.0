// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Based on original work by Supratim Sanyal of SANYALnet Labs.
// Governed by the SANYALnet Labs Non-Commercial License in the root LICENSE file.

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DoNotPanicPortfolioVisualizer.App.ViewModels;
using DoNotPanicPortfolioVisualizer.App.Services;
using DoNotPanicPortfolioVisualizer.Presentation.Services;
using DoNotPanicPortfolioVisualizer.Presentation.ViewModels;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.App.Views;

public partial class ProductShellWindow : Window
{
    private const double UpstreamTickerTopOffset = 188d;
    private WindowState _windowStateBeforeFullScreen = WindowState.Maximized;
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

    public bool IsFullScreen => WindowState == WindowState.FullScreen;

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
        MainMenu.IsVisible = false;
        WindowState = WindowState.FullScreen;
    }

    public void ExitFullScreen()
    {
        if (!IsFullScreen)
            return;

        WindowState = _windowStateBeforeFullScreen;
        MainMenu.IsVisible = true;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
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
                UpdateBackgroundLayerOpacities(scene);
            WriteBackgroundTrace($"BACKGROUND;SIGNAL=COMMITTED;LAYER={(isLayerA ? "A" : "B")};SOURCE={source};PRESENTATION_OPACITY={frame.PresentationOpacity:0.00}");
        }
        catch (OperationCanceledException) when (_backgroundLoadCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            WriteBackgroundTrace($"BACKGROUND;SIGNAL=RETAINED;LAYER={(isLayerA ? "A" : "B")};SOURCE={source};ERROR={ex.GetType().Name}");
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

    private static void WriteBackgroundTrace(string message)
    {
        string? path = Environment.GetEnvironmentVariable("DNPPV_CINEMATIC_TRACE");
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O};{message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

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
        Window dialog = new()
        {
            Title = "About DO NOT PANIC PORTFOLIO VISUALIZER 2.0",
            Width = 520,
            Height = 230,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "DO NOT PANIC PORTFOLIO VISUALIZER 2.0", FontSize = 20 },
                    new TextBlock { Text = "Cross-platform Avalonia migration under development." },
                    new TextBlock { Text = "Based on original work by Supratim Sanyal of SANYALnet Labs.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right },
                },
            },
        };

        Button closeButton = (Button)((StackPanel)dialog.Content).Children[^1];
        closeButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}
