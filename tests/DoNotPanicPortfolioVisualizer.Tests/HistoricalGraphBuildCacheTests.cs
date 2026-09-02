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
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Render.Services;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class HistoricalGraphBuildCacheTests
{
    [Fact]
    public void GetOrBuild_ReusesIdenticalGraphAndInvalidatesChangedSnapshot()
    {
        HistoricalGraphBuildCache cache = new();
        TickerHistorySnapshot firstSnapshot = CreateSnapshot("AAPL", 100m);
        int builds = 0;

        FloatingGraphViewModel first = cache.GetOrBuild("Core", firstSnapshot, 1m, true, () => Build("AAPL", ++builds));
        FloatingGraphViewModel reused = cache.GetOrBuild("Core", firstSnapshot, 1m, true, () => Build("AAPL", ++builds));
        FloatingGraphViewModel changed = cache.GetOrBuild("Core", CreateSnapshot("AAPL", 101m), 1m, true, () => Build("AAPL", ++builds));

        Assert.Same(first, reused);
        Assert.NotSame(first, changed);
        Assert.Equal(2, builds);
    }

    [Fact]
    public void GetOrBuild_IsolatesTapeAndSymbolKeysAndEvictsLeastRecentlyUsed()
    {
        HistoricalGraphBuildCache cache = new(capacity: 2);
        TickerHistorySnapshot a = CreateSnapshot("A/B", 100m);
        TickerHistorySnapshot b = CreateSnapshot("A", 100m);
        int builds = 0;

        FloatingGraphViewModel first = cache.GetOrBuild("C", a, null, false, () => Build("first", ++builds));
        _ = cache.GetOrBuild("C/A", b, null, false, () => Build("second", ++builds));
        _ = cache.GetOrBuild("C", a, null, false, () => Build("reuse", ++builds));
        _ = cache.GetOrBuild("third", CreateSnapshot("Z", 100m), null, false, () => Build("third", ++builds));
        FloatingGraphViewModel retained = cache.GetOrBuild("C", a, null, false, () => Build("unexpected", ++builds));

        Assert.Same(first, retained);
        Assert.Equal(2, cache.Count);
        Assert.Equal(4, builds);
    }

    private static FloatingGraphViewModel Build(string symbol, int buildNumber)
        => new() { Symbol = symbol, OverlayText = buildNumber.ToString() };

    private static TickerHistorySnapshot CreateSnapshot(string symbol, decimal close)
        => new()
        {
            Symbol = symbol,
            FetchTimestampUtc = DateTimeOffset.UtcNow,
            LookbackDays = 30,
            SeriesKind = GraphSeriesKind.DailyCloseFallback,
            ExchangeTimeZoneId = "America/New_York",
            Points = [new HistoricalPricePoint { TimestampUtc = DateTimeOffset.UtcNow, Close = close }]
        };
}
