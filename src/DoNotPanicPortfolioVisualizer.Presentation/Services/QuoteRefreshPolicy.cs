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
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Services;
using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.Presentation.Services;

public static class QuoteRefreshPolicy
{
    private static readonly TimeSpan UiSequentialCadence = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinimumHardStaleThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HardStaleGrace = TimeSpan.FromMinutes(2);

    public static TimeSpan GetConfiguredRefreshWindow(AppSettings settings, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(settings);
        int configuredSeconds = IsLikelyOpenNewYorkMarket(nowUtc)
            ? settings.RefreshSecondsPortfolio
            : settings.RefreshSecondsOffHours;
        return TimeSpan.FromSeconds(Math.Clamp(configuredSeconds, Defaults.MinRefreshSeconds, Defaults.MaxRefreshSeconds));
    }

    // Quote dispatch is deliberately independent of the user-facing freshness window.
    public static TimeSpan GetRefreshPollingInterval(AppSettings settings, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return UiSequentialCadence;
    }

    public static TimeSpan GetHardStaleThreshold(AppSettings settings, DateTimeOffset nowUtc)
    {
        TimeSpan withGrace = GetRefreshPollingInterval(settings, nowUtc) + HardStaleGrace;
        return withGrace > MinimumHardStaleThreshold ? withGrace : MinimumHardStaleThreshold;
    }

    public static bool IsHardStale(QuoteSnapshot quote, AppSettings settings, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return quote.IsStale || nowUtc - quote.FetchTimestampUtc >= GetHardStaleThreshold(settings, nowUtc);
    }

    private static bool IsLikelyOpenNewYorkMarket(DateTimeOffset nowUtc)
    {
        TimeZoneInfo eastern = ExchangeTimeZoneResolver.Resolve("America/New_York");
        DateTimeOffset easternNow = TimeZoneInfo.ConvertTime(nowUtc, eastern);
        if (easternNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return false;

        TimeOnly localTime = TimeOnly.FromDateTime(easternNow.DateTime);
        return localTime >= new TimeOnly(9, 30) && localTime < new TimeOnly(16, 0);
    }
}
