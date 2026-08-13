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
using DoNotPanicPortfolioVisualizer.Core.Storage;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

internal static class StorageOverridePathValidator
{
    public static string ResolveFilePath(string? overridePath, string defaultPath)
    {
        if (string.IsNullOrWhiteSpace(overridePath))
            return defaultPath;

        string resolved = Path.GetFullPath(Environment.ExpandEnvironmentVariables(overridePath.Trim()));
        ValidateAllowedPath(resolved);
        return resolved;
    }

    private static void ValidateAllowedPath(string resolvedPath)
    {
        string candidateDirectory = Path.GetDirectoryName(resolvedPath) ?? resolvedPath;
        string[] allowedRoots =
        [
            Path.GetFullPath(LocalDataRootResolver.ResolveForCurrentPlatform().Root),
            Path.GetFullPath(Path.GetTempPath()),
            Path.GetFullPath(Environment.CurrentDirectory),
            Path.GetFullPath(AppContext.BaseDirectory)
        ];

        if (allowedRoots.Any(root => IsWithinRoot(candidateDirectory, root)))
            return;

        throw new InvalidOperationException(
            "Storage path overrides must stay within the application data root, current working directory, application base directory, or system temporary directory.");
    }

    private static bool IsWithinRoot(string candidatePath, string rootPath)
    {
        string relative = Path.GetRelativePath(rootPath, candidatePath);
        return !relative.StartsWith("..", StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }
}
