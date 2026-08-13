using DoNotPanicPortfolioVisualizer.Core;

namespace DoNotPanicPortfolioVisualizer.Shared.Storage;

public static class LocalDataRootResolver
{
    public static LocalDataPaths ResolveForCurrentPlatform(bool createDirectories = true)
    {
        DesktopPlatformKind platform = DetectCurrentPlatform();
        return Resolve(
            platform,
            Environment.GetEnvironmentVariable,
            platform == DesktopPlatformKind.Windows ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) : null,
            platform != DesktopPlatformKind.Windows ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : null,
            createDirectories);
    }

    public static LocalDataPaths Resolve(
        DesktopPlatformKind platform,
        Func<string, string?>? environmentLookup = null,
        string? windowsLocalAppData = null,
        string? userHomeDirectory = null,
        bool createDirectories = false)
    {
        environmentLookup ??= Environment.GetEnvironmentVariable;

        string root = ResolveRoot(platform, environmentLookup, windowsLocalAppData, userHomeDirectory);
        LocalDataPaths paths = new(
            platform,
            root,
            Combine(platform, root, "Data"),
            Combine(platform, root, "Caches"),
            Combine(platform, root, "Caches", "History"),
            Combine(platform, root, "Logs"),
            Combine(platform, root, "Secrets"));

        if (createDirectories)
        {
            Directory.CreateDirectory(paths.Root);
            Directory.CreateDirectory(paths.DataRoot);
            Directory.CreateDirectory(paths.CacheRoot);
            Directory.CreateDirectory(paths.HistoricalCacheRoot);
            Directory.CreateDirectory(paths.LogRoot);
            Directory.CreateDirectory(paths.SecretRoot);
        }

        return paths;
    }

    public static string? ResolveFirstOverride(Func<string, string?>? environmentLookup = null)
    {
        environmentLookup ??= Environment.GetEnvironmentVariable;

        string? current = NormalizeOverride(environmentLookup(AppIdentity.LocalDataRootOverrideEnvironmentVariable));
        if (!string.IsNullOrWhiteSpace(current))
            return current;

        foreach (string name in AppIdentity.DeprecatedOverrideEnvironmentVariables)
        {
            string? legacy = NormalizeOverride(environmentLookup(name));
            if (!string.IsNullOrWhiteSpace(legacy))
                return legacy;
        }

        return null;
    }

    public static DesktopPlatformKind DetectCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return DesktopPlatformKind.Windows;
        if (OperatingSystem.IsLinux())
            return DesktopPlatformKind.Linux;
        if (OperatingSystem.IsMacOS())
            return DesktopPlatformKind.MacOS;

        throw new PlatformNotSupportedException("DNPPV-2.0 supports Windows, Linux, and macOS desktop platforms.");
    }

    private static string ResolveRoot(
        DesktopPlatformKind platform,
        Func<string, string?> environmentLookup,
        string? windowsLocalAppData,
        string? userHomeDirectory)
    {
        string? overrideRoot = ResolveFirstOverride(environmentLookup);
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return NormalizeAbsoluteOrWellKnownPath(platform, overrideRoot);

        return platform switch
        {
            DesktopPlatformKind.Windows => Combine(
                platform,
                RequireValue(windowsLocalAppData, "windowsLocalAppData"),
                AppIdentity.LocalDataFolderName),
            DesktopPlatformKind.Linux => ResolveLinuxRoot(environmentLookup, userHomeDirectory),
            DesktopPlatformKind.MacOS => ResolveMacOsRoot(userHomeDirectory),
            _ => throw new PlatformNotSupportedException($"Unsupported platform: {platform}")
        };
    }

    private static string ResolveLinuxRoot(Func<string, string?> environmentLookup, string? userHomeDirectory)
    {
        string? xdgDataHome = NormalizeOverride(environmentLookup("XDG_DATA_HOME"));
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
            return Combine(DesktopPlatformKind.Linux, xdgDataHome, AppIdentity.LocalDataFolderName);

        return Combine(
            DesktopPlatformKind.Linux,
            RequireValue(userHomeDirectory, "userHomeDirectory"),
            ".local",
            "share",
            AppIdentity.LocalDataFolderName);
    }

    private static string ResolveMacOsRoot(string? userHomeDirectory)
        => Combine(
            DesktopPlatformKind.MacOS,
            RequireValue(userHomeDirectory, "userHomeDirectory"),
            "Library",
            "Application Support",
            AppIdentity.LocalDataFolderName);

    private static string RequireValue(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"A non-empty {parameterName} value is required for this platform resolution.");

        return value.Trim();
    }

    private static string NormalizeOverride(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeAbsoluteOrWellKnownPath(DesktopPlatformKind platform, string path)
    {
        if (platform == DetectCurrentPlatform())
            return Path.GetFullPath(path);

        char separator = platform == DesktopPlatformKind.Windows ? '\\' : '/';
        string normalized = path.Trim();
        normalized = platform == DesktopPlatformKind.Windows
            ? normalized.Replace('/', separator)
            : normalized.Replace('\\', separator);

        string doubleSeparator = new(separator, 2);
        while (normalized.Contains(doubleSeparator, StringComparison.Ordinal))
            normalized = normalized.Replace(doubleSeparator, separator.ToString(), StringComparison.Ordinal);

        if (normalized.Length > 1)
            normalized = normalized.TrimEnd(separator);

        return normalized;
    }

    private static string Combine(DesktopPlatformKind platform, string root, params string[] segments)
    {
        char separator = platform == DesktopPlatformKind.Windows ? '\\' : '/';
        string combined = NormalizeAbsoluteOrWellKnownPath(platform, root);

        foreach (string segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
                continue;

            string normalizedSegment = segment.Trim().Trim('\\', '/');
            if (normalizedSegment.Length == 0)
                continue;

            if (!combined.EndsWith(separator))
                combined += separator;

            normalizedSegment = normalizedSegment.Replace('\\', separator).Replace('/', separator);
            combined += normalizedSegment;
        }

        return combined;
    }
}
