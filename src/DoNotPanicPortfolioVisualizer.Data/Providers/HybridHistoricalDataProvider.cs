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
using System.Collections.Concurrent;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Data.Interfaces;
using DoNotPanicPortfolioVisualizer.Data.Runtime;
using DoNotPanicPortfolioVisualizer.Data.Services;

namespace DoNotPanicPortfolioVisualizer.Data.Providers;

public sealed class HybridHistoricalDataProvider : IHistoricalDataProvider, IDisposable
{
    private const int MaxConcurrentHistoryRequests = 2;
    private const int MaxHistoryFetchAttempts = 2;
    private readonly IHistoricalCacheService _cacheService;
    private readonly IYFinanceRuntimeClient _runtimeClient;
    private readonly TimeSpan _cacheFreshness;
    private readonly bool _disposeCache;

    public HybridHistoricalDataProvider(
        IHistoricalCacheService cacheService,
        IYFinanceRuntimeClient runtimeClient,
        TimeSpan? cacheFreshness = null,
        bool disposeCache = false)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _runtimeClient = runtimeClient ?? throw new ArgumentNullException(nameof(runtimeClient));
        _cacheFreshness = cacheFreshness ?? TimeSpan.FromHours(12);
        _disposeCache = disposeCache;
    }

    public void Dispose()
    {
        if (_disposeCache && _cacheService is IDisposable disposableCache)
            disposableCache.Dispose();
    }

    public async Task<IReadOnlyList<TickerHistorySnapshot>> GetHistoryAsync(
        IEnumerable<string> symbols,
        int lookbackDays,
        CancellationToken cancellationToken = default)
    {
        List<string> orderedSymbols = symbols
            .Select(YFinanceSymbolMapper.Normalize)
            .Where(static symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedSymbols.Count == 0)
            return [];

        await _cacheService.PurgeExpiredAsync(cancellationToken).ConfigureAwait(false);

        ConcurrentDictionary<string, TickerHistorySnapshot> resolved = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, TickerHistorySnapshot> staleCache = new(StringComparer.OrdinalIgnoreCase);
        List<string> pending = [];

        foreach (string symbol in orderedSymbols)
        {
            TickerHistorySnapshot? cached = await _cacheService.LoadAsync(symbol, cancellationToken).ConfigureAwait(false);
            if (cached is not null && cached.LookbackDays == lookbackDays && cached.IsFresh(_cacheFreshness))
            {
                resolved[symbol] = cached;
                continue;
            }

            if (cached is not null)
                staleCache[symbol] = cached;

            pending.Add(symbol);
        }

        if (pending.Count > 0)
        {
            DateTimeOffset endUtc = DateTimeOffset.UtcNow;
            DateTimeOffset startUtc = endUtc.AddDays(-Math.Max(1, lookbackDays));

            using SemaphoreSlim historyGate = new(MaxConcurrentHistoryRequests, MaxConcurrentHistoryRequests);
            Task[] fetchTasks = pending
                .Select(symbol => FetchAndCacheHistoryAsync(
                    symbol,
                    lookbackDays,
                    startUtc,
                    endUtc,
                    historyGate,
                    resolved,
                    cancellationToken))
                .ToArray();
            await Task.WhenAll(fetchTasks).ConfigureAwait(false);
        }

        List<TickerHistorySnapshot> results = [];
        foreach (string symbol in orderedSymbols)
        {
            if (resolved.TryGetValue(symbol, out TickerHistorySnapshot? fetched))
            {
                results.Add(fetched);
                continue;
            }

            if (staleCache.TryGetValue(symbol, out TickerHistorySnapshot? cached))
            {
                results.Add(cached);
                continue;
            }

            results.Add(new TickerHistorySnapshot
            {
                Symbol = symbol,
                LookbackDays = lookbackDays,
                FetchTimestampUtc = DateTimeOffset.UtcNow,
                Points = []
            });
        }

        return results;
    }

    private async Task FetchAndCacheHistoryAsync(
        string symbol,
        int lookbackDays,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        SemaphoreSlim historyGate,
        ConcurrentDictionary<string, TickerHistorySnapshot> resolved,
        CancellationToken cancellationToken)
    {
        bool acquired = false;
        try
        {
            await historyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            string requestSymbol = YFinanceSymbolMapper.ToRequestSymbol(symbol);
            YFinanceHistoryResponse response = await FetchHistoryWithRetryAsync(
                requestSymbol,
                startUtc,
                endUtc,
                ResolveInterval(lookbackDays),
                cancellationToken).ConfigureAwait(false);

            TickerHistorySnapshot snapshot = MapHistory(symbol, lookbackDays, response);
            if (snapshot.Points.Count > 0 && resolved.TryAdd(symbol, snapshot))
            {
                await _cacheService.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsRecoverableHistoryFailure(ex))
        {
        }
        finally
        {
            if (acquired)
                historyGate.Release();
        }
    }

    private async Task<YFinanceHistoryResponse> FetchHistoryWithRetryAsync(
        string requestSymbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string interval,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (int attempt = 1; attempt <= MaxHistoryFetchAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await _runtimeClient.GetHistoryAsync(
                    requestSymbol,
                    startUtc,
                    endUtc,
                    interval,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxHistoryFetchAttempts && IsTransientHistoryException(ex))
            {
                lastException = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException ?? new InvalidOperationException("History fetch retry failed without a captured exception.");
    }

    private static bool IsTransientHistoryException(Exception ex)
        => ex is HttpRequestException or IOException or TimeoutException;

    private static bool IsRecoverableHistoryFailure(Exception ex)
        => ex is HttpRequestException or IOException or TimeoutException or InvalidOperationException;

    private static TickerHistorySnapshot MapHistory(
        string originalSymbol,
        int lookbackDays,
        YFinanceHistoryResponse response)
    {
        return new TickerHistorySnapshot
        {
            Symbol = originalSymbol,
            LookbackDays = lookbackDays,
            SeriesKind = lookbackDays <= 1 ? GraphSeriesKind.Intraday : GraphSeriesKind.DailyCloseFallback,
            ExchangeTimeZoneId = string.IsNullOrWhiteSpace(response.Metadata?.ExchangeTimezoneName)
                ? "UTC"
                : response.Metadata.ExchangeTimezoneName,
            FetchTimestampUtc = DateTimeOffset.UtcNow,
            Points = response.Bars
                .Where(static bar => bar.Close.HasValue)
                .Select(bar => new HistoricalPricePoint
                {
                    TimestampUtc = bar.TimestampUtc,
                    Close = YFinanceSymbolMapper.NormalizeNumericValue(originalSymbol, bar.Close) ?? 0m
                })
                .Where(static point => point.Close > 0m)
                .OrderBy(point => point.TimestampUtc)
                .ToList()
        };
    }

    private static string ResolveInterval(int lookbackDays)
        => lookbackDays <= 1 ? "1h" : "1d";
}
