using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Diagnostics;
using DoNotPanicPortfolioVisualizer.App.Views;
using DoNotPanicPortfolioVisualizer.App.ViewModels;
using DoNotPanicPortfolioVisualizer.Core;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using DoNotPanicPortfolioVisualizer.Presentation.ViewModels;
using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.App;

public partial class App : Application
{
    private SingleInstanceLease? _singleInstanceLease;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            bool configurationValidationMode = string.Equals(
                Environment.GetEnvironmentVariable("DNPPV_CONFIGURATION_VALIDATION_MODE"),
                "1",
                StringComparison.Ordinal);
            string[] startupArguments = Environment.GetCommandLineArgs();
            bool startFullScreen = StartupOptions.RequestsFullScreen(startupArguments);
            bool startsWindowed = StartupOptions.TryGetWindowedStartupSize(startupArguments, out StartupWindowSize windowedSize);
            LocalDataPaths localDataPaths = LocalDataRootResolver.ResolveForCurrentPlatform();
            string lockFileName = SingleInstanceLease.ResolveLockFileName(
                AppIdentity.DesktopSingleInstanceLockFileName,
                OperatingSystem.IsWindows(),
                OperatingSystem.IsWindows() ? Process.GetCurrentProcess().SessionId : 0);
            if (!configurationValidationMode && !SingleInstanceLease.TryAcquire(
                    Path.Combine(localDataPaths.Root, lockFileName),
                    out _singleInstanceLease))
            {
                desktop.MainWindow = new DuplicateInstanceWindow();
                base.OnFrameworkInitializationCompleted();
                return;
            }

            try
            {
                if (configurationValidationMode)
                {
                    desktop.MainWindow = new MainWindow { DataContext = new MainViewModel() };
                }
                else
                {
                    ProductShellWindow shell = new()
                    {
                        DataContext = ProductSceneViewModel.CreateDefault(),
                    };
                    if (startsWindowed)
                    {
                        shell.WindowState = WindowState.Normal;
                        shell.Width = windowedSize.Width;
                        shell.Height = windowedSize.Height;
                    }
                    if (startFullScreen)
                    {
                        EventHandler? enterFullScreenAfterOpen = null;
                        enterFullScreenAfterOpen = (_, _) =>
                        {
                            shell.Opened -= enterFullScreenAfterOpen;
                            Dispatcher.UIThread.Post(
                                () =>
                                {
                                    if (shell.IsVisible)
                                        shell.EnterFullScreen();
                                },
                                DispatcherPriority.ApplicationIdle);
                        };
                        shell.Opened += enterFullScreenAfterOpen;
                    }
                    desktop.MainWindow = shell;
                }
                if (!configurationValidationMode)
                    desktop.Exit += (_, _) => ReleaseSingleInstanceLease();
            }
            catch
            {
                ReleaseSingleInstanceLease();
                throw;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ReleaseSingleInstanceLease()
    {
        Interlocked.Exchange(ref _singleInstanceLease, null)?.Dispose();
    }
}
