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

namespace DoNotPanicPortfolioVisualizer.Presentation.Services;

public static class SingleSymbolQuoteRefresh
{
    public static async Task<IReadOnlyList<QuoteSnapshot>> FetchAsync(
        IQuoteProvider quoteProvider,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quoteProvider);
        ArgumentNullException.ThrowIfNull(symbols);

        List<QuoteSnapshot> quotes = [];
        foreach (string symbol in symbols
                     .Where(static symbol => !string.IsNullOrWhiteSpace(symbol))
                     .Select(static symbol => symbol.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<QuoteSnapshot> response = await quoteProvider
                .GetQuotesAsync([symbol], cancellationToken)
                .ConfigureAwait(false);
            quotes.AddRange(response);
        }

        return quotes;
    }
}
