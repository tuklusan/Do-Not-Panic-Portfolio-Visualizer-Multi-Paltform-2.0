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
using DoNotPanicPortfolioVisualizer.Core.Storage;

namespace DoNotPanicPortfolioVisualizer.Shared.Helpers;

// Shared intentionally layers on top of Core foundation contracts so UI-facing
// code can consume stable helpers without making Core depend on Shared.
public static class AppDataRootResolver
{
    public const string AppLocalDataFolderName = AppIdentity.LocalDataFolderName;
    public const string LegacyAppLocalDataFolderName = AppIdentity.LegacyPortfolioSaverLocalDataFolderName;
    public const string ProductLocalDataRootEnvironmentVariable = AppIdentity.LocalDataRootOverrideEnvironmentVariable;
    public const string LegacyLocalDataRootEnvironmentVariable = AppIdentity.DeprecatedPortfolioSaverLocalDataRootOverrideEnvironmentVariable;
    public const string LegacyAppDataRootEnvironmentVariable = AppIdentity.DeprecatedPortfolioSaverAppDataRootOverrideEnvironmentVariable;

    public static string ResolveInstalledLocalDataRoot(bool createDirectory = true)
        => LocalDataRootResolver.ResolveForCurrentPlatform(createDirectory).Root;

    public static string? ResolveFirstEnvironmentOverride(params string[] names)
    {
        foreach (string name in names)
        {
            string? value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
                ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
