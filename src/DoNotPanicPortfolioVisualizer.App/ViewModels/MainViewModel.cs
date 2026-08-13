using DoNotPanicPortfolioVisualizer.Shared;

namespace DoNotPanicPortfolioVisualizer.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public string ProductTitle => PortfolioVersion.DisplayName;
    public string StageTitle => "Avalonia Migration Baseline";
    public string StatusLine => "Phase 1 / CR-004 portable foundation";
    public string RuntimeLine => ".NET 10 + Avalonia 12.1.1";
}
