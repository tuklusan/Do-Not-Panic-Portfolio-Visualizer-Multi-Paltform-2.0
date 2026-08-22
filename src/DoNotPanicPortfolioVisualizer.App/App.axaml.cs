using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using DoNotPanicPortfolioVisualizer.App.Views;
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
            LocalDataPaths localDataPaths = LocalDataRootResolver.ResolveForCurrentPlatform();
            string lockFileName = SingleInstanceLease.ResolveLockFileName(
                AppIdentity.DesktopSingleInstanceLockFileName,
                OperatingSystem.IsWindows(),
                OperatingSystem.IsWindows() ? Process.GetCurrentProcess().SessionId : 0);
            if (!SingleInstanceLease.TryAcquire(
                    Path.Combine(localDataPaths.Root, lockFileName),
                    out _singleInstanceLease))
            {
                desktop.MainWindow = new DuplicateInstanceWindow();
                base.OnFrameworkInitializationCompleted();
                return;
            }

            try
            {
                desktop.MainWindow = new ProductShellWindow
                {
                    DataContext = ProductSceneViewModel.CreateDefault(),
                };
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
