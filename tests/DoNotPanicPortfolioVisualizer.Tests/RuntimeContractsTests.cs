using DoNotPanicPortfolioVisualizer.Core;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using YFinance.NET.Protocol.Constants;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class RuntimeContractsTests
{
    [Fact]
    public void DataAssembly_UsesActive2FriendAssemblyIdentities()
    {
        string source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "DoNotPanicPortfolioVisualizer.Data", "Properties", "AssemblyInfo.cs"));

        Assert.Contains("InternalsVisibleTo(\"DoNotPanicPortfolioVisualizer.Presentation\")", source, StringComparison.Ordinal);
        Assert.Contains("InternalsVisibleTo(\"DoNotPanicPortfolioVisualizer.Tests\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InternalsVisibleTo(\"PortfolioSaver.Presentation\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InternalsVisibleTo(\"PortfolioSaver.Tests\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleInstanceLease_BlocksDuplicateAndAllowsAcquireAfterRelease()
    {
        string root = Path.Combine(Path.GetTempPath(), $"dnppv2-single-instance-{Guid.NewGuid():N}");
        string lockFile = Path.Combine(root, AppIdentity.DesktopSingleInstanceLockFileName);

        Assert.True(SingleInstanceLease.TryAcquire(lockFile, out SingleInstanceLease? first));
        Assert.NotNull(first);
        try
        {
            Assert.False(SingleInstanceLease.TryAcquire(lockFile, out SingleInstanceLease? duplicate));
            Assert.Null(duplicate);

            first.Dispose();

            Assert.True(SingleInstanceLease.TryAcquire(lockFile, out SingleInstanceLease? reacquired));
            reacquired!.Dispose();
        }
        finally
        {
            first.Dispose();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SingleInstanceIdentity_IsDistinctFromUpstream10()
    {
        Assert.Equal("desktop-instance.lock", AppIdentity.DesktopSingleInstanceLockFileName);
        Assert.DoesNotContain("PortfolioSaver", AppIdentity.DesktopSingleInstanceLockFileName, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleInstanceIdentity_IsScopedByThePlatformSpecific20DataRoot()
    {
        LocalDataPaths windowsPaths = LocalDataRootResolver.Resolve(
            DesktopPlatformKind.Windows,
            environmentLookup: _ => null,
            windowsLocalAppData: @"C:\Users\Tester\AppData\Local");
        LocalDataPaths linuxPaths = LocalDataRootResolver.Resolve(
            DesktopPlatformKind.Linux,
            environmentLookup: _ => null,
            userHomeDirectory: "/home/tester");

        string windowsFileName = SingleInstanceLease.ResolveLockFileName(
            AppIdentity.DesktopSingleInstanceLockFileName,
            isWindows: true,
            sessionId: 7);
        string linuxFileName = SingleInstanceLease.ResolveLockFileName(
            AppIdentity.DesktopSingleInstanceLockFileName,
            isWindows: false,
            sessionId: 0);
        string windowsLock = windowsPaths.Root + "\\" + windowsFileName;
        string linuxLock = linuxPaths.Root + "/" + linuxFileName;

        Assert.Equal(@"C:\Users\Tester\AppData\Local\DoNotPanicPortfolioVisualizer2\desktop-instance.session-7.lock", windowsLock);
        Assert.Equal("/home/tester/.local/share/DoNotPanicPortfolioVisualizer2/desktop-instance.lock", linuxLock);
    }

    [Fact]
    public void SingleInstanceLease_AcquiresAnExistingButUnlockedCrashRemnant()
    {
        string root = Path.Combine(Path.GetTempPath(), $"dnppv2-stale-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string lockFile = Path.Combine(root, AppIdentity.DesktopSingleInstanceLockFileName);
        File.WriteAllText(lockFile, string.Empty);

        try
        {
            Assert.True(SingleInstanceLease.TryAcquire(lockFile, out SingleInstanceLease? lease));
            lease!.Dispose();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ResolveLockFileName_RejectsTraversalAndInvalidSessions()
    {
        Assert.Throws<ArgumentException>(
            () => SingleInstanceLease.ResolveLockFileName("../desktop.lock", isWindows: false, sessionId: 0));
        Assert.Throws<ArgumentException>(
            () => SingleInstanceLease.ResolveLockFileName(@"..\desktop.lock", isWindows: true, sessionId: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SingleInstanceLease.ResolveLockFileName("desktop.lock", isWindows: true, sessionId: -1));
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
        Assert.Equal("yfinance-server-14871.lock", ProtocolConstants.GetLockFileName(ProtocolConstants.DefaultPort));
    }
}
