using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DoNotPanicPortfolioVisualizer.App.Views;
using DoNotPanicPortfolioVisualizer.Core;
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
            if (!SingleInstanceLease.TryAcquireForCurrentUser(
                    AppIdentity.DesktopSingleInstanceName,
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
