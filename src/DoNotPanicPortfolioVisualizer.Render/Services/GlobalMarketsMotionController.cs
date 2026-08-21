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
namespace DoNotPanicPortfolioVisualizer.Render.Services;

public sealed class GlobalMarketsMotionController
{
    public const double CardWidth = 164d;
    public const double CopySpacing = 14d;
    public const double PixelsPerSecond = 30d;
    private const double MaximumFrameSeconds = 0.1d;

    public double Offset { get; private set; }
    public double SequenceWidth { get; private set; }
    public int RequiredCopies { get; private set; }
    public double TrackWidth => SequenceWidth * RequiredCopies;

    public bool Configure(double viewportWidth, int scrollingMarketCount)
    {
        double nextSequenceWidth = Math.Max(1d, scrollingMarketCount * (CardWidth + CopySpacing));
        int nextCopies = Math.Max(2, (int)Math.Ceiling(Math.Max(1d, viewportWidth) / nextSequenceWidth) + 1);
        bool changed = !SequenceWidth.Equals(nextSequenceWidth) || RequiredCopies != nextCopies;
        SequenceWidth = nextSequenceWidth;
        RequiredCopies = nextCopies;
        NormalizeOffset();
        return changed;
    }

    public void Step(TimeSpan elapsed)
    {
        if (SequenceWidth <= 0d || RequiredCopies < 2)
            return;

        double elapsedSeconds = Math.Clamp(elapsed.TotalSeconds, 0d, MaximumFrameSeconds);
        Offset -= PixelsPerSecond * elapsedSeconds;
        NormalizeOffset();
    }

    private void NormalizeOffset()
    {
        if (SequenceWidth <= 0d)
        {
            Offset = 0d;
            return;
        }

        Offset %= SequenceWidth;
        if (Offset > 0d)
            Offset -= SequenceWidth;
    }
}
