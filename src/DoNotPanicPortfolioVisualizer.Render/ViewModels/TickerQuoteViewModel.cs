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

    [ObservableProperty]
    private decimal? _last;

    [ObservableProperty]
    private decimal? _changePercent;

    [ObservableProperty]
    private string _flashBrush = "#00FFFFFF";

    [ObservableProperty]
    private double _flashOpacity;

    [ObservableProperty]
    private int _updateSequence;

    private double _flashElapsedSeconds = double.PositiveInfinity;

    public TickerQuoteViewModel(TickerItem ticker)
    {
        Symbol = ticker.Symbol;
        DisplayName = string.IsNullOrWhiteSpace(ticker.DisplayName) ? ticker.Symbol : ticker.DisplayName;
    }

    public string Symbol { get; }
    public string DisplayName { get; }

    public void Apply(QuoteSnapshot quote)
    {
        bool changed = Last.HasValue && quote.Last.HasValue && Last.Value != quote.Last.Value;
        PriceText = TickerFormatter.FormatPrice(quote);
        ChangeText = TickerFormatter.FormatChange(quote);
        TrendBrush = quote.ChangePercent switch
        {
            > 0m => "#39E75F",
            < 0m => "#FF5A36",
            _ => "#D4DEE5"
        };
        IsStale = quote.IsStale;
        Last = quote.Last;
        ChangePercent = quote.ChangePercent;
        if (changed)
        {
            FlashBrush = quote.ChangePercent switch
            {
                > 0m => "#F039E75F",
                < 0m => "#F0FF5A36",
                _ => "#F0FFC440"
            };
            _flashElapsedSeconds = 0d;
            UpdateSequence++;
        }
    }

    public void StepVisuals(TimeSpan elapsed)
    {
        if (double.IsPositiveInfinity(_flashElapsedSeconds))
            return;

        _flashElapsedSeconds += Math.Clamp(elapsed.TotalSeconds, 0d, 0.1d);
        FlashOpacity = SampleFlashOpacity(_flashElapsedSeconds);
        if (_flashElapsedSeconds >= 1.68d)
        {
            FlashOpacity = 0d;
            _flashElapsedSeconds = double.PositiveInfinity;
        }
    }

    private static double SampleFlashOpacity(double seconds)
    {
        if (seconds <= 0.18d)
            return Interpolate(0d, 0.94d, seconds / 0.18d);
        if (seconds <= 0.62d)
            return Interpolate(0.94d, 0.15d, (seconds - 0.18d) / 0.44d);
        if (seconds <= 0.98d)
            return Interpolate(0.15d, 0.85d, (seconds - 0.62d) / 0.36d);
        return Interpolate(0.85d, 0d, (seconds - 0.98d) / 0.70d);
    }

    private static double Interpolate(double start, double end, double progress)
        => start + ((end - start) * Math.Clamp(progress, 0d, 1d));
}
