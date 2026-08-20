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
namespace YFinance.NET.Storage;

public static class AppDataRootResolver
{
    public const string ProductLocalDataRootEnvironmentVariable = "DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT";
    private const string ProductFolderName = "DoNotPanicPortfolioVisualizer2";

    public static string ResolveInstalledLocalDataRoot(bool createDirectory = true)
    {
        string? overrideRoot = Environment.GetEnvironmentVariable(ProductLocalDataRootEnvironmentVariable);
        string root = string.IsNullOrWhiteSpace(overrideRoot)
            ? ResolvePlatformRoot()
            : RequireAbsolutePath(overrideRoot.Trim(), ProductLocalDataRootEnvironmentVariable);

        root = Path.GetFullPath(root);
        if (createDirectory)
            Directory.CreateDirectory(root);

        return root;
    }

    private static string ResolvePlatformRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(RequireDirectory(localAppData, "Windows local application data"), ProductFolderName);
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(RequireDirectory(home, "macOS user home"), "Library", "Application Support", ProductFolderName);

        if (OperatingSystem.IsLinux())
        {
            string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return string.IsNullOrWhiteSpace(xdgDataHome)
                ? Path.Combine(RequireDirectory(home, "Linux user home"), ".local", "share", ProductFolderName)
                : Path.Combine(RequireAbsolutePath(xdgDataHome.Trim(), "XDG_DATA_HOME"), ProductFolderName);
        }

        throw new PlatformNotSupportedException("YFinance local storage supports Windows, Linux, and macOS.");
    }

    private static string RequireAbsolutePath(string path, string description)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidOperationException($"{description} must be an absolute path.");

        return path;
    }

    private static string RequireDirectory(string? path, string description)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"The {description} directory is unavailable.");

        return path;
    }
}
