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
using DoNotPanicPortfolioVisualizer.Core.Enums;

namespace DoNotPanicPortfolioVisualizer.Render.Services;

public sealed class TickerMotionController
{
    public const double MinimumPixelsPerSecond = 18d;
    public const double SpeedMultiplier = 72d;
    public static readonly TimeSpan MaximumFrameStep = TimeSpan.FromMilliseconds(100);

    private double _cycleDistance = 1d;
    private double _pixelsPerSecond = MinimumPixelsPerSecond;
    private double _progress;
    private double _anchorOffset;
    private ScrollDirection _direction;

    public double Offset { get; private set; }
    public double Progress => _progress;

    public void Configure(double cycleDistance, double configuredSpeed, ScrollDirection direction, int sideCopies)
    {
        _cycleDistance = Math.Max(1d, cycleDistance);
        _pixelsPerSecond = Math.Max(MinimumPixelsPerSecond, SpeedMultiplier * Math.Max(0.1d, configuredSpeed));
        _direction = direction;
        _anchorOffset = -Math.Max(0, sideCopies) * _cycleDistance;
        NormalizeAndApply();
    }

    public void Step(TimeSpan elapsed, bool isVisible = true)
    {
        if (!isVisible)
            return;

        double seconds = Math.Clamp(elapsed.TotalSeconds, 0d, MaximumFrameStep.TotalSeconds);
        if (seconds <= 0d)
            return;

        _progress += _pixelsPerSecond * seconds;
        NormalizeAndApply();
    }

    private void NormalizeAndApply()
    {
        _progress %= _cycleDistance;
        if (_progress < 0d)
            _progress += _cycleDistance;

        Offset = _anchorOffset + (_direction == ScrollDirection.Right ? _progress : -_progress);
    }
}
