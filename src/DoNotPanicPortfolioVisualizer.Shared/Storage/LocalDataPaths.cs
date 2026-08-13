using DoNotPanicPortfolioVisualizer.Core;

namespace DoNotPanicPortfolioVisualizer.Shared.Storage;

public sealed record LocalDataPaths(
    DesktopPlatformKind Platform,
    string Root,
    string DataRoot,
    string CacheRoot,
    string HistoricalCacheRoot,
    string LogRoot,
    string SecretRoot);

