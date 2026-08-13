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
namespace DoNotPanicPortfolioVisualizer.Data.Runtime;

public interface IYFinanceRuntimeClient
{
    Task<YFinanceQuotesResponse> GetQuotesAsync(
        IReadOnlyList<string> requestSymbols,
        CancellationToken cancellationToken = default);

    Task<YFinanceHistoryResponse> GetHistoryAsync(
        string requestSymbol,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        string interval,
        CancellationToken cancellationToken = default);

    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed record YFinanceCacheMetadata(bool Stale);

public sealed record YFinanceQuoteResponse(
    string Symbol,
    decimal? RegularMarketPrice,
    decimal? RegularMarketPreviousClose,
    decimal? RegularMarketChange,
    decimal? RegularMarketChangePercent,
    string? Currency,
    string? ExchangeTimezoneName,
    string? MarketState,
    YFinanceCacheMetadata Cache);

public sealed class YFinanceQuotesResponse
{
    public YFinanceQuotesResponse(
        IReadOnlyList<YFinanceQuoteResponse> quotes,
        IReadOnlyList<string>? missingSymbols = null)
    {
        Quotes = quotes ?? [];
        MissingSymbols = missingSymbols ?? [];
    }

    public IReadOnlyList<YFinanceQuoteResponse> Quotes { get; }
    public IReadOnlyList<string> MissingSymbols { get; }
}

public sealed record YFinanceHistoryBar(DateTimeOffset TimestampUtc, decimal? Close);

public sealed record YFinanceHistoryMetadata(string? ExchangeTimezoneName);

public sealed class YFinanceHistoryResponse
{
    public YFinanceHistoryResponse(
        IReadOnlyList<YFinanceHistoryBar> bars,
        YFinanceHistoryMetadata? metadata = null)
    {
        Bars = bars ?? [];
        Metadata = metadata;
    }

    public IReadOnlyList<YFinanceHistoryBar> Bars { get; }
    public YFinanceHistoryMetadata? Metadata { get; }
}
