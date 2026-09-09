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
namespace YFinance.NET.Config;

public static class YFinanceUpstreamSyncMetadata
{
    // Keep these constants synchronized with YFinance.net/upstream-sync.json whenever an upstream review baseline changes.
    public const string UpstreamRepository = "https://github.com/ranaroussi/yfinance";
    public const string ForkRepository = "https://github.com/tuklusan/yfinance";
    public const string ReviewedCommit = "3d9d2f0cacb662bff689874cd6113bae3a30a885";
    public const string ReviewedCommitDate = "2026-08-26T18:20:38+01:00";
    public const string ReviewedVersion = "1.7.0";
    public const string ReviewedByCr = "CR-108";
}
