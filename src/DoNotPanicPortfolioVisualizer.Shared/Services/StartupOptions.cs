// ============================================================================
// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Proprietary rights reserved except as expressly licensed herein.
//
// DO NOT PANIC PORTFOLIO VISUALIZER
// This file is governed by the SANYALnet Labs Non-Commercial License in the
// root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
// for AI/ML model training are prohibited unless separately authorized.
// ============================================================================

namespace DoNotPanicPortfolioVisualizer.Shared.Services;

public static class StartupOptions
{
    public static bool RequestsFullScreen(IEnumerable<string> arguments)
        => arguments.Any(static argument => string.Equals(argument, "--fullscreen", StringComparison.OrdinalIgnoreCase));
}
