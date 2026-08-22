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
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;
using YFinance.NET.Client;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

internal sealed class DoNotPanicPortfolioVisualizerYFinanceServerClientTraceSink : IYFinanceServerClientTraceSink
{
    public static DoNotPanicPortfolioVisualizerYFinanceServerClientTraceSink Instance { get; } = new();

    public void Info(string eventName, IReadOnlyList<KeyValuePair<string, object?>> fields)
        => TraceLog.InfoState("YFinanceClientProtocol", eventName, fields);

    public void Warn(string eventName, IReadOnlyList<KeyValuePair<string, object?>> fields)
        => TraceLog.WarnState("YFinanceClientProtocol", eventName, fields);

    public void Error(string eventName, IReadOnlyList<KeyValuePair<string, object?>> fields, Exception ex)
        => TraceLog.ErrorState("YFinanceClientProtocol", eventName, fields, ex);
}

