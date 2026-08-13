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
using System.Net;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Data.Runtime;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

public interface IYahooSymbolValidationService
{
    Task<YahooSymbolValidationResult> ValidateAsync(
        IEnumerable<string> symbols,
        int timeoutSeconds,
        IProgress<YahooSymbolValidationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class YahooSymbolValidationService : IYahooSymbolValidationService
{
    private const int MaxBatchSymbols = 25;
    private readonly IYFinanceRuntimeClient _runtimeClient;

    public YahooSymbolValidationService(IYFinanceRuntimeClient runtimeClient)
    {
        _runtimeClient = runtimeClient ?? throw new ArgumentNullException(nameof(runtimeClient));
    }

    public async Task<YahooSymbolValidationResult> ValidateAsync(
        IEnumerable<string> symbols,
        int timeoutSeconds,
        IProgress<YahooSymbolValidationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<string> normalizedSymbols = symbols
            .Select(Normalize)
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        YahooSymbolValidationResult result = new(normalizedSymbols);
        if (normalizedSymbols.Count == 0)
            return result;

        IReadOnlyList<List<string>> batches = ChunkSymbols(normalizedSymbols, MaxBatchSymbols).ToList();
        for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            List<string> batch = batches[batchIndex];
            try
            {
                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, timeoutSeconds)));
                CancellationToken requestCancellationToken = timeoutSource.Token;
                Dictionary<string, string> requestByOriginal = batch.ToDictionary(
                    symbol => symbol,
                    YFinanceSymbolMapper.ToRequestSymbol,
                    StringComparer.OrdinalIgnoreCase);
                YFinanceQuotesResponse response = await _runtimeClient.GetQuotesAsync(
                    requestByOriginal.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    requestCancellationToken).ConfigureAwait(false);

                Dictionary<string, YFinanceQuoteResponse> exactByResponseSymbol = new(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, List<YFinanceQuoteResponse>> byResponseMatchKey = new(StringComparer.OrdinalIgnoreCase);
                foreach (YFinanceQuoteResponse quote in response.Quotes)
                {
                    string normalizedResponseSymbol = Normalize(quote.Symbol);
                    if (!string.IsNullOrWhiteSpace(normalizedResponseSymbol))
                        exactByResponseSymbol[normalizedResponseSymbol] = quote;

                    string responseKey = YFinanceSymbolMapper.ToResponseMatchKey(quote.Symbol);
                    if (!string.IsNullOrWhiteSpace(responseKey))
                    {
                        if (!byResponseMatchKey.TryGetValue(responseKey, out List<YFinanceQuoteResponse>? matches))
                        {
                            matches = [];
                            byResponseMatchKey[responseKey] = matches;
                        }

                        matches.Add(quote);
                    }
                }

                HashSet<string> resolvedBatchSymbols = new(StringComparer.OrdinalIgnoreCase);
                foreach ((string originalSymbol, string requestSymbol) in requestByOriginal)
                {
                    if (!TryResolveQuote(
                            requestSymbol,
                            exactByResponseSymbol,
                            byResponseMatchKey,
                            out YFinanceQuoteResponse? quote) ||
                        quote is null)
                    {
                        continue;
                    }

                    bool hasLiveData = quote.RegularMarketPrice.HasValue ||
                                       quote.RegularMarketPreviousClose.HasValue ||
                                       quote.RegularMarketChange.HasValue;
                    if (!hasLiveData)
                        continue;

                    string normalized = Normalize(originalSymbol);
                    resolvedBatchSymbols.Add(normalized);
                    string resolvedName = quote.Symbol;
                    result.RecordQuote(normalized, MapQuote(normalized, quote));
                    result.MarkValid(normalized, resolvedName, resolvedName);
                    progress?.Report(new YahooSymbolValidationProgress(normalized, true, resolvedName, "Validated via YFinance.NET"));
                }

                foreach (string requestedSymbol in batch)
                {
                    string normalized = Normalize(requestedSymbol);
                    if (resolvedBatchSymbols.Contains(normalized))
                        continue;

                    result.MarkInvalid(normalized, "YFinance.NET does not recognize this symbol.");
                    progress?.Report(new YahooSymbolValidationProgress(normalized, false, string.Empty, "Failed"));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                result.MarkDeferredBatch(batch, ex.Message);
                foreach (string symbol in batch)
                    progress?.Report(new YahooSymbolValidationProgress(symbol, false, string.Empty, "Validation timed out"));

                MarkRemainingSymbolsDeferred(batches, batchIndex + 1, result, progress, "Validation timed out before remaining symbols could be checked.");
                break;
            }
            catch (Exception ex) when (IsTooManyRequests(ex))
            {
                result.MarkRateLimitedBatch(batch, ex.Message);
                foreach (string symbol in batch)
                    progress?.Report(new YahooSymbolValidationProgress(symbol, false, string.Empty, "Rate limited"));

                MarkRemainingSymbolsRateLimited(batches, batchIndex + 1, result, progress, ex.Message);
                break;
            }
            catch (Exception ex) when (IsRecoverableValidationFailure(ex))
            {
                result.MarkDeferredBatch(batch, ex.Message);
                foreach (string symbol in batch)
                    progress?.Report(new YahooSymbolValidationProgress(symbol, false, string.Empty, "Validation unavailable"));
            }
        }

        return result;
    }

