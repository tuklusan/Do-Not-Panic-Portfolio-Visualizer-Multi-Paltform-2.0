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
using DoNotPanicPortfolioVisualizer.Data.Providers;
using DoNotPanicPortfolioVisualizer.Data.Services;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;

namespace DoNotPanicPortfolioVisualizer.Presentation.Services;

public sealed class ProgressiveQuoteRefreshPipeline : IDisposable
{
    public const int MaximumInFlightRequests = 4;
    // The UI scheduler supplies the one-second cadence. The initial bounded
    // pipeline may fill immediately so slow symbols cannot hold up the scene.
    private static readonly TimeSpan MinimumRequestSpacing = TimeSpan.Zero;
    private readonly Dictionary<string, PendingRequest> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QuoteSnapshot> _memory = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProviderHealthService _providerHealth;
    private readonly RetryPolicyService _retryPolicy;
    private readonly RateLimitGuard _rateLimitGuard = new();
    private int _cursor;
    private bool _disposed;

    public ProgressiveQuoteRefreshPipeline(
        ProviderHealthService? providerHealth = null,
        RetryPolicyService? retryPolicy = null)
    {
        _providerHealth = providerHealth ?? new ProviderHealthService();
        _retryPolicy = retryPolicy ?? new RetryPolicyService();
        Prime(RuntimeQuoteSeedStore.ConsumeAll().Values);
    }

    public ProviderHealthSnapshot ProviderHealth => _providerHealth.Snapshot;

    public async Task<ProgressiveQuoteRefreshResult> RefreshAsync(
        IEnumerable<string> symbols,
        IQuoteProvider quoteProvider,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(quoteProvider);

        Prime(RuntimeQuoteSeedStore.ConsumeAll().Values);
        List<string> orderedSymbols = symbols
            .Where(static symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(static symbol => symbol.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Dictionary<string, QuoteSnapshot> updated = new(StringComparer.OrdinalIgnoreCase);
        List<string> failures = [];

        foreach ((string symbol, PendingRequest pending) in _pending.Where(static pair => pair.Value.Task.IsCompleted).ToList())
        {
            _pending.Remove(symbol);
            try
            {
                IReadOnlyList<QuoteSnapshot> resolved = await pending.Task.ConfigureAwait(false);
                foreach (QuoteSnapshot quote in resolved.Where(HasUsableValue))
                {
                    QuoteSnapshot copy = Clone(quote);
                    _memory[copy.Symbol] = copy;
                    updated[copy.Symbol] = Clone(copy);
                }

                if (resolved.Count > 0)
                    _providerHealth.MarkSuccess();
            }
            catch (PartialQuoteResultException ex)
            {
                foreach (QuoteSnapshot quote in ex.PartialQuotes.Where(HasUsableValue))
                {
                    QuoteSnapshot copy = Clone(quote);
                    _memory[copy.Symbol] = copy;
                    updated[copy.Symbol] = Clone(copy);
                }

                _providerHealth.MarkFailure(ex.Message);
                failures.Add(pending.Symbol);
            }
            catch (OperationCanceledException) when (pending.Cancellation.IsCancellationRequested || cancellationToken.IsCancellationRequested)
            {
                // Cancellation is expected during scene shutdown and is not a health failure.
            }
            catch (Exception ex)
            {
                _providerHealth.MarkFailure(ex.Message);
                failures.Add(pending.Symbol);
                TraceLog.WarnState("ProgressiveQuoteRefreshPipeline", "QuoteRequestFailed", [new("symbol", pending.Symbol), new("message", ex.Message)]);
            }
            finally
            {
                pending.Cancellation.Dispose();
            }
        }

        int capacity = Math.Max(0, MaximumInFlightRequests - _pending.Count);
        foreach (string symbol in TakeNextSymbols(orderedSymbols, capacity))
        {
            CancellationTokenSource requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task<IReadOnlyList<QuoteSnapshot>> request = RequestOneAsync(symbol, quoteProvider, requestCancellation.Token);
            _pending.Add(symbol, new PendingRequest(symbol, request, requestCancellation));
        }

        RuntimeQuoteSeedStore.Publish(_memory.Values);
        return new ProgressiveQuoteRefreshResult(
            updated.Values.Select(Clone).ToList(),
            SnapshotMemory(),
            failures,
            _pending.Count,
            _providerHealth.Snapshot);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (PendingRequest pending in _pending.Values)
        {
            pending.Cancellation.Cancel();
            pending.Cancellation.Dispose();
        }

        _pending.Clear();
        _rateLimitGuard.Dispose();
    }

    private async Task<IReadOnlyList<QuoteSnapshot>> RequestOneAsync(
        string symbol,
        IQuoteProvider quoteProvider,
        CancellationToken cancellationToken)
    {
        await _rateLimitGuard.WaitIfNeededAsync(MinimumRequestSpacing, cancellationToken).ConfigureAwait(false);
        return await _retryPolicy.ExecuteAsync(
            () => quoteProvider.GetQuotesAsync([symbol], cancellationToken),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private IReadOnlyList<string> TakeNextSymbols(IReadOnlyList<string> orderedSymbols, int capacity)
    {
        if (orderedSymbols.Count == 0 || capacity == 0)
            return [];

        List<string> selected = [];
        for (int offset = 0; offset < orderedSymbols.Count && selected.Count < capacity; offset++)
        {
            string symbol = orderedSymbols[(_cursor + offset) % orderedSymbols.Count];
            if (_pending.ContainsKey(symbol) || selected.Contains(symbol, StringComparer.OrdinalIgnoreCase))
                continue;

            selected.Add(symbol);
        }

        _cursor = (_cursor + Math.Max(1, selected.Count)) % orderedSymbols.Count;
        return selected;
    }

    private void Prime(IEnumerable<QuoteSnapshot> quotes)
    {
        foreach (QuoteSnapshot quote in quotes.Where(HasUsableValue))
            _memory[quote.Symbol] = Clone(quote);
    }

    private IReadOnlyDictionary<string, QuoteSnapshot> SnapshotMemory()
        => _memory.ToDictionary(static pair => pair.Key, static pair => Clone(pair.Value), StringComparer.OrdinalIgnoreCase);

    private static bool HasUsableValue(QuoteSnapshot quote)
        => !string.IsNullOrWhiteSpace(quote.Symbol) && (quote.Last.HasValue || quote.PreviousClose.HasValue);

    private static QuoteSnapshot Clone(QuoteSnapshot source)
        => new()
        {
            Symbol = source.Symbol,
            Last = source.Last,
            Change = source.Change,
            ChangePercent = source.ChangePercent,
            PreviousClose = source.PreviousClose,
            Currency = source.Currency,
            ExchangeTimeZoneId = source.ExchangeTimeZoneId,
            MarketSession = source.MarketSession,
            ProviderTimestampUtc = source.ProviderTimestampUtc,
            FetchTimestampUtc = source.FetchTimestampUtc,
            IsStale = source.IsStale
        };

    private sealed record PendingRequest(string Symbol, Task<IReadOnlyList<QuoteSnapshot>> Task, CancellationTokenSource Cancellation);
}

public sealed record ProgressiveQuoteRefreshResult(
    IReadOnlyList<QuoteSnapshot> UpdatedQuotes,
    IReadOnlyDictionary<string, QuoteSnapshot> CachedQuotes,
    IReadOnlyList<string> FailedSymbols,
    int InFlightRequestCount,
    ProviderHealthSnapshot ProviderHealth);
