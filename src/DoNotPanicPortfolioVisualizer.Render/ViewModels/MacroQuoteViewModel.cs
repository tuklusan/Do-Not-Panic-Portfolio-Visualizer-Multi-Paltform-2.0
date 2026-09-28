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

public sealed partial class MacroQuoteViewModel(
    string label,
    string symbol,
    decimal maximumValue,
    bool invertRiskColors = false) : ObservableObject
{
    [ObservableProperty]
    private string _valueText = "--";

    [ObservableProperty]
    private string _changeText = "--";

    [ObservableProperty]
    private string _accentBrush = "#B8C4CC";

    [ObservableProperty]
    private string _arcPath = BuildArcPath(0d);

    [ObservableProperty]
    private string _needlePath = BuildNeedlePath(0d);

    public string Label { get; } = label;
    public string Symbol { get; } = symbol;
    public string TrackPath { get; } = BuildArcPath(1d);

    public void Apply(QuoteSnapshot quote)
    {
        ValueText = TickerFormatter.FormatPrice(quote);
        ChangeText = TickerFormatter.FormatChange(quote);
        string upBrush = invertRiskColors ? "#FF5A36" : "#39E75F";
        string downBrush = invertRiskColors ? "#39E75F" : "#FF5A36";
        AccentBrush = quote.IsStale ? "#F4C95D" : quote.ChangePercent switch
        {
            > 0m => upBrush,
            < 0m => downBrush,
            _ => "#D4DEE5"
        };
        decimal? last = quote.Last ?? quote.PreviousClose;
        SetFill(last.HasValue
            ? (double)Math.Clamp(last.Value / Math.Max(1m, maximumValue), 0m, 1m)
            : 0d);
    }

    private void SetFill(double fill)
    {
        double normalized = Math.Clamp(fill, 0d, 1d);
        ArcPath = BuildArcPath(normalized);
        NeedlePath = BuildNeedlePath(normalized);
    }

    private static string BuildArcPath(double fill)
    {
        const double radius = 10d;
        const double center = 12d;
        const double startDegrees = 210d;
        double sweepDegrees = Math.Max(2d, 240d * fill);
        (double X, double Y) start = PolarToCartesian(center, center, radius, startDegrees);
        (double X, double Y) end = PolarToCartesian(center, center, radius, startDegrees + sweepDegrees);
        int largeArc = sweepDegrees > 180d ? 1 : 0;
        return FormattableString.Invariant($"M {start.X:0.###},{start.Y:0.###} A {radius:0.###},{radius:0.###} 0 {largeArc} 1 {end.X:0.###},{end.Y:0.###}");
    }

    private static string BuildNeedlePath(double fill)
    {
        const double center = 12d;
        (double X, double Y) tip = PolarToCartesian(center, center, 8d, 210d + (240d * fill));
        return FormattableString.Invariant($"M {center:0.###},{center:0.###} L {tip.X:0.###},{tip.Y:0.###}");
    }

    private static (double X, double Y) PolarToCartesian(
        double centerX,
        double centerY,
        double radius,
        double angleDegrees)
    {
        double radians = angleDegrees * Math.PI / 180d;
        return (centerX + (radius * Math.Cos(radians)), centerY + (radius * Math.Sin(radians)));
    }
}
