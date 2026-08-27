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
        Assert.Equal("99.50", TickerFormatter.FormatPrice(new QuoteSnapshot { PreviousClose = 99.5m }));
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
        Assert.Equal(source.RowHeight, lane.RowHeight);
        Assert.Equal(source.Tickers.Count - 1, lane.Quotes.Count);
        Assert.DoesNotContain(lane.Quotes, quote => quote.Symbol == source.Tickers[0].Symbol);
        Assert.True(lane.TrackItems.Count >= TickerLaneViewModel.MinimumSequenceItemCount * 5);
    }

    [Fact]
    public void TickerLane_RepeatsTheConfiguredQuotesThroughTheMinimumSequence()
    {
        TickerGroup source = new()
        {
            Tickers =
            [
                new TickerItem { Symbol = "AAA", Enabled = true },
                new TickerItem { Symbol = "BBB", Enabled = true }
            ]
        };
        TickerLaneViewModel lane = new(source);

        Assert.True(lane.TrackItems.Count >= TickerLaneViewModel.MinimumSequenceItemCount * 5);
        Assert.Equal(0, lane.TrackItems.Count % TickerLaneViewModel.MinimumSequenceItemCount);
        Assert.Equal("AAA", lane.TrackItems[0].Quote.Symbol);
        Assert.Equal("BBB", lane.TrackItems[1].Quote.Symbol);
        Assert.Equal("BBB", lane.TrackItems[TickerLaneViewModel.MinimumSequenceItemCount - 1].Quote.Symbol);
        Assert.Equal(TickerLaneViewModel.ItemWidth + TickerLaneViewModel.CopySpacing,
            lane.TrackItems[TickerLaneViewModel.MinimumSequenceItemCount - 1].Width);
        Assert.Equal("AAA", lane.TrackItems[TickerLaneViewModel.MinimumSequenceItemCount].Quote.Symbol);
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
        MacroQuoteViewModel macro = new("S&P 500", "VOO", 1000m);

        ticker.Apply(quote);
        macro.Apply(quote);

        Assert.Equal("700.25", ticker.PriceText);
        Assert.Equal("-0.42%", ticker.ChangeText);
        Assert.Equal("#FF5A36", ticker.TrendBrush);
        Assert.True(ticker.IsStale);
        Assert.Equal(ticker.PriceText, macro.ValueText);
        Assert.Equal(ticker.ChangeText, macro.ChangeText);
        Assert.Equal("#F4C95D", macro.AccentBrush);
        Assert.StartsWith("M ", macro.TrackPath, StringComparison.Ordinal);
        Assert.StartsWith("M ", macro.ArcPath, StringComparison.Ordinal);
        Assert.StartsWith("M 12,12 L ", macro.NeedlePath, StringComparison.Ordinal);
        Assert.False(ticker.IsWaitingOnData);
        Assert.False(ticker.HasMissingData);

        MacroQuoteViewModel invertedRisk = new("VIX", "^VIX", 60m, invertRiskColors: true);
        invertedRisk.Apply(new QuoteSnapshot
        {
            Symbol = "^VIX",
            Last = 30m,
            ChangePercent = 1m
        });
        Assert.Equal("#FF5A36", invertedRisk.AccentBrush);
    }

    [Fact]
    public void TickerQuote_DistinguishesWaitingAndMissingQuoteStates()
    {
        TickerQuoteViewModel ticker = new(new TickerItem { Symbol = "VOO", DisplayName = "VOO" });

        Assert.True(ticker.IsWaitingOnData);
        Assert.Equal("🕒", ticker.WaitingGlyphText);

        ticker.Apply(new QuoteSnapshot { Symbol = "VOO" });

        Assert.True(ticker.IsWaitingOnData);
        Assert.True(ticker.HasMissingData);
        Assert.Equal("◌", ticker.WaitingGlyphText);
        Assert.Equal("#FF8C00", ticker.WaitingGlyphBrush);

        ticker.Apply(new QuoteSnapshot { Symbol = "VOO", PreviousClose = 99.5m });

        Assert.False(ticker.IsWaitingOnData);
        Assert.False(ticker.HasMissingData);
        Assert.Equal(string.Empty, ticker.WaitingGlyphText);
        Assert.Equal(99.5m, ticker.Last);
    }
}
