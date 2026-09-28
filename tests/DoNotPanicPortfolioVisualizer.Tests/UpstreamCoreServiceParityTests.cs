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
using DoNotPanicPortfolioVisualizer.Core.Services;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class MarketSessionResolverTests
{
    [Fact]
    public void Resolve_ReturnsAValidEnum()
    {
        MarketSessionResolver resolver = new();
        MarketSession session = resolver.Resolve(DateTimeOffset.UtcNow);
        Assert.True(Enum.IsDefined(session));
    }

    [Theory]
    [InlineData("2026-08-24T11:00:00Z", MarketSession.PreMarket)]
    [InlineData("2026-08-24T14:00:00Z", MarketSession.Regular)]
    [InlineData("2026-08-24T21:00:00Z", MarketSession.AfterHours)]
    [InlineData("2026-08-24T01:00:00Z", MarketSession.Closed)]
    [InlineData("2026-08-23T14:00:00Z", MarketSession.Closed)]
    public void Resolve_UsesNewYorkMarketHoursOnEveryPlatform(string utcTimestamp, MarketSession expected)
    {
        MarketSessionResolver resolver = new();

        Assert.Equal(expected, resolver.Resolve(DateTimeOffset.Parse(utcTimestamp)));
    }

    [Fact]
    public void ExchangeTimeZoneResolver_MapsWindowsAndIanaExchangeIds()
    {
        TimeZoneInfo iana = ExchangeTimeZoneResolver.Resolve("America/New_York");
        TimeZoneInfo windows = ExchangeTimeZoneResolver.Resolve("Eastern Standard Time");

        Assert.Equal(
            TimeZoneInfo.ConvertTime(DateTimeOffset.Parse("2026-08-24T14:00:00Z"), iana).Offset,
            TimeZoneInfo.ConvertTime(DateTimeOffset.Parse("2026-08-24T14:00:00Z"), windows).Offset);
    }

    [Fact]
    public void NyseTradingCalendarSnapshot_CoversObservedHolidaysAndEarlyCloses()
    {
        NyseTradingCalendarSnapshot calendar = NyseTradingCalendarSnapshot.CreateOfflineFallback(2026, 2026);

        Assert.True(calendar.IsHoliday(new DateOnly(2026, 7, 3)));
        Assert.True(calendar.IsHoliday(new DateOnly(2026, 11, 26)));
        Assert.Equal(new TimeOnly(13, 0), calendar.ResolveRegularClose(new DateOnly(2026, 11, 27)));
        Assert.Equal(new TimeOnly(16, 0), calendar.ResolveRegularClose(new DateOnly(2026, 8, 24)));
    }
}

public sealed class SymbolNormalizerTests
{
    [Fact]
    public void Normalize_TrimsAndUppercasesWithoutChangingDotSymbols()
    {
        SymbolNormalizer normalizer = new();
        Assert.Equal("BRK.B", normalizer.Normalize(" brk.b "));
    }

    [Theory]
    [InlineData(" eur/usd ", "EUR/USD")]
    [InlineData("^gspc", "^GSPC")]
    [InlineData(" btc-usd ", "BTC-USD")]
    public void Normalize_PreservesCommonNonEquityTickerCharacters(string input, string expected)
    {
        SymbolNormalizer normalizer = new();
        Assert.Equal(expected, normalizer.Normalize(input));
    }
}

public sealed class SymbolProfileHeuristicsTests
{
    [Theory]
    [InlineData("ES=F", SymbolAssetClass.Future)]
    [InlineData("^GSPC", SymbolAssetClass.Index)]
    [InlineData("^VIX", SymbolAssetClass.Index)]
    [InlineData("^TNX", SymbolAssetClass.Index)]
    [InlineData("^IRX", SymbolAssetClass.Index)]
    [InlineData("EUR/USD", SymbolAssetClass.Forex)]
    [InlineData("EURUSD=X", SymbolAssetClass.Forex)]
    [InlineData("BTC-USD", SymbolAssetClass.Crypto)]
    [InlineData("SWVXX", SymbolAssetClass.MoneyMarketFund)]
    [InlineData("VTSAX", SymbolAssetClass.MutualFund)]
    public void InferAssetClass_RecognizesMixedTickerShapes(string symbol, SymbolAssetClass expected)
    {
        Assert.Equal(expected, SymbolProfileHeuristics.InferAssetClass(symbol));
    }

    [Theory]
    [InlineData("AAPL", "EQUITY", SymbolAssetClass.Equity)]
    [InlineData("SPY", "ETF", SymbolAssetClass.ExchangeTradedFund)]
    [InlineData("VTSAX", "MUTUALFUND", SymbolAssetClass.MutualFund)]
    [InlineData("BTC-USD", "CRYPTOCURRENCY", SymbolAssetClass.Crypto)]
    public void InferAssetClass_PrefersProviderInstrumentType(string symbol, string instrumentType, SymbolAssetClass expected)
    {
        Assert.Equal(expected, SymbolProfileHeuristics.InferAssetClass(symbol, instrumentType));
    }
}
