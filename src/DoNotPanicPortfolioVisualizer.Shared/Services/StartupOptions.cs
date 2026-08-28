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
    private const int MinimumWindowWidth = 960;
    private const int MinimumWindowHeight = 600;
    private const int MaximumWindowDimension = 8192;

    public static bool RequestsFullScreen(IEnumerable<string> arguments)
        => arguments.Any(static argument => string.Equals(argument, "--fullscreen", StringComparison.OrdinalIgnoreCase));

    public static bool TryGetWindowedStartupSize(IEnumerable<string> arguments, out StartupWindowSize size)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string[] optionArguments = arguments as string[] ?? arguments.ToArray();
        if (RequestsFullScreen(optionArguments))
        {
            size = default;
            return false;
        }

        foreach (string argument in optionArguments)
        {
            if (!argument.StartsWith("--windowed=", StringComparison.OrdinalIgnoreCase))
                continue;

            string value = argument["--windowed=".Length..];
            string[] parts = value.Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out int width) ||
                !int.TryParse(parts[1], out int height) ||
                width < MinimumWindowWidth ||
                height < MinimumWindowHeight ||
                width > MaximumWindowDimension ||
                height > MaximumWindowDimension)
            {
                continue;
            }

            size = new StartupWindowSize(width, height);
            return true;
        }

        size = default;
        return false;
    }
}

public readonly record struct StartupWindowSize(int Width, int Height);
