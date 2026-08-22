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
namespace DoNotPanicPortfolioVisualizer.Render.Services;

public enum RenderSurfaceHeartbeatSignal
{
    None,
    Heartbeat,
    RecoveryRequested,
    Recovered
}

public readonly record struct RenderSurfaceHeartbeatResult(
    RenderSurfaceHeartbeatSignal Signal,
    TimeSpan ElapsedSinceFrame,
    long AcceptedFrameCount,
    int RecoveryCount,
    int EpisodeAttempt);

public sealed class RenderSurfaceHeartbeatController
{
    public static readonly TimeSpan TraceInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MissingThreshold = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan RecoveryRequestInterval = TimeSpan.FromSeconds(30);
    public const int MaximumRecoveryAttemptsPerEpisode = 3;

    private DateTimeOffset _armedUtc = DateTimeOffset.MaxValue;
    private DateTimeOffset _lastAcceptedFrameUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastHeartbeatTraceUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastRecoveryRequestUtc = DateTimeOffset.MinValue;
    private bool _active;
    private bool _heartbeatMissing;
    private long _acceptedFrameCount;
    private int _recoveryCount;
    private int _episodeAttempts;

    public long AcceptedFrameCount => _acceptedFrameCount;
    public int RecoveryCount => _recoveryCount;
    public int EpisodeAttempts => _episodeAttempts;
    public bool IsHeartbeatMissing => _heartbeatMissing;

    public void Start(DateTimeOffset now)
    {
        _active = true;
        _heartbeatMissing = false;
        _acceptedFrameCount = 0;
        _recoveryCount = 0;
        _episodeAttempts = 0;
        _lastAcceptedFrameUtc = now;
        _lastHeartbeatTraceUtc = DateTimeOffset.MinValue;
        _lastRecoveryRequestUtc = DateTimeOffset.MinValue;
        _armedUtc = now + StartupGracePeriod;
    }

    public void Pause() => _active = false;

    public void Resume(DateTimeOffset now)
    {
        _active = true;
        _heartbeatMissing = false;
        _episodeAttempts = 0;
        _lastAcceptedFrameUtc = now;
        _lastRecoveryRequestUtc = DateTimeOffset.MinValue;
        _armedUtc = now + StartupGracePeriod;
    }

    public void Stop() => _active = false;

    public RenderSurfaceHeartbeatResult AcceptFrame(DateTimeOffset now)
    {
        if (!_active)
            return Snapshot(RenderSurfaceHeartbeatSignal.None, TimeSpan.Zero);

        TimeSpan elapsed = now - _lastAcceptedFrameUtc;
        _lastAcceptedFrameUtc = now;
        _acceptedFrameCount++;
        if (_heartbeatMissing)
        {
            _heartbeatMissing = false;
            _episodeAttempts = 0;
            return Snapshot(RenderSurfaceHeartbeatSignal.Recovered, elapsed);
        }

        if (now - _lastHeartbeatTraceUtc >= TraceInterval)
        {
            _lastHeartbeatTraceUtc = now;
            return Snapshot(RenderSurfaceHeartbeatSignal.Heartbeat, elapsed);
        }

        return Snapshot(RenderSurfaceHeartbeatSignal.None, elapsed);
    }

    public RenderSurfaceHeartbeatResult Inspect(DateTimeOffset now, bool isVisible)
    {
        TimeSpan elapsed = now - _lastAcceptedFrameUtc;
        if (!_active || !isVisible || now < _armedUtc || elapsed <= MissingThreshold)
            return Snapshot(RenderSurfaceHeartbeatSignal.None, elapsed);
        if (_episodeAttempts >= MaximumRecoveryAttemptsPerEpisode)
            return Snapshot(RenderSurfaceHeartbeatSignal.None, elapsed);
        if (_lastRecoveryRequestUtc != DateTimeOffset.MinValue &&
            now - _lastRecoveryRequestUtc < RecoveryRequestInterval)
        {
            return Snapshot(RenderSurfaceHeartbeatSignal.None, elapsed);
        }

        _heartbeatMissing = true;
        _lastRecoveryRequestUtc = now;
        _episodeAttempts++;
        _recoveryCount++;
        return Snapshot(RenderSurfaceHeartbeatSignal.RecoveryRequested, elapsed);
    }

    private RenderSurfaceHeartbeatResult Snapshot(RenderSurfaceHeartbeatSignal signal, TimeSpan elapsed)
        => new(signal, elapsed, _acceptedFrameCount, _recoveryCount, _episodeAttempts);
}
