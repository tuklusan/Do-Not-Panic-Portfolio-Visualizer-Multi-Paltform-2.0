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
using DoNotPanicPortfolioVisualizer.Data.Interfaces;
using DoNotPanicPortfolioVisualizer.Data.Providers;
using DoNotPanicPortfolioVisualizer.Data.Runtime;
using DoNotPanicPortfolioVisualizer.Data.Services;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class YFinanceProviderPipelineTests
{
    [Fact]
    public async Task HistoricalCacheService_SaveLoadAndPurgeExpiredFiles()
    {
        using TemporaryDirectoryScope directory = new();
        HistoricalCacheService cache = new(directory.Path);
        TickerHistorySnapshot fresh = CreateHistorySnapshot("AAPL", DateTimeOffset.UtcNow);
        TickerHistorySnapshot expired = CreateHistorySnapshot("MSFT", DateTimeOffset.UtcNow.AddDays(-20));

        await cache.SaveAsync(fresh);
        await cache.SaveAsync(expired);
        File.SetLastWriteTimeUtc(Path.Combine(directory.Path, "MSFT.json"), DateTime.UtcNow.AddDays(-20));

        await cache.PurgeExpiredAsync();

        Assert.NotNull(await cache.LoadAsync("AAPL"));
        Assert.Null(await cache.LoadAsync("MSFT"));
    }

    [Fact]
    public async Task HistoricalCacheService_DeletesInvalidJsonWhenLoaded()
    {
        using TemporaryDirectoryScope directory = new();
        HistoricalCacheService cache = new(directory.Path);
        string path = Path.Combine(directory.Path, "AAPL.json");
        await File.WriteAllTextAsync(path, "{ not-json");

        TickerHistorySnapshot? loaded = await cache.LoadAsync("AAPL");

        Assert.Null(loaded);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task YahooFinanceQuoteProvider_MapsAliasesAndNumericNormalization()
    {
        FakeYFinanceRuntimeClient runtimeClient = new()
        {
            QuotesAsync = (symbols, _) =>
            {
                Assert.Equal(["^TNX", "INDY"], symbols);
                return Task.FromResult(
                    new YFinanceQuotesResponse(
                    [
                        new("^TNX", 42.3m, 41.8m, 0.5m, null, "USD", "America/New_York", "REGULAR", new YFinanceCacheMetadata(true)),
                        new("INDY", 55.0m, 54.0m, 1.0m, 1.8519m, "USD", "America/New_York", "POST", new YFinanceCacheMetadata(false))
                    ]));
            }
        };
        YahooFinanceQuoteProvider provider = new(runtimeClient, throwOnPartial: false);

        IReadOnlyList<QuoteSnapshot> quotes = await provider.GetQuotesAsync(["US10Y", "INDY.US"]);

        QuoteSnapshot us10y = Assert.Single(quotes, q => q.Symbol == "US10Y");
        Assert.Equal(4.23m, us10y.Last);
        Assert.Equal(4.18m, us10y.PreviousClose);
        Assert.Equal(0.05m, us10y.Change);
        Assert.Equal(MarketSession.Regular, us10y.MarketSession);
        Assert.True(us10y.IsStale);

        QuoteSnapshot indy = Assert.Single(quotes, q => q.Symbol == "INDY.US");
        Assert.Equal(MarketSession.AfterHours, indy.MarketSession);
        Assert.False(indy.IsStale);
    }

    [Fact]
    public async Task YahooFinanceQuoteProvider_ThrowsPartialQuoteResultWhenConfigured()
    {
        FakeYFinanceRuntimeClient runtimeClient = new()
        {
            QuotesAsync = (_, _) => Task.FromResult(
                new YFinanceQuotesResponse(
                [
                    new("AAPL", 200m, 198m, 2m, 1.01m, "USD", "America/New_York", "REGULAR", new YFinanceCacheMetadata(false))
                ],
                ["MSFT"]))
        };
        YahooFinanceQuoteProvider provider = new(runtimeClient, throwOnPartial: true);

        PartialQuoteResultException ex = await Assert.ThrowsAsync<PartialQuoteResultException>(
            () => provider.GetQuotesAsync(["AAPL", "MSFT"]));

        Assert.Single(ex.PartialQuotes);
        Assert.Equal("AAPL", ex.PartialQuotes[0].Symbol);
    }

    [Fact]
    public async Task YahooFinanceQuoteProvider_PrefersExactResponseSymbolOverFallbackKey()
    {
        FakeYFinanceRuntimeClient runtimeClient = new()
        {
            QuotesAsync = (_, _) => Task.FromResult(
                new YFinanceQuotesResponse(
                [
                    new("^TNX", 42.3m, 41.8m, 0.5m, null, "USD", "America/New_York", "REGULAR", new YFinanceCacheMetadata(false)),
                    new("TNX", 99m, 98m, 1m, null, "USD", "America/New_York", "REGULAR", new YFinanceCacheMetadata(false))
                ]))
        };
        YahooFinanceQuoteProvider provider = new(runtimeClient, throwOnPartial: false);

        IReadOnlyList<QuoteSnapshot> quotes = await provider.GetQuotesAsync(["^TNX"]);

        QuoteSnapshot quote = Assert.Single(quotes);
        Assert.Equal(42.3m, quote.Last);
    }

    [Fact]
    public async Task HybridHistoricalDataProvider_UsesFreshCacheBeforeRuntime()
    {
        InMemoryHistoricalCacheService cache = new();
        cache.Stored["AAPL"] = CreateHistorySnapshot("AAPL", DateTimeOffset.UtcNow);
        FakeYFinanceRuntimeClient runtimeClient = new();
        HybridHistoricalDataProvider provider = new(cache, runtimeClient);

        IReadOnlyList<TickerHistorySnapshot> history = await provider.GetHistoryAsync(["AAPL"], 14);

        Assert.Single(history);
        Assert.Equal(0, runtimeClient.GetHistoryCallCount);
    }

    [Fact]
    public async Task HybridHistoricalDataProvider_FallsBackToStaleCacheWhenRuntimeFails()
    {
        InMemoryHistoricalCacheService cache = new();
        TickerHistorySnapshot stale = CreateHistorySnapshot("AAPL", DateTimeOffset.UtcNow.AddDays(-2));
        cache.Stored["AAPL"] = stale;
        FakeYFinanceRuntimeClient runtimeClient = new()
        {
            HistoryAsync = (_, _, _, _, _) => throw new HttpRequestException("Simulated upstream failure.")
        };
        HybridHistoricalDataProvider provider = new(cache, runtimeClient, TimeSpan.FromHours(1));

        IReadOnlyList<TickerHistorySnapshot> history = await provider.GetHistoryAsync(["AAPL"], 14);

        Assert.Single(history);
        Assert.Equal(stale.FetchTimestampUtc, history[0].FetchTimestampUtc);
        Assert.Equal(2, runtimeClient.GetHistoryCallCount);
    }

    [Fact]
    public async Task HybridHistoricalDataProvider_ReturnsEmptySnapshotWhenRuntimeFailsWithoutCache()
    {
        InMemoryHistoricalCacheService cache = new();
        FakeYFinanceRuntimeClient runtimeClient = new()
        {
            HistoryAsync = (_, _, _, _, _) => throw new InvalidOperationException("Simulated hard failure.")
        };
        HybridHistoricalDataProvider provider = new(cache, runtimeClient, TimeSpan.FromHours(1));

        IReadOnlyList<TickerHistorySnapshot> history = await provider.GetHistoryAsync(["AAPL"], 14);

        TickerHistorySnapshot snapshot = Assert.Single(history);
        Assert.Equal("AAPL", snapshot.Symbol);
        Assert.Empty(snapshot.Points);
    }

    [Fact]
    public async Task HybridHistoricalDataProvider_FetchesAndCachesMissingHistory()
    {
        InMemoryHistoricalCacheService cache = new();
        FakeYFinanceRuntimeClient runtimeClient = new()
        {
            HistoryAsync = (symbol, _, _, interval, _) =>
            {
                Assert.Equal("AAPL", symbol);
                Assert.Equal("1d", interval);
                return Task.FromResult(
                    new YFinanceHistoryResponse(
                    [
                        new(DateTimeOffset.UtcNow.AddDays(-2), 198m),
                        new(DateTimeOffset.UtcNow.AddDays(-1), 200m)
                    ],
                    new YFinanceHistoryMetadata("America/New_York")));
            }
        };
        HybridHistoricalDataProvider provider = new(cache, runtimeClient);

        IReadOnlyList<TickerHistorySnapshot> history = await provider.GetHistoryAsync(["AAPL"], 14);

        TickerHistorySnapshot snapshot = Assert.Single(history);
        Assert.Equal(GraphSeriesKind.DailyCloseFallback, snapshot.SeriesKind);
        Assert.Equal(2, snapshot.Points.Count);
        Assert.True(cache.Stored.ContainsKey("AAPL"));
    }

    private static TickerHistorySnapshot CreateHistorySnapshot(string symbol, DateTimeOffset fetchTimestampUtc)
        => new()
        {
            Symbol = symbol,
            FetchTimestampUtc = fetchTimestampUtc,
            LookbackDays = 14,
            SeriesKind = GraphSeriesKind.DailyCloseFallback,
            ExchangeTimeZoneId = "America/New_York",
            Points =
            [
                new HistoricalPricePoint
                {
                    TimestampUtc = fetchTimestampUtc.AddDays(-1),
                    Close = 200m
                }
            ]
        };

    private sealed class InMemoryHistoricalCacheService : IHistoricalCacheService
    {
        public Dictionary<string, TickerHistorySnapshot> Stored { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<TickerHistorySnapshot?> LoadAsync(string symbol, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored.TryGetValue(symbol, out TickerHistorySnapshot? snapshot) ? snapshot : null);

        public Task SaveAsync(TickerHistorySnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Stored[snapshot.Symbol] = snapshot;
            return Task.CompletedTask;
        }

        public Task PurgeExpiredAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TemporaryDirectoryScope : IDisposable
    {
        public TemporaryDirectoryScope()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dnppv2-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}

internal sealed class FakeYFinanceRuntimeClient : IYFinanceRuntimeClient
{
    public Func<IReadOnlyList<string>, CancellationToken, Task<YFinanceQuotesResponse>> QuotesAsync { get; init; }
        = (_, _) => Task.FromResult(new YFinanceQuotesResponse([]));

    public Func<string, DateTimeOffset, DateTimeOffset, string, CancellationToken, Task<YFinanceHistoryResponse>> HistoryAsync { get; init; }
        = (_, _, _, _, _) => Task.FromResult(new YFinanceHistoryResponse([]));

    public Func<CancellationToken, Task<bool>> ConnectionAsync { get; init; }
        = _ => Task.FromResult(true);

    public int GetQuotesCallCount { get; private set; }
    public int GetHistoryCallCount { get; private set; }

    public Task<YFinanceQuotesResponse> GetQuotesAsync(
        IReadOnlyList<string> requestSymbols,
        CancellationToken cancellationToken = default)
    {
        GetQuotesCallCount++;
        return QuotesAsync(requestSymbols, cancellationToken);
    }

    public Task<YFinanceHistoryResponse> GetHistoryAsync(
        string requestSymbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string interval,
        CancellationToken cancellationToken = default)
    {
        GetHistoryCallCount++;
        return HistoryAsync(requestSymbol, startUtc, endUtc, interval, cancellationToken);
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        => ConnectionAsync(cancellationToken);
}
