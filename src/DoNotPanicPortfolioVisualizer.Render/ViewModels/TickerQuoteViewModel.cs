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
using CommunityToolkit.Mvvm.ComponentModel;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Render.Services;

namespace DoNotPanicPortfolioVisualizer.Render.ViewModels;

public sealed partial class TickerQuoteViewModel : ObservableObject
{
    [ObservableProperty]
    private string _priceText = "--";

    [ObservableProperty]
    private string _changeText = "--";

    [ObservableProperty]
    private string _trendBrush = "#B8C4CC";

    [ObservableProperty]
    private bool _isStale;

    public TickerQuoteViewModel(TickerItem ticker)
    {
        Symbol = ticker.Symbol;
        DisplayName = string.IsNullOrWhiteSpace(ticker.DisplayName) ? ticker.Symbol : ticker.DisplayName;
    }

    public string Symbol { get; }
    public string DisplayName { get; }

    public void Apply(QuoteSnapshot quote)
    {
        PriceText = TickerFormatter.FormatPrice(quote);
        ChangeText = TickerFormatter.FormatChange(quote);
        TrendBrush = quote.ChangePercent switch
        {
            > 0m => "#39E75F",
            < 0m => "#FF5A36",
            _ => "#D4DEE5"
        };
        IsStale = quote.IsStale;
    }
}
