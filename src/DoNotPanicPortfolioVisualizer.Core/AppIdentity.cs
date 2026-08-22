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
namespace DoNotPanicPortfolioVisualizer.Core;

public static class AppIdentity
{
    public const string ProductName = "DO NOT PANIC PORTFOLIO VISUALIZER 2.0";
    public const string ProductDisplayName = "DO NOT PANIC PORTFOLIO VISUALIZER";
    public const string PublisherName = "SANYALnet Labs";
    public const string AuthorName = "Supratim Sanyal";
    public const string LicenseName = "SANYALnet Labs Non-Commercial License";
    public const string DesktopSingleInstanceLockFileName = "desktop-instance.lock";

    public const string LocalDataFolderName = "DoNotPanicPortfolioVisualizer2";
    public const string LegacyProductLocalDataFolderName = "DoNotPanicPortfolioVisualizer";
    public const string LegacyPortfolioSaverLocalDataFolderName = "PortfolioSaver";

    public const string LocalDataRootOverrideEnvironmentVariable = "DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT";
    public const string DeprecatedLocalDataRootOverrideEnvironmentVariable = "DONOTPANICPORTFOLIOVISUALIZER_LOCALDATA_ROOT";
    public const string DeprecatedPortfolioSaverLocalDataRootOverrideEnvironmentVariable = "PORTFOLIOSAVER_LOCALDATA_ROOT";
    public const string DeprecatedPortfolioSaverAppDataRootOverrideEnvironmentVariable = "PORTFOLIOSAVER_APPDATA_ROOT";

    public static IReadOnlyList<string> DeprecatedOverrideEnvironmentVariables { get; } =
    [
        DeprecatedLocalDataRootOverrideEnvironmentVariable,
        DeprecatedPortfolioSaverLocalDataRootOverrideEnvironmentVariable,
        DeprecatedPortfolioSaverAppDataRootOverrideEnvironmentVariable
    ];
}

