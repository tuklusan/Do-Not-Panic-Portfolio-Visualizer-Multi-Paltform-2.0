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
            string[] startupArguments = desktop.Args ?? Environment.GetCommandLineArgs();
            bool startFullScreen = StartupOptions.RequestsFullScreen(startupArguments);
            bool startsWindowed = StartupOptions.TryGetWindowedStartupSize(startupArguments, out StartupWindowSize windowedSize);
            WriteStartupTrace(startupArguments, startsWindowed, startFullScreen);
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
                        shell.WindowedStartupSize = new Size(windowedSize.Width, windowedSize.Height);
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

    private static void WriteStartupTrace(IEnumerable<string> arguments, bool startsWindowed, bool startsFullScreen)
    {
        string? path = Environment.GetEnvironmentVariable("DNPPV_CINEMATIC_TRACE");
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            string serializedArguments = string.Join("|", arguments.Select(static argument => argument.Replace("|", "%7C", StringComparison.Ordinal)));
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O};STARTUP;WINDOWED={startsWindowed};FULLSCREEN={startsFullScreen};ARGS={serializedArguments}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
