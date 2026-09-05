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
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Data.Interfaces;
using DoNotPanicPortfolioVisualizer.Presentation.Services;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class ProgressiveQuoteRefreshPipelineTests
{
    [Fact]
    public async Task RefreshAsync_DrainsCompletedRequestsAndRetainsLatestQuotes()
    {
        ControlledQuoteProvider provider = new();
        using ProgressiveQuoteRefreshPipeline pipeline = new();

        ProgressiveQuoteRefreshResult queued = await pipeline.RefreshAsync(["AAA", "BBB"], provider);

        Assert.Empty(queued.UpdatedQuotes);
        Assert.Equal(2, queued.InFlightRequestCount);
        await provider.WaitForRequestsAsync();
        provider.Complete("AAA", Quote("AAA", 101m));
        provider.Complete("BBB", Quote("BBB", 202m));
        await provider.WaitForCompletionsAsync();

        ProgressiveQuoteRefreshResult drained = await WaitForCompletedQuotesAsync(pipeline, provider);

        Assert.Equal(2, drained.UpdatedQuotes.Count);
        Assert.Equal(101m, drained.CachedQuotes["AAA"].Last);
        Assert.Equal(202m, drained.CachedQuotes["BBB"].Last);
        Assert.True(drained.ProviderHealth.IsHealthy);
    }

    [Fact]
    public void QuoteRefreshPolicy_UsesOneSecondDispatchAndFifteenMinuteHardStaleFloor()
    {
        AppSettings settings = new()
        {
            RefreshSecondsPortfolio = 900,
            RefreshSecondsOffHours = 1800
        };
        DateTimeOffset now = new(2026, 6, 5, 14, 0, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromSeconds(1), QuoteRefreshPolicy.GetRefreshPollingInterval(settings, now));
        Assert.Equal(TimeSpan.FromMinutes(15), QuoteRefreshPolicy.GetHardStaleThreshold(settings, now));
        Assert.True(QuoteRefreshPolicy.IsHardStale(
            Quote("AAA", 1m, now.AddMinutes(-16)), settings, now));
    }

    [Fact]
    public async Task RefreshAsync_CapsInitialProgressiveDispatchAtFourRequests()
    {
        CountingPendingQuoteProvider provider = new();
        using ProgressiveQuoteRefreshPipeline pipeline = new();

        ProgressiveQuoteRefreshResult result = await pipeline.RefreshAsync(
            ["AAA", "BBB", "CCC", "DDD", "EEE"],
            provider);

        Assert.Equal(ProgressiveQuoteRefreshPipeline.MaximumInFlightRequests, result.InFlightRequestCount);
        Assert.Equal(ProgressiveQuoteRefreshPipeline.MaximumInFlightRequests, provider.RequestCount);
    }

    [Fact]
    public async Task RefreshAsync_PrunesTimedOutRequestsAndFreesTheSymbolForRetry()
    {
        CancellationAwareQuoteProvider provider = new();
        using ProgressiveQuoteRefreshPipeline pipeline = new(requestTimeout: TimeSpan.FromMilliseconds(10));

        ProgressiveQuoteRefreshResult first = await pipeline.RefreshAsync(["AAA"], provider);
        await Task.Delay(TimeSpan.FromMilliseconds(30));
        ProgressiveQuoteRefreshResult retried = await pipeline.RefreshAsync(["AAA"], provider);

        Assert.Equal(1, first.InFlightRequestCount);
        Assert.Contains("AAA", retried.FailedSymbols);
        Assert.Equal(1, retried.InFlightRequestCount);
        Assert.Equal(2, provider.RequestCount);
        Assert.False(retried.ProviderHealth.IsHealthy);
        await provider.WaitForCancellationAsync();
    }

    [Fact]
    public async Task SingleSymbolQuoteRefresh_FetchesDistinctSymbolsSequentially()
    {
        RecordingQuoteProvider provider = new();

        IReadOnlyList<QuoteSnapshot> quotes = await SingleSymbolQuoteRefresh.FetchAsync(
            provider,
            [" AAA ", "BBB", "aaa", " ", "BBB"]);

        Assert.Equal(["AAA", "BBB"], provider.Requests);
        Assert.Equal(["AAA", "BBB"], quotes.Select(static quote => quote.Symbol));
    }

    private static QuoteSnapshot Quote(string symbol, decimal last, DateTimeOffset? fetchedAt = null)
        => new()
        {
            Symbol = symbol,
            Last = last,
            PreviousClose = last - 1m,
            FetchTimestampUtc = fetchedAt ?? DateTimeOffset.UtcNow
        };

    private static async Task<ProgressiveQuoteRefreshResult> WaitForCompletedQuotesAsync(
        ProgressiveQuoteRefreshPipeline pipeline,
        IQuoteProvider provider)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            ProgressiveQuoteRefreshResult result = await pipeline.RefreshAsync(["AAA", "BBB"], provider);
            if (result.UpdatedQuotes.Count == 2)
                return result;

            await Task.Delay(10);
        }

        return await pipeline.RefreshAsync(["AAA", "BBB"], provider);
    }

    private sealed class ControlledQuoteProvider : IQuoteProvider
    {
        private readonly Dictionary<string, TaskCompletionSource<IReadOnlyList<QuoteSnapshot>>> _requests = new(StringComparer.OrdinalIgnoreCase);
        private readonly TaskCompletionSource _allInitialRequestsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allInitialRequestsCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _completionCount;

        public Task<IReadOnlyList<QuoteSnapshot>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
        {
            string symbol = Assert.Single(symbols);
            TaskCompletionSource<IReadOnlyList<QuoteSnapshot>> request = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(symbol, request);
            if (_requests.Count >= 2)
                _allInitialRequestsStarted.TrySetResult();
            return request.Task;
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public void Complete(string symbol, QuoteSnapshot quote)
        {
            _requests[symbol].SetResult([quote]);
            if (Interlocked.Increment(ref _completionCount) >= 2)
                _allInitialRequestsCompleted.TrySetResult();
        }

        public Task WaitForRequestsAsync()
            => _allInitialRequestsStarted.Task;

        public async Task WaitForCompletionsAsync()
        {
            await _allInitialRequestsCompleted.Task;
            await Task.Yield();
        }
    }

    private sealed class CancellationAwareQuoteProvider : IQuoteProvider
    {
        private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int RequestCount { get; private set; }

        public Task<IReadOnlyList<QuoteSnapshot>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken cancellationToken = default)
        {
            RequestCount++;
            TaskCompletionSource<IReadOnlyList<QuoteSnapshot>> request = new(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() =>
            {
                request.TrySetCanceled(cancellationToken);
                _cancelled.TrySetResult();
            });
            return request.Task;
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task WaitForCancellationAsync()
            => _cancelled.Task;
    }

    private sealed class CountingPendingQuoteProvider : IQuoteProvider
    {
        public int RequestCount { get; private set; }

        public Task<IReadOnlyList<QuoteSnapshot>> GetQuotesAsync(
            IEnumerable<string> symbols,
            CancellationToken cancellationToken = default)
        {
            RequestCount++;
            return new TaskCompletionSource<IReadOnlyList<QuoteSnapshot>>(
                TaskCreationOptions.RunContinuationsAsynchronously).Task;
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class RecordingQuoteProvider : IQuoteProvider
    {
        public List<string> Requests { get; } = [];

        public Task<IReadOnlyList<QuoteSnapshot>> GetQuotesAsync(
            IEnumerable<string> symbols,
            CancellationToken cancellationToken = default)
        {
            string symbol = Assert.Single(symbols);
            Requests.Add(symbol);
            return Task.FromResult<IReadOnlyList<QuoteSnapshot>>([Quote(symbol, 1m)]);
        }

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
