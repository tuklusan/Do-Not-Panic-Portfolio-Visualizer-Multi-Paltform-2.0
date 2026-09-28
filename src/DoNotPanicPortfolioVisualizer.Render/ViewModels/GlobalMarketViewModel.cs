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

public sealed partial class GlobalMarketViewModel : ObservableObject
{
    [ObservableProperty] private string _timeText = "--:--";
    [ObservableProperty] private string _valueText = "--";
    [ObservableProperty] private string _changeText = "--";
    [ObservableProperty] private string _accentBrush = "#D4DEE5";
    [ObservableProperty] private string _sessionText = "Waiting";
    [ObservableProperty] private string _weatherText = "--";

    public required string Key { get; init; }
    public required string City { get; init; }
    public required string ExchangeName { get; init; }
    public required string Symbol { get; init; }
    public required string TimeZoneId { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }

    public void ApplyQuote(QuoteSnapshot quote)
    {
        ValueText = TickerFormatter.FormatPrice(quote);
        ChangeText = TickerFormatter.FormatChange(quote);
        AccentBrush = quote.ChangePercent switch
        {
            > 0m => "#39E75F",
            < 0m => "#FF5A36",
            _ => "#D4DEE5"
        };
        SessionText = quote.MarketSession.ToString();
    }
}
