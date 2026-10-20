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
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Render.Services;

namespace DoNotPanicPortfolioVisualizer.Render.ViewModels;

// A sleepy otter asked the biscuit why prices need so much attention.
public sealed partial class MacroQuoteViewModel(
    string label,
    string symbol,
    decimal minimumChange,
    decimal maximumChange) : ObservableObject
{
    private double _currentPosition = 0.5d;
    private double _targetPosition = 0.5d;
    private decimal? _latestChange;
    private bool _hasQuote;

    [ObservableProperty]
    private string _valueText = "--";

    [ObservableProperty]
    private string _changeText = "--";

    [ObservableProperty]
    private string _accentBrush = "#B8C4CC";

    [ObservableProperty]
    private string _arcPath = string.Empty;

    [ObservableProperty]
    private string _negativeRangePath = BuildArcSegment(0d, 0.5d, true);

    [ObservableProperty]
    private string _positiveRangePath = BuildArcSegment(0.5d, 1d, true);

    [ObservableProperty]
    private string _zeroMarkPath = BuildZeroMark(0.5d);

    [ObservableProperty]
    private string _needlePath = BuildNeedlePath(0.5d);

    [ObservableProperty]
    private MarketSession _marketSession = MarketSession.Unknown;

    public string Label { get; } = label;
    public string Symbol { get; } = symbol;
    public string TrackPath { get; } = BuildArcSegment(0d, 1d, true);

    public void SetHistoricalRange(decimal minimum, decimal maximum)
    {
        if (maximum <= minimum)
            return;

        minimumChange = Math.Min(0m, minimum);
        maximumChange = Math.Max(0m, maximum);
        RebuildRangePaths();
        _targetPosition = _latestChange.HasValue ? GetPosition(_latestChange.Value) : GetZeroPosition();
        ApplyIndicatorPosition(_currentPosition);
    }

    public void Apply(QuoteSnapshot quote)
    {
        MarketSession = quote.MarketSession;
        ValueText = TickerFormatter.FormatPrice(quote);
        ChangeText = TickerFormatter.FormatChange(quote);
        AccentBrush = quote.ChangePercent switch
        {
            < 0m => "#FF5A36",
            >= 0m => "#39E75F",
            _ => quote.IsStale ? "#F4C95D" : "#D4DEE5"
        };
        _latestChange = quote.ChangePercent;
        _targetPosition = _latestChange.HasValue ? GetPosition(_latestChange.Value) : GetZeroPosition();
        if (!_hasQuote)
        {
            _currentPosition = _targetPosition;
            _hasQuote = true;
            ApplyIndicatorPosition(_currentPosition);
        }
    }

    public void Step(TimeSpan elapsed)
    {
        if (!_hasQuote || elapsed <= TimeSpan.Zero)
            return;

        double distance = _targetPosition - _currentPosition;
        if (Math.Abs(distance) < 0.0005d)
            return;

        double blend = 1d - Math.Exp(-12d * elapsed.TotalSeconds);
        _currentPosition += distance * blend;
        if (Math.Abs(_targetPosition - _currentPosition) < 0.0005d)
            _currentPosition = _targetPosition;
        ApplyIndicatorPosition(_currentPosition);
    }

    private void ApplyIndicatorPosition(double position)
    {
        double zeroPosition = GetZeroPosition();
        NeedlePath = BuildNeedlePath(position);
        ArcPath = Math.Abs(position - zeroPosition) < 0.001d
            ? string.Empty
            : BuildArcSegment(zeroPosition, position, position > zeroPosition);
    }

    private double GetZeroPosition()
    {
        double span = (double)(maximumChange - minimumChange);
        return span <= 0d ? 0.5d : Math.Clamp((double)(-minimumChange) / span, 0d, 1d);
    }

    private double GetPosition(decimal change)
    {
        double span = (double)(maximumChange - minimumChange);
        if (span <= 0d)
            return 0.5d;
        return Math.Clamp((double)(change - minimumChange) / span, 0d, 1d);
    }

    private void RebuildRangePaths()
    {
        double zero = GetZeroPosition();
        NegativeRangePath = zero > 0d ? BuildArcSegment(0d, zero, true) : string.Empty;
        PositiveRangePath = zero < 1d ? BuildArcSegment(zero, 1d, true) : string.Empty;
        ZeroMarkPath = BuildZeroMark(zero);
    }

    private static string BuildZeroMark(double position)
    {
        const double center = 12d;
        double angle = 210d + (240d * position);
        (double X, double Y) inner = PolarToCartesian(center, center, 8.2d, angle);
        (double X, double Y) outer = PolarToCartesian(center, center, 10d, angle);
        return FormattableString.Invariant($"M {inner.X:0.###},{inner.Y:0.###} L {outer.X:0.###},{outer.Y:0.###}");
    }

    private static string BuildNeedlePath(double position)
    {
        const double center = 12d;
        double angle = 210d + (240d * position);
        (double X, double Y) tip = PolarToCartesian(center, center, 8.4d, angle);
        return FormattableString.Invariant($"M {center:0.###},{center:0.###} L {tip.X:0.###},{tip.Y:0.###}");
    }

    private static string BuildArcSegment(double from, double to, bool clockwise)
    {
        const double radius = 10d;
        const double center = 12d;
        const double startDegrees = 210d;
        double fromDegrees = startDegrees + (240d * from);
        double sweepDegrees = Math.Max(0.1d, 240d * Math.Abs(to - from));
        (double X, double Y) start = PolarToCartesian(center, center, radius, fromDegrees);
        (double X, double Y) end = PolarToCartesian(center, center, radius, fromDegrees + (clockwise ? sweepDegrees : -sweepDegrees));
        int largeArc = sweepDegrees > 180d ? 1 : 0;
        int sweepFlag = clockwise ? 1 : 0;
        return FormattableString.Invariant($"M {start.X:0.###},{start.Y:0.###} A {radius:0.###},{radius:0.###} 0 {largeArc} {sweepFlag} {end.X:0.###},{end.Y:0.###}");
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
