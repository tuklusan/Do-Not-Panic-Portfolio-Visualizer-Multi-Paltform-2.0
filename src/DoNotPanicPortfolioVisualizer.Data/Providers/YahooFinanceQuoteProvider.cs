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
using DoNotPanicPortfolioVisualizer.Data.Runtime;
using DoNotPanicPortfolioVisualizer.Data.Services;

namespace DoNotPanicPortfolioVisualizer.Data.Providers;

public sealed class YahooFinanceQuoteProvider : IQuoteProvider
{
    private readonly IYFinanceRuntimeClient _runtimeClient;
    private readonly bool _throwOnPartial;

    public YahooFinanceQuoteProvider(IYFinanceRuntimeClient runtimeClient, bool throwOnPartial = true)
    {
        _runtimeClient = runtimeClient ?? throw new ArgumentNullException(nameof(runtimeClient));
        _throwOnPartial = throwOnPartial;
    }

    public async Task<IReadOnlyList<QuoteSnapshot>> GetQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        List<string> requestedSymbols = symbols
            .Select(YFinanceSymbolMapper.Normalize)
            .Where(static symbol => !string.IsNullOrWhiteSpace(symbol))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requestedSymbols.Count == 0)
            return [];

        IReadOnlyDictionary<string, string> requestByOriginal = BuildRequestMap(requestedSymbols);
        YFinanceQuotesResponse resolved = await _runtimeClient.GetQuotesAsync(
            requestByOriginal.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            cancellationToken).ConfigureAwait(false);

        IReadOnlyList<QuoteSnapshot> results = MapQuotesResponse(requestedSymbols, requestByOriginal, resolved);
        if (results.Count == 0)
            throw new InvalidOperationException("YFinance.NET returned no matching quotes.");

        List<string> unresolved = requestedSymbols
            .Where(symbol => results.All(quote => !string.Equals(quote.Symbol, symbol, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (_throwOnPartial && unresolved.Count > 0)
        {
            throw new PartialQuoteResultException(
                $"YFinance.NET returned partial quotes. Missing: {string.Join(", ", unresolved)}",
                results);
        }

        return results;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<QuoteSnapshot> quotes = await GetQuotesAsync(["AAPL"], cancellationToken).ConfigureAwait(false);
            return quotes.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    internal static IReadOnlyList<QuoteSnapshot> MapQuotesResponse(
        IReadOnlyList<string> requestedSymbols,
        YFinanceQuotesResponse resolved)
        => MapQuotesResponse(requestedSymbols, BuildRequestMap(requestedSymbols), resolved);

    private static IReadOnlyList<QuoteSnapshot> MapQuotesResponse(
        IReadOnlyList<string> requestedSymbols,
        IReadOnlyDictionary<string, string> requestByOriginal,
        YFinanceQuotesResponse resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);

        Dictionary<string, QuoteSnapshot> results = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, YFinanceQuoteResponse> exactByResponseSymbol = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<YFinanceQuoteResponse>> byResponseMatchKey = new(StringComparer.OrdinalIgnoreCase);
        foreach (YFinanceQuoteResponse quote in resolved.Quotes)
        {
            string normalizedSymbol = YFinanceSymbolMapper.Normalize(quote.Symbol);
            if (!string.IsNullOrWhiteSpace(normalizedSymbol))
                exactByResponseSymbol[normalizedSymbol] = quote;

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

        foreach ((string originalSymbol, string requestSymbol) in requestByOriginal)
        {
            if (!TryResolveQuote(
                    requestSymbol,
                    exactByResponseSymbol,
                    byResponseMatchKey,
                    out YFinanceQuoteResponse? quote))
            {
                continue;
            }

            QuoteSnapshot mapped = MapQuote(originalSymbol, quote);
            if (mapped.Last is null && mapped.PreviousClose is null)
                continue;

            results[originalSymbol] = mapped;
        }

        return requestedSymbols.Where(results.ContainsKey).Select(symbol => results[symbol]).ToList();
    }

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

    private static Dictionary<string, string> BuildRequestMap(IEnumerable<string> requestedSymbols)
        => requestedSymbols.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(
            symbol => symbol,
            YFinanceSymbolMapper.ToRequestSymbol,
            StringComparer.OrdinalIgnoreCase);

    private static bool TryResolveQuote(
        string requestSymbol,
        IReadOnlyDictionary<string, YFinanceQuoteResponse> exactByResponseSymbol,
        IReadOnlyDictionary<string, List<YFinanceQuoteResponse>> byResponseMatchKey,
        out YFinanceQuoteResponse? quote)
    {
        string normalizedRequestSymbol = YFinanceSymbolMapper.Normalize(requestSymbol);
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

public sealed class PartialQuoteResultException : HttpRequestException
{
    public PartialQuoteResultException(string message, IReadOnlyList<QuoteSnapshot> partialQuotes)
        : base(message)
    {
        PartialQuotes = partialQuotes;
    }

    public IReadOnlyList<QuoteSnapshot> PartialQuotes { get; }
}
