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

namespace DoNotPanicPortfolioVisualizer.Render.ViewModels;

public sealed partial class FloatingGraphViewModel : ObservableObject
{
    public const double CardWidth = 186d;
    public const double CardHeight = 78d;
    public const double ChartWidth = 132d;
    public const double ChartHeight = 40d;

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private string _lastText = "--";
    [ObservableProperty] private string _changeText = "--";
    [ObservableProperty] private string _accentBrush = "#D4DEE5";
    [ObservableProperty] private string _pathData = string.Empty;
    [ObservableProperty] private IReadOnlyList<string> _greenSegmentPaths = [];
    [ObservableProperty] private IReadOnlyList<string> _redSegmentPaths = [];
    [ObservableProperty] private string _latestSegmentPath = string.Empty;
    [ObservableProperty] private string _latestSegmentBrush = "#D4DEE5";
    [ObservableProperty] private string _rangeText = string.Empty;
    [ObservableProperty] private double _velocityX;
    [ObservableProperty] private double _velocityY;
    [ObservableProperty] private double _nominalVelocityX;
    [ObservableProperty] private double _nominalVelocityY;
    [ObservableProperty] private decimal? _rawLastValue;
    [ObservableProperty] private double? _refreshTravelTargetY;
    [ObservableProperty] private int _refreshTravelDirection;
    [ObservableProperty] private bool _isRefreshTravelFlashActive;
    [ObservableProperty] private bool _isCardFlashActive;
    [ObservableProperty] private string _flashBrush = "#00FFFFFF";
    [ObservableProperty] private double _flashOpacity;
    [ObservableProperty] private string _maxScaleText = string.Empty;
    [ObservableProperty] private string _midScaleText = string.Empty;
    [ObservableProperty] private string _minScaleText = string.Empty;
    [ObservableProperty] private string _leftTimeScaleText = string.Empty;
    [ObservableProperty] private string _middleTimeScaleText = string.Empty;
    [ObservableProperty] private string _rightTimeScaleText = string.Empty;
    [ObservableProperty] private string _overlayText = string.Empty;

    public string Symbol { get; init; } = string.Empty;
    public string TapeName { get; init; } = string.Empty;
    public double Width => CardWidth;
    public double Height => CardHeight;
    public double PlotWidth => ChartWidth;
    public double PlotHeight => ChartHeight;
    public bool HasMotionState { get; set; }
    public double RefreshTravelElapsedSeconds { get; set; }
    public double CardFlashElapsedSeconds { get; set; }

    public void CopyContentFrom(FloatingGraphViewModel source)
    {
        LastText = source.LastText;
        ChangeText = source.ChangeText;
        AccentBrush = source.AccentBrush;
        PathData = source.PathData;
        GreenSegmentPaths = source.GreenSegmentPaths;
        RedSegmentPaths = source.RedSegmentPaths;
        LatestSegmentPath = source.LatestSegmentPath;
        LatestSegmentBrush = source.LatestSegmentBrush;
        RangeText = source.RangeText;
        MaxScaleText = source.MaxScaleText;
        MidScaleText = source.MidScaleText;
        MinScaleText = source.MinScaleText;
        LeftTimeScaleText = source.LeftTimeScaleText;
        MiddleTimeScaleText = source.MiddleTimeScaleText;
        RightTimeScaleText = source.RightTimeScaleText;
        OverlayText = source.OverlayText;
    }
}
