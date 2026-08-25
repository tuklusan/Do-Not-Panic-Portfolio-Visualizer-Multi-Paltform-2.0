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
}