    private static IEnumerable<List<string>> ChunkSymbols(IReadOnlyList<string> symbols, int size)
    {
        if (size <= 0)
            yield break;

        for (int index = 0; index < symbols.Count; index += size)
        {
            int count = Math.Min(size, symbols.Count - index);
            List<string> batch = new(count);
            for (int offset = 0; offset < count; offset++)
                batch.Add(symbols[index + offset]);

            yield return batch;
        }
    }

    private static void MarkRemainingSymbolsDeferred(
        IReadOnlyList<List<string>> batches,
        int startBatchIndex,
        YahooSymbolValidationResult result,
        IProgress<YahooSymbolValidationProgress>? progress,
        string reason)
    {
        foreach (string symbol in batches.Skip(startBatchIndex).SelectMany(batch => batch))
        {
            result.MarkDeferredBatch([symbol], reason);
            progress?.Report(new YahooSymbolValidationProgress(symbol, false, string.Empty, "Skipped"));
        }
    }

    private static void MarkRemainingSymbolsRateLimited(
        IReadOnlyList<List<string>> batches,
        int startBatchIndex,
        YahooSymbolValidationResult result,
        IProgress<YahooSymbolValidationProgress>? progress,
        string reason)
    {
        foreach (string symbol in batches.Skip(startBatchIndex).SelectMany(batch => batch))
        {
            result.MarkRateLimitedBatch([symbol], reason);
            progress?.Report(new YahooSymbolValidationProgress(symbol, false, string.Empty, "Rate limited"));
        }
    }

    private static string Normalize(string? symbol)
        => YFinanceSymbolMapper.NormalizeValidationSymbol(symbol);

    private static bool IsTooManyRequests(Exception ex)
        => ex is HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } ||
           ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
           ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase);

    private static bool IsRecoverableValidationFailure(Exception ex)
        => ex is HttpRequestException or IOException or TimeoutException or InvalidOperationException;

    private static QuoteSnapshot MapQuote(string originalSymbol, YFinanceQuoteResponse quote)
    {
        decimal? last = YFinanceSymbolMapper.NormalizeNumericValue(originalSymbol, quote.RegularMarketPrice);
        decimal? previousClose = YFinanceSymbolMapper.NormalizeNumericValue(originalSymbol, quote.RegularMarketPreviousClose);
        decimal? change = YFinanceSymbolMapper.NormalizeNumericValue(originalSymbol, quote.RegularMarketChange);
        decimal? changePercent = quote.RegularMarketChangePercent;
        if (changePercent is null && last is decimal current && previousClose is decimal prior && prior != 0m)
            changePercent = ((current - prior) / prior) * 100m;

        return new QuoteSnapshot
        {
            Symbol = originalSymbol,
            Last = last,
            Change = change,
            ChangePercent = changePercent,
            PreviousClose = previousClose,
            Currency = quote.Currency ?? "USD",
            ExchangeTimeZoneId = string.IsNullOrWhiteSpace(quote.ExchangeTimezoneName) ? "UTC" : quote.ExchangeTimezoneName,
            MarketSession = YFinanceSymbolMapper.MapMarketSession(quote.MarketState),
            ProviderTimestampUtc = null,
            FetchTimestampUtc = DateTimeOffset.UtcNow,
            IsStale = quote.Cache.Stale
        };
    }

    private static bool TryResolveQuote(
        string requestSymbol,
        IReadOnlyDictionary<string, YFinanceQuoteResponse> exactByResponseSymbol,
        IReadOnlyDictionary<string, List<YFinanceQuoteResponse>> byResponseMatchKey,
        out YFinanceQuoteResponse? quote)
    {
        string normalizedRequestSymbol = Normalize(requestSymbol);
        if (exactByResponseSymbol.TryGetValue(normalizedRequestSymbol, out quote))
            return true;

        string responseMatchKey = YFinanceSymbolMapper.ToResponseMatchKey(requestSymbol);
        if (byResponseMatchKey.TryGetValue(responseMatchKey, out List<YFinanceQuoteResponse>? matches) &&
            matches.Count == 1)
        {
            quote = matches[0];
            return true;
        }

        quote = null;
        return false;
    }
}

