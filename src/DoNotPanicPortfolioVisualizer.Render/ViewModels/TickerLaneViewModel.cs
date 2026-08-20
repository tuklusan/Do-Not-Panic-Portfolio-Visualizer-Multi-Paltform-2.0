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
using System.Collections.ObjectModel;
using DoNotPanicPortfolioVisualizer.Core.Models;

namespace DoNotPanicPortfolioVisualizer.Render.ViewModels;

public sealed class TickerLaneViewModel
{
    public TickerLaneViewModel(TickerGroup group)
    {
        Title = group.Name;
        Direction = group.Direction.ToString();
        Speed = group.Speed;
        Quotes = new ObservableCollection<TickerQuoteViewModel>(
            group.Tickers.Where(static ticker => ticker.Enabled && !string.IsNullOrWhiteSpace(ticker.Symbol))
                .Select(static ticker => new TickerQuoteViewModel(ticker)));
    }

    public string Title { get; }
    public string Direction { get; }
    public double Speed { get; }
    public ObservableCollection<TickerQuoteViewModel> Quotes { get; }
}
