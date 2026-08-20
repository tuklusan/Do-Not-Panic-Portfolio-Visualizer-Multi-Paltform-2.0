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
using System.Globalization;
using DoNotPanicPortfolioVisualizer.Core.Models;

namespace DoNotPanicPortfolioVisualizer.Render.Services;

public static class TickerFormatter
{
    public static string FormatPrice(QuoteSnapshot? quote)
        => quote?.Last is decimal value
            ? value.ToString(value >= 1000m ? "N0.##" : "N2", CultureInfo.InvariantCulture)
            : "--";

    public static string FormatChange(QuoteSnapshot? quote)
        => quote?.ChangePercent is decimal value
            ? value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%"
            : "--";

    public static string FormatUpdatedSymbol(QuoteSnapshot quote)
        => $"{quote.Symbol}  {FormatPrice(quote)}  {FormatChange(quote)}";
}
