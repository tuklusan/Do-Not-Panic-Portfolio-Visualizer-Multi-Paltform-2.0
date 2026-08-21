// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Based on original work by Supratim Sanyal of SANYALnet Labs.
// Governed by the SANYALnet Labs Non-Commercial License in the root LICENSE file.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DoNotPanicPortfolioVisualizer.App.ViewModels;
using DoNotPanicPortfolioVisualizer.Presentation.ViewModels;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.App.Views;

public partial class ProductShellWindow : Window
{
    private WindowState _windowStateBeforeFullScreen = WindowState.Maximized;
    private MainWindow? _settingsWindow;
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
            await scene.InitializeAsync();
    }

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
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_shutdownStarted || DataContext is not ProductSceneViewModel scene)
            return;

        e.Cancel = true;
        _shutdownStarted = true;
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
        ToggleFullScreen();
        e.Handled = true;
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

        _settingsWindow = new MainWindow
        {
            DataContext = new MainViewModel(),
        };
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
