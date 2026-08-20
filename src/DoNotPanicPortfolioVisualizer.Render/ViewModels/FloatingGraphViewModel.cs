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
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private string _lastText = "--";
    [ObservableProperty] private string _changeText = "--";
    [ObservableProperty] private string _accentBrush = "#D4DEE5";
    [ObservableProperty] private string _pathData = string.Empty;
    [ObservableProperty] private string _rangeText = string.Empty;
    [ObservableProperty] private double _velocityX;
    [ObservableProperty] private double _velocityY;

    public string Symbol { get; init; } = string.Empty;
    public string TapeName { get; init; } = string.Empty;
    public double AnchorX { get; init; }
    public double AnchorY { get; init; }
}