public sealed record YahooSymbolValidationProgress(
    string Symbol,
    bool IsValid,
    string ResolvedName,
    string Message);

public sealed class YahooSymbolValidationResult
{
    private readonly Dictionary<string, YahooSymbolValidationEntry> _entries;
    private readonly HashSet<string> _rateLimitedSymbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QuoteSnapshot> _validatedQuotes = new(StringComparer.OrdinalIgnoreCase);

    public YahooSymbolValidationResult(IEnumerable<string> requestedSymbols)
    {
        _entries = requestedSymbols
            .Select(symbol => Normalize(symbol))
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                symbol => symbol,
                symbol => new YahooSymbolValidationEntry(symbol),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, YahooSymbolValidationEntry> Entries => _entries;

    public IReadOnlyList<string> InvalidSymbols => _entries.Values
        .Where(entry => entry.WasChecked && !entry.IsValid)
        .Select(entry => entry.Symbol)
        .OrderBy(symbol => symbol, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyList<string> DeferredSymbols => _entries.Values
        .Where(entry => !entry.WasChecked && !entry.IsValid)
        .Select(entry => entry.Symbol)
        .OrderBy(symbol => symbol, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyList<string> RateLimitedSymbols => _rateLimitedSymbols
        .OrderBy(symbol => symbol, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyDictionary<string, QuoteSnapshot> ValidatedQuotes => _validatedQuotes;

    public bool WasRateLimited => _rateLimitedSymbols.Count > 0;

    public void MarkValid(string symbol, string? shortName, string? longName)
    {
        string normalized = Normalize(symbol);
        if (!_entries.TryGetValue(normalized, out YahooSymbolValidationEntry? entry))
            return;

        entry.IsValid = true;
        entry.WasChecked = true;
        entry.FailureReason = string.Empty;
        entry.DisplayName = !string.IsNullOrWhiteSpace(shortName)
            ? shortName!.Trim()
            : (!string.IsNullOrWhiteSpace(longName) ? longName!.Trim() : entry.DisplayName);
    }

    public void RecordQuote(string symbol, QuoteSnapshot quote)
    {
        string normalized = Normalize(symbol);
        _validatedQuotes[normalized] = quote;
    }

    public void MarkInvalid(string symbol, string reason)
    {
        string normalized = Normalize(symbol);
        if (!_entries.TryGetValue(normalized, out YahooSymbolValidationEntry? entry))
            return;

        entry.IsValid = false;
        entry.WasChecked = true;
        entry.FailureReason = reason;
    }

    public void MarkDeferredBatch(IEnumerable<string> symbols, string reason)
    {
        foreach (string symbol in symbols)
            MarkDeferred(symbol, reason);
    }

    public void MarkRateLimitedBatch(IEnumerable<string> symbols, string reason)
    {
        foreach (string symbol in symbols)
        {
            string normalized = Normalize(symbol);
            _rateLimitedSymbols.Add(normalized);
            MarkDeferred(normalized, string.IsNullOrWhiteSpace(reason)
                ? "YFinance.NET rate limited this validation request."
                : $"YFinance.NET rate limited this validation request: {reason}");
        }
    }

    private void MarkDeferred(string symbol, string reason)
    {
        string normalized = Normalize(symbol);
        if (!_entries.TryGetValue(normalized, out YahooSymbolValidationEntry? entry))
            return;

        entry.IsValid = false;
        entry.WasChecked = false;
        entry.FailureReason = reason;
    }

    private static string Normalize(string? symbol)
        => YFinanceSymbolMapper.NormalizeValidationSymbol(symbol);
}

public sealed class YahooSymbolValidationEntry
{
    public YahooSymbolValidationEntry(string symbol)
    {
        Symbol = symbol;
        IsValid = false;
        WasChecked = false;
    }

    public string Symbol { get; }
    public bool IsValid { get; set; }
    public bool WasChecked { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
}
