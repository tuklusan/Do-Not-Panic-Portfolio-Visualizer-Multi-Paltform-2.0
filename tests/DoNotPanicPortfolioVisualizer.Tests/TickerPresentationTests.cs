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
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Render.Services;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class TickerPresentationTests
{
    [Fact]
    public void TickerFormatter_FormatsPositiveNegativeAndUnavailableValues()
    {
        Assert.Equal("123.46", TickerFormatter.FormatPrice(new QuoteSnapshot { Last = 123.456m }));
        Assert.Equal("+1.25%", TickerFormatter.FormatChange(new QuoteSnapshot { ChangePercent = 1.25m }));
        Assert.Equal("-0.75%", TickerFormatter.FormatChange(new QuoteSnapshot { ChangePercent = -0.75m }));
        Assert.Equal("--", TickerFormatter.FormatPrice(null));
        Assert.Equal("--", TickerFormatter.FormatChange(new QuoteSnapshot()));
    }

    [Fact]
    public void TickerLane_UsesConfiguredTitleDirectionSpeedAndEnabledSymbols()
    {
        TickerGroup source = Defaults.CreateSettings().Groups[1];
        source.Tickers[0].Enabled = false;

        TickerLaneViewModel lane = new(source);

        Assert.Equal(source.Name, lane.Title);
        Assert.Equal(source.Direction, lane.Direction);
        Assert.Equal(source.Speed, lane.Speed);
        Assert.Equal(source.Tickers.Count - 1, lane.Quotes.Count);
        Assert.DoesNotContain(lane.Quotes, quote => quote.Symbol == source.Tickers[0].Symbol);
    }

    [Fact]
    public void TickerAndMacroViewModels_ApplyQuoteTrendAndStaleness()
    {
        QuoteSnapshot quote = new()
        {
            Symbol = "VOO",
            Last = 700.25m,
            ChangePercent = -0.42m,
            IsStale = true
        };
        TickerQuoteViewModel ticker = new(new TickerItem { Symbol = "VOO", DisplayName = "VOO" });
        MacroQuoteViewModel macro = new("S&P 500", "VOO");

        ticker.Apply(quote);
        macro.Apply(quote);

        Assert.Equal("700.25", ticker.PriceText);
        Assert.Equal("-0.42%", ticker.ChangeText);
        Assert.Equal("#FF5A36", ticker.TrendBrush);
        Assert.True(ticker.IsStale);
        Assert.Equal(ticker.PriceText, macro.ValueText);
        Assert.Equal(ticker.ChangeText, macro.ChangeText);
        Assert.Equal(ticker.TrendBrush, macro.AccentBrush);
    }
}
