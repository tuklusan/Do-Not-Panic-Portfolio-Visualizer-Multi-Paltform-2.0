// ============================================================================
// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Proprietary rights reserved except as expressly licensed herein.
//
// DO NOT PANIC PORTFOLIO VISUALIZER
// This file is governed by the SANYALnet Labs Non-Commercial License in the
// root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
// for AI/ML model training are prohibited unless separately authorized.
//
// Attribution is required: "Based on original work by Supratim Sanyal of
// SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
// patent, trademark, and governing-law provisions.
// ============================================================================
using DoNotPanicPortfolioVisualizer.Core;

namespace DoNotPanicPortfolioVisualizer.Core.Storage;

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
        => ResolveFirstOverride(DetectCurrentPlatform(), environmentLookup);

    public static string? ResolveFirstOverride(
        DesktopPlatformKind platform,
        Func<string, string?>? environmentLookup = null)
    {
        environmentLookup ??= Environment.GetEnvironmentVariable;

        foreach (string name in EnumerateOverrideNames())
        {
            string? candidate = NormalizeOverride(environmentLookup(name));
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            return NormalizeValidatedOverride(platform, candidate, name);
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
        string? overrideRoot = ResolveFirstOverride(platform, environmentLookup);
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return overrideRoot;

        return platform switch
        {
            DesktopPlatformKind.Windows => Combine(
                platform,
                RequireValue(windowsLocalAppData, "Windows LocalApplicationData base path"),
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
            RequireValue(userHomeDirectory, "Linux home directory"),
            ".local",
            "share",
            AppIdentity.LocalDataFolderName);
    }

    private static string ResolveMacOsRoot(string? userHomeDirectory)
        => Combine(
            DesktopPlatformKind.MacOS,
            RequireValue(userHomeDirectory, "macOS home directory"),
            "Library",
            "Application Support",
            AppIdentity.LocalDataFolderName);

    private static string RequireValue(string? value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"The {description} is unavailable. Provide a valid {description} or set the DNPPV-2.0 local-data override before continuing.");
        }

        return value.Trim();
    }

    private static string NormalizeOverride(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeAbsoluteOrWellKnownPath(DesktopPlatformKind platform, string path)
    {
        char separator = platform == DesktopPlatformKind.Windows ? '\\' : '/';
        string normalized = path.Trim();
        normalized = platform == DesktopPlatformKind.Windows
            ? normalized.Replace('/', separator)
            : normalized.Replace('\\', separator);

        return platform switch
        {
            DesktopPlatformKind.Windows => NormalizeWindowsAbsolutePath(normalized),
            DesktopPlatformKind.Linux or DesktopPlatformKind.MacOS => NormalizePosixAbsolutePath(normalized),
            _ => throw new PlatformNotSupportedException($"Unsupported platform: {platform}")
        };
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

            if (!combined.EndsWith(separator.ToString(), StringComparison.Ordinal))
                combined += separator;

            normalizedSegment = normalizedSegment.Replace('\\', separator).Replace('/', separator);
            combined += normalizedSegment;
        }

        return combined;
    }

    private static IEnumerable<string> EnumerateOverrideNames()
    {
        yield return AppIdentity.LocalDataRootOverrideEnvironmentVariable;
        foreach (string name in AppIdentity.DeprecatedOverrideEnvironmentVariables)
            yield return name;
    }

    private static string NormalizeValidatedOverride(
        DesktopPlatformKind platform,
        string candidate,
        string environmentVariableName)
    {
        if (!IsAbsolutePathForPlatform(platform, candidate))
        {
            throw new InvalidOperationException(
                $"The local data override from {environmentVariableName} must be an absolute {platform} path.");
        }

        return NormalizeAbsoluteOrWellKnownPath(platform, candidate);
    }

    private static bool IsAbsolutePathForPlatform(DesktopPlatformKind platform, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return platform switch
        {
            DesktopPlatformKind.Windows => IsWindowsAbsolutePath(path),
            DesktopPlatformKind.Linux or DesktopPlatformKind.MacOS => path.StartsWith("/", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsWindowsAbsolutePath(string path)
    {
        if (path.Length >= 3 &&
            char.IsLetter(path[0]) &&
            path[1] == ':' &&
            (path[2] == '\\' || path[2] == '/'))
        {
            return true;
        }

        return path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);
    }

    private static string NormalizeWindowsAbsolutePath(string path)
    {
        string normalized = path.Replace('/', '\\');

        string prefix;
        int startIndex;
        if (normalized.StartsWith(@"\\", StringComparison.Ordinal))
        {
            string[] uncSegments = normalized[2..]
                .Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (uncSegments.Length < 2)
                throw new InvalidOperationException("UNC local data override paths must include both server and share segments.");

            prefix = $@"\\{uncSegments[0]}\{uncSegments[1]}";
            startIndex = 2;
            return AppendNormalizedSegments(prefix, uncSegments.Skip(startIndex), '\\');
        }

        if (normalized.Length < 3 || !char.IsLetter(normalized[0]) || normalized[1] != ':' || normalized[2] != '\\')
            throw new InvalidOperationException("Windows local data override paths must be drive-rooted or UNC paths.");

        prefix = char.ToUpperInvariant(normalized[0]) + @":\";
        string[] segments = normalized[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return AppendNormalizedSegments(prefix, segments, '\\');
    }

    private static string NormalizePosixAbsolutePath(string path)
    {
        string normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("/", StringComparison.Ordinal))
            throw new InvalidOperationException("POSIX local data override paths must start at the filesystem root.");

        string[] segments = normalized[1..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        return AppendNormalizedSegments("/", segments, '/');
    }

    private static string AppendNormalizedSegments(string prefix, IEnumerable<string> segments, char separator)
    {
        Stack<string> stack = new();
        foreach (string rawSegment in segments)
        {
            string segment = rawSegment.Trim();
            if (segment.Length == 0 || segment == ".")
                continue;

            if (segment == "..")
            {
                if (stack.Count == 0)
                    throw new InvalidOperationException("Local data override paths cannot traverse above their root.");

                stack.Pop();
                continue;
            }

            stack.Push(segment);
        }

        string[] ordered = stack.Reverse().ToArray();
        if (ordered.Length == 0)
            return prefix.TrimEnd(separator) + separator;

        string trimmedPrefix = prefix.EndsWith(separator.ToString(), StringComparison.Ordinal) ? prefix[..^1] : prefix;
        return trimmedPrefix + separator + string.Join(separator, ordered);
    }
}
