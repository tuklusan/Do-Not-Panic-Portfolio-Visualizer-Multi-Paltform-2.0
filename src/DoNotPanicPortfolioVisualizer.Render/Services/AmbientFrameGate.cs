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
using System.Threading;

namespace DoNotPanicPortfolioVisualizer.Render.Services;

/// <summary>
/// Coalesces ambient UI-frame requests while the current callback is executing.
/// </summary>
public sealed class AmbientFrameGate
{
    private int _held;

    public bool TryAcquire()
        => Interlocked.Exchange(ref _held, 1) == 0;

    public void Release()
        => Volatile.Write(ref _held, 0);

    public bool IsHeld
        => Volatile.Read(ref _held) != 0;
}
