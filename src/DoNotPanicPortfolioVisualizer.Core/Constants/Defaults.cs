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
using System;
using System.IO;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Storage;

namespace DoNotPanicPortfolioVisualizer.Core.Constants;

// A sleepy badger asked the clock why prices need so much attention.
public static class Defaults
{
    public const string DefaultNewsFeedUrl = "https://www.cnbc.com/id/100003114/device/rss/rss.html";
    public static readonly IReadOnlyList<string> DefaultNewsFeedUrls =
    [
        "https://www.cnbc.com/id/100003114/device/rss/rss.html",
        "https://feeds.content.dowjones.io/public/rss/mw_topstories",
        "https://www.investing.com/rss/news.rss"
    ];
    public const int MaximumNewsFeedCount = 3;
    public const string DefaultAiEndpointUrl = "https://openrouter.ai/api/v1";
    public const string DefaultAiModelId = "openrouter/free";

    public const int MinRefreshSeconds = 5;
    public const int MaxRefreshSeconds = 4 * 60 * 60;
    public const int MinBackgroundChangeSeconds = 120;
    public const int DefaultDesktopRefreshSeconds = 300;
    public const int LegacySteadyStateRefreshSeconds = 1200;
    public const int MinimumSummarizedNewsRefreshMinutes = 30;
    public const int MinNewsRefreshMinutes = 30;
    public const int MaxNewsRefreshMinutes = 4 * 60;
    public const int MaxTapeCount = 4;
    public const int MaxTickersPerTape = 8;
    public const int MaxTapeNameLength = 16;
    public const double DefaultTapeBaseSpeed = 0.45d;
    public const double MinTapeSpeed = 0.20d;
    public const double MaxTapeSpeed = 0.65d;
    public const double DefaultNewsSpeed = 0.30d;

    public static string GetLegacyHistoricalCacheFolder()
        => Path.Combine(Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath(), "PortfolioSaver", "history-cache");

    public static string GetHistoricalCacheFolder()
        => LocalDataRootResolver.ResolveForCurrentPlatform().HistoricalCacheRoot;

    public static string GetManagedBackgroundCacheFolder()
        => Path.Combine(LocalDataRootResolver.ResolveForCurrentPlatform().Root, "Backgrounds", "ExchangePhotoCache");

    public static AppSettings CreateSettings() => new()
    {
        AiApiKey = string.Empty,
        AiEndpointUrl = DefaultAiEndpointUrl,
        AiModelId = DefaultAiModelId,
        MarketCalendarRefreshHours = 12,
        RefreshSecondsPortfolio = DefaultDesktopRefreshSeconds,
        RefreshSecondsOffHours = DefaultDesktopRefreshSeconds,
        NewsScrollerMode = NewsScrollerMode.RssFeed,
        AiWritingStyle = AiWritingStyle.DouglasAdams,
        NewsFeedUrl = DefaultNewsFeedUrl,
        NewsFeedUrls = DefaultNewsFeedUrls.ToList(),
        NewsRefreshMinutes = 30,
        EnableFloatingGraphs = true,
        HistoricalLookbackDays = 14,
        HistoricalRefreshHours = 12,
        MaxFloatingGraphsPerTape = 4,
        HistoricalCacheRootFolder = GetHistoricalCacheFolder(),
        HttpTimeoutSeconds = 10,
        BackgroundImageFolder = GetManagedBackgroundCacheFolder(),
        UseCustomBackgroundImageFolder = false,
        CustomBackgroundImageFolder = string.Empty,
        BackgroundChangeSeconds = 300,
        BackgroundIncludeSubfolders = true,
        DimOpacity = 0.55,
        LayoutPreset = LayoutPreset.UltrawideDefault,
        Groups =
        [
            new TickerGroup
            {
                Name = "GBLCORE+SAT",
                Direction = ScrollDirection.Left,
                Speed = GetDefaultTapeSpeed(0),
                RowHeight = 56.0,
                Tickers =
                [
                    new TickerItem { Symbol = "VOO" },
                    new TickerItem { Symbol = "IJH" },
                    new TickerItem { Symbol = "IJR" },
                    new TickerItem { Symbol = "VEA" },
                    new TickerItem { Symbol = "VWO" },
                    new TickerItem { Symbol = "VSS" },
                    new TickerItem { Symbol = "EFV" },
                    new TickerItem { Symbol = "EFG" }
                ]
            },
            new TickerGroup
            {
                Name = "MLTFCT+SMRTBETA",
                Direction = ScrollDirection.Right,
                Speed = GetDefaultTapeSpeed(1),
                RowHeight = 56.0,
                Tickers =
                [
                    new TickerItem { Symbol = "QUAL" },
                    new TickerItem { Symbol = "MTUM" },
                    new TickerItem { Symbol = "VLUE" },
                    new TickerItem { Symbol = "SIZE" },
                    new TickerItem { Symbol = "USMV" },
                    new TickerItem { Symbol = "INTF" },
                    new TickerItem { Symbol = "EMGF" },
                    new TickerItem { Symbol = "SYLD" }
                ]
            },
            new TickerGroup
            {
                Name = "DIVGRTH+YLD",
                Direction = ScrollDirection.Left,
                Speed = GetDefaultTapeSpeed(2),
                RowHeight = 56.0,
                Tickers =
                [
                    new TickerItem { Symbol = "SCHD" },
                    new TickerItem { Symbol = "VIG" },
                    new TickerItem { Symbol = "VYM" },
                    new TickerItem { Symbol = "DGRO" },
                    new TickerItem { Symbol = "VYMI" },
                    new TickerItem { Symbol = "VIGI" },
                    new TickerItem { Symbol = "SPHD" },
                    new TickerItem { Symbol = "DGRW" }
                ]
            },
            new TickerGroup
            {
                Name = "STRTGC-ROTN",
                Direction = ScrollDirection.Right,
                Speed = GetDefaultTapeSpeed(3),
                RowHeight = 56.0,
                Tickers =
                [
                    new TickerItem { Symbol = "XLK" },
                    new TickerItem { Symbol = "XLV" },
                    new TickerItem { Symbol = "XLF" },
                    new TickerItem { Symbol = "XLY" },
                    new TickerItem { Symbol = "XLE" },
                    new TickerItem { Symbol = "SMH" },
                    new TickerItem { Symbol = "CIBR" },
                    new TickerItem { Symbol = "ARKK" }
                ]
            }
        ]
    };

    public static string GetDefaultTapeName(int index)
        => $"Tape {Math.Clamp(index, 1, MaxTapeCount)}";

    public static double GetDefaultTapeSpeed(int displayIndex)
        => displayIndex switch
        {
            0 => DefaultTapeBaseSpeed,
            1 => Math.Round(DefaultTapeBaseSpeed * 0.90d, 4),
            2 => Math.Round(DefaultTapeBaseSpeed * 1.10d, 4),
            3 => Math.Round(DefaultTapeBaseSpeed * 0.85d, 4),
            _ => DefaultTapeBaseSpeed
        };

    public static TickerGroup CreateEmptyTickerGroup(int displayIndex)
        => new()
        {
            Name = GetDefaultTapeName(displayIndex + 1),
            Direction = displayIndex % 2 == 0 ? ScrollDirection.Left : ScrollDirection.Right,
            Speed = GetDefaultTapeSpeed(displayIndex),
            RowHeight = 56.0,
            Enabled = true
        };
}

