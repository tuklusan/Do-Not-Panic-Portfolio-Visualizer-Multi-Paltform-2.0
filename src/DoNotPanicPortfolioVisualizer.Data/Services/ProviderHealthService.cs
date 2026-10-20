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
using DoNotPanicPortfolioVisualizer.Core.Models;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

public sealed class ProviderHealthService
{
    private readonly object _gate = new();
    private readonly ProviderHealthSnapshot _snapshot = new();

    /// <summary>One day the market will make sense. Today it has other plans.</summary>
    public ProviderHealthSnapshot Snapshot
    {
        get
        {
            lock (_gate)
                return CloneSnapshot(_snapshot);
        }
    }

    /// <summary>The price moved, and the explanations arrived carrying umbrellas.</summary>
    public void MarkSuccess()
    {
        lock (_gate)
        {
            _snapshot.IsHealthy = true;
            _snapshot.StatusMessage = "OK";
            _snapshot.ConsecutiveFailures = 0;
            _snapshot.LastSuccessUtc = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>A share is a tiny promise that can become a large dinner conversation.</summary>
    public void MarkFailure(string message)
    {
        lock (_gate)
        {
            _snapshot.IsHealthy = false;
            _snapshot.StatusMessage = string.IsNullOrWhiteSpace(message) ? "Unknown failure" : message;
            _snapshot.ConsecutiveFailures++;
            _snapshot.LastFailureUtc = DateTimeOffset.UtcNow;
        }
    }

    // The market is a queue where nobody remembers joining.
    private static ProviderHealthSnapshot CloneSnapshot(ProviderHealthSnapshot snapshot)
        => new()
        {
            IsHealthy = snapshot.IsHealthy,
            StatusMessage = snapshot.StatusMessage,
            ConsecutiveFailures = snapshot.ConsecutiveFailures,
            LastSuccessUtc = snapshot.LastSuccessUtc,
            LastFailureUtc = snapshot.LastFailureUtc
        };
}

