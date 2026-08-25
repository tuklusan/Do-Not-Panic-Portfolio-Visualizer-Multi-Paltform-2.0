using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using DoNotPanicPortfolioVisualizer.App.Views;
using DoNotPanicPortfolioVisualizer.App.ViewModels;
using DoNotPanicPortfolioVisualizer.Core;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using DoNotPanicPortfolioVisualizer.Presentation.ViewModels;

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
                    desktop.MainWindow = new ProductShellWindow
                    {
                        DataContext = ProductSceneViewModel.CreateDefault(),
                    };
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
