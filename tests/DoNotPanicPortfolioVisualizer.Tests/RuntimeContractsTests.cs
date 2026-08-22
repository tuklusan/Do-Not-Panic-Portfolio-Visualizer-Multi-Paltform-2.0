using DoNotPanicPortfolioVisualizer.Core;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using YFinance.NET.Protocol.Constants;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class RuntimeContractsTests
{
    [Fact]
    public void SingleInstanceLease_BlocksDuplicateAndAllowsAcquireAfterRelease()
    {
        string name = $"{AppIdentity.DesktopSingleInstanceName}.Test.{Guid.NewGuid():N}";

        Assert.True(SingleInstanceLease.TryAcquire(name, out SingleInstanceLease? first));
        Assert.NotNull(first);
        bool duplicateAcquired = Task.Run(() =>
        {
            bool acquired = SingleInstanceLease.TryAcquire(name, out SingleInstanceLease? duplicate);
            duplicate?.Dispose();
            return acquired;
        }).GetAwaiter().GetResult();
        Assert.False(duplicateAcquired);

        first.Dispose();

        Assert.True(SingleInstanceLease.TryAcquire(name, out SingleInstanceLease? reacquired));
        reacquired!.Dispose();
    }

    [Fact]
    public void SingleInstanceIdentity_IsDistinctFromUpstream10()
    {
        Assert.Equal("DoNotPanicPortfolioVisualizer2.Desktop", AppIdentity.DesktopSingleInstanceName);
        Assert.DoesNotContain("PortfolioSaver.Desktop", AppIdentity.DesktopSingleInstanceName, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleInstanceIdentity_IsSessionScopedOnWindowsAndUserScopedElsewhere()
    {
        string windowsName = SingleInstanceLease.ResolvePlatformName(
            AppIdentity.DesktopSingleInstanceName,
            isWindows: true,
            userName: "tester");
        string unixNameA = SingleInstanceLease.ResolvePlatformName(
            AppIdentity.DesktopSingleInstanceName,
            isWindows: false,
            userName: "tester-a");
        string unixNameB = SingleInstanceLease.ResolvePlatformName(
            AppIdentity.DesktopSingleInstanceName,
            isWindows: false,
            userName: "tester-b");

        Assert.StartsWith($"Local\\{AppIdentity.DesktopSingleInstanceName}.", windowsName, StringComparison.Ordinal);
        Assert.StartsWith(AppIdentity.DesktopSingleInstanceName + ".", unixNameA, StringComparison.Ordinal);
        Assert.NotEqual(unixNameA, unixNameB);
    }

    [Fact]
    public void ResolveFirstOverride_PrefersCurrentOverrideOverDeprecatedAliases()
    {
        Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase)
        {
            [AppIdentity.LocalDataRootOverrideEnvironmentVariable] = @"D:\DNPPV2",
            [AppIdentity.DeprecatedLocalDataRootOverrideEnvironmentVariable] = @"D:\OldProduct",
            [AppIdentity.DeprecatedPortfolioSaverLocalDataRootOverrideEnvironmentVariable] = @"D:\PortfolioSaver"
        };

        string? resolved = LocalDataRootResolver.ResolveFirstOverride(DesktopPlatformKind.Windows, name => values.GetValueOrDefault(name));

        Assert.Equal(@"D:\DNPPV2", resolved);
    }

    [Fact]
    public void ResolveFirstOverride_RejectsRelativeOverrides()
    {
        Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase)
        {
            [AppIdentity.LocalDataRootOverrideEnvironmentVariable] = @"..\DNPPV2"
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => LocalDataRootResolver.ResolveFirstOverride(DesktopPlatformKind.Windows, name => values.GetValueOrDefault(name)));

        Assert.Contains("absolute", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveFirstOverride_NormalizesAbsoluteDotSegments()
    {
        Dictionary<string, string?> values = new(StringComparer.OrdinalIgnoreCase)
        {
            [AppIdentity.LocalDataRootOverrideEnvironmentVariable] = @"D:\Workspace\..\DNPPV2\.\CacheRoot"
        };

        string? resolved = LocalDataRootResolver.ResolveFirstOverride(
            DesktopPlatformKind.Windows,
            name => values.GetValueOrDefault(name));

        Assert.Equal(@"D:\DNPPV2\CacheRoot", resolved);
    }

    [Fact]
    public void Resolve_Windows_DefaultsUnderLocalAppData()
    {
        LocalDataPaths paths = LocalDataRootResolver.Resolve(
            DesktopPlatformKind.Windows,
            environmentLookup: _ => null,
            windowsLocalAppData: @"C:\Users\Tester\AppData\Local",
            createDirectories: false);

        Assert.Equal(@"C:\Users\Tester\AppData\Local\DoNotPanicPortfolioVisualizer2", paths.Root);
        Assert.Equal(@"C:\Users\Tester\AppData\Local\DoNotPanicPortfolioVisualizer2\Data", paths.DataRoot);
        Assert.Equal(@"C:\Users\Tester\AppData\Local\DoNotPanicPortfolioVisualizer2\Caches", paths.CacheRoot);
        Assert.Equal(@"C:\Users\Tester\AppData\Local\DoNotPanicPortfolioVisualizer2\Caches\History", paths.HistoricalCacheRoot);
        Assert.Equal(@"C:\Users\Tester\AppData\Local\DoNotPanicPortfolioVisualizer2\Logs", paths.LogRoot);
        Assert.Equal(@"C:\Users\Tester\AppData\Local\DoNotPanicPortfolioVisualizer2\Secrets", paths.SecretRoot);
    }

    [Fact]
    public void Resolve_Linux_PrefersXdgDataHome()
    {
        LocalDataPaths paths = LocalDataRootResolver.Resolve(
            DesktopPlatformKind.Linux,
            environmentLookup: name => string.Equals(name, "XDG_DATA_HOME", StringComparison.Ordinal) ? "/home/tester/.xdg-data" : null,
            userHomeDirectory: "/home/tester",
            createDirectories: false);

        Assert.Equal("/home/tester/.xdg-data/DoNotPanicPortfolioVisualizer2", paths.Root);
        Assert.Equal("/home/tester/.xdg-data/DoNotPanicPortfolioVisualizer2/Caches/History", paths.HistoricalCacheRoot);
    }

    [Fact]
    public void Resolve_Linux_FallsBackToDotLocalShare()
    {
        LocalDataPaths paths = LocalDataRootResolver.Resolve(
            DesktopPlatformKind.Linux,
            environmentLookup: _ => null,
            userHomeDirectory: "/home/tester",
            createDirectories: false);

        Assert.Equal("/home/tester/.local/share/DoNotPanicPortfolioVisualizer2", paths.Root);
        Assert.Equal("/home/tester/.local/share/DoNotPanicPortfolioVisualizer2/Secrets", paths.SecretRoot);
    }

    [Fact]
    public void Resolve_MacOs_UsesApplicationSupport()
    {
        LocalDataPaths paths = LocalDataRootResolver.Resolve(
            DesktopPlatformKind.MacOS,
            environmentLookup: _ => null,
            userHomeDirectory: "/Users/tester",
            createDirectories: false);

        Assert.Equal("/Users/tester/Library/Application Support/DoNotPanicPortfolioVisualizer2", paths.Root);
        Assert.Equal("/Users/tester/Library/Application Support/DoNotPanicPortfolioVisualizer2/Logs", paths.LogRoot);
    }

    [Fact]
    public void Resolve_OverrideBypassesPlatformDefaults()
    {
        LocalDataPaths paths = LocalDataRootResolver.Resolve(
            DesktopPlatformKind.Linux,
            environmentLookup: name => string.Equals(name, AppIdentity.LocalDataRootOverrideEnvironmentVariable, StringComparison.Ordinal) ? "/srv/dnppv2" : null,
            userHomeDirectory: "/home/tester",
            createDirectories: false);

        Assert.Equal("/srv/dnppv2", paths.Root);
        Assert.Equal("/srv/dnppv2/Caches/History", paths.HistoricalCacheRoot);
    }

    [Fact]
    public void Resolve_NormalizesForeignPlatformOverrideWithoutCurrentPlatformAssumptions()
    {
        LocalDataPaths paths = LocalDataRootResolver.Resolve(
            DesktopPlatformKind.Linux,
            environmentLookup: name => string.Equals(name, AppIdentity.LocalDataRootOverrideEnvironmentVariable, StringComparison.Ordinal) ? "/srv//dnppv2/./state" : null,
            userHomeDirectory: "/home/tester",
            createDirectories: false);

        Assert.Equal("/srv/dnppv2/state", paths.Root);
    }

    [Fact]
    public void YFinanceLoopbackContract_UsesApprovedPort()
    {
        Assert.Equal("127.0.0.1", YFinanceLoopbackContract.LoopbackHost);
        Assert.Equal(14871, YFinanceLoopbackContract.DefaultPort);
        Assert.Equal(YFinanceLoopbackContract.DefaultPort, ProtocolConstants.DefaultPort);
        Assert.Equal("http://127.0.0.1:14871/", YFinanceLoopbackContract.BaseUri.AbsoluteUri);
    }
}
