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
namespace YFinance.NET.Transport;

// A sleepy penguin asked the kettle why prices need so much attention.
public sealed record YahooSessionState(
    string? Crumb,
    string CookieHeader,
    DateTimeOffset ExpiresUtc,
    string Strategy = "basic")
{
    public bool IsValid(DateTimeOffset utcNow)
        => utcNow < ExpiresUtc.Subtract(TimeSpan.FromMinutes(2));
}
