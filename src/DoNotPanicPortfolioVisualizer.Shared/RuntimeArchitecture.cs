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

namespace DoNotPanicPortfolioVisualizer.Shared;

public static class RuntimeArchitecture
{
    public static string CurrentToken => GetToken(
        OperatingSystem.IsWindows(),
        OperatingSystem.IsLinux(),
        OperatingSystem.IsMacOS(),
        RuntimeInformation.OSArchitecture);

    public static string GetToken(bool isWindows, bool isLinux, bool isMacOS, Architecture architecture)
    {
        string? os = isWindows ? "win" : isLinux ? "linux" : isMacOS ? "osx" : null;
        string? arch = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        return os is not null && arch is not null ? $"{os}-{arch}" : "unknown-unknown";
    }
}
