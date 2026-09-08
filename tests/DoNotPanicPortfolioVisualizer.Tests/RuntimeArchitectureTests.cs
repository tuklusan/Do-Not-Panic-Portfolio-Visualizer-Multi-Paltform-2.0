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
using System.Runtime.InteropServices;
using DoNotPanicPortfolioVisualizer.Shared;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class RuntimeArchitectureTests
{
    [Theory]
    [InlineData(true, false, false, Architecture.X64, "win-x64")]
    [InlineData(true, false, false, Architecture.Arm64, "win-arm64")]
    [InlineData(false, true, false, Architecture.X64, "linux-x64")]
    [InlineData(false, true, false, Architecture.Arm64, "linux-arm64")]
    [InlineData(false, false, true, Architecture.X64, "osx-x64")]
    [InlineData(false, false, true, Architecture.Arm64, "osx-arm64")]
    public void GetToken_ReturnsStableSupportedToken(
        bool isWindows,
        bool isLinux,
        bool isMacOS,
        Architecture architecture,
        string expected)
    {
        Assert.Equal(expected, RuntimeArchitecture.GetToken(isWindows, isLinux, isMacOS, architecture));
    }

    [Theory]
    [InlineData(false, false, false, Architecture.X64)]
    [InlineData(true, false, false, Architecture.Arm)]
    public void GetToken_UsesUnknownFallbackForUnsupportedRuntime(
        bool isWindows,
        bool isLinux,
        bool isMacOS,
        Architecture architecture)
    {
        Assert.Equal("unknown-unknown", RuntimeArchitecture.GetToken(isWindows, isLinux, isMacOS, architecture));
    }
}
