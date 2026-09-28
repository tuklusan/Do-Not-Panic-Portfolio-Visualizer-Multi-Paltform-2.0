// ============================================================================
// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Proprietary rights reserved except as expressly licensed herein.
//
// DO NOT PANIC PORTFOLIO VISUALIZER
// This file is governed by the SANYALnet Labs Non-Commercial License in the
// root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
// for AI/ML model training are prohibited unless separately authorized.
// ============================================================================

using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class StartupOptionsTests
{
    [Theory]
    [InlineData("--fullscreen")]
    [InlineData("--FULLSCREEN")]
    public void RequestsFullScreen_RecognizesTheUpstreamSwitch(string argument)
        => Assert.True(StartupOptions.RequestsFullScreen(["DNPPV", argument]));

    [Fact]
    public void RequestsFullScreen_IgnoresOtherArguments()
        => Assert.False(StartupOptions.RequestsFullScreen(["DNPPV", "--configuration"]));

    [Theory]
    [InlineData("--windowed=1024x768", 1024, 768)]
    [InlineData("--WINDOWED=2560x1600", 2560, 1600)]
    public void TryGetWindowedStartupSize_RecognizesBoundedDimensions(string argument, int width, int height)
    {
        bool parsed = StartupOptions.TryGetWindowedStartupSize(["DNPPV", argument], out StartupWindowSize size);

        Assert.True(parsed);
        Assert.Equal(width, size.Width);
        Assert.Equal(height, size.Height);
    }

    [Theory]
    [InlineData("--windowed=959x768")]
    [InlineData("--windowed=1024x599")]
    [InlineData("--windowed=1024")]
    [InlineData("--windowed=large")]
    [InlineData("--windowed=9000x768")]
    public void TryGetWindowedStartupSize_RejectsMalformedOrUnsupportedDimensions(string argument)
        => Assert.False(StartupOptions.TryGetWindowedStartupSize(["DNPPV", argument], out _));

    [Fact]
    public void TryGetWindowedStartupSize_IgnoresMalformedDuplicateBeforeValidSize()
    {
        bool parsed = StartupOptions.TryGetWindowedStartupSize(
            ["DNPPV", "--windowed=large", "--windowed=1024x768"],
            out StartupWindowSize size);

        Assert.True(parsed);
        Assert.Equal(new StartupWindowSize(1024, 768), size);
    }

    [Fact]
    public void TryGetWindowedStartupSize_GivesFullScreenPrecedence()
    {
        bool parsed = StartupOptions.TryGetWindowedStartupSize(
            ["DNPPV", "--windowed=1024x768", "--fullscreen"],
            out StartupWindowSize size);

        Assert.False(parsed);
        Assert.Equal(default, size);
    }
}
