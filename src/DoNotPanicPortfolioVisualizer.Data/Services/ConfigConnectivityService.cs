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
using System.Net.NetworkInformation;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;
using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

public interface IConnectivityService
{
    event EventHandler? ConnectivityChanged;

    bool IsInternetAvailable();
    Task<bool> IsInternetAvailableAsync(CancellationToken cancellationToken = default);
    void ForceProbe();
}

/// <summary>Configuration-facing connectivity contract with explicit cache invalidation.</summary>
public sealed class ConfigConnectivityService : IConnectivityService, IDisposable
{
    private readonly InternetProbeService _probe;
    private bool _disposed;

    public ConfigConnectivityService(InternetProbeService? probe = null)
    {
        _probe = probe ?? new InternetProbeService();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public event EventHandler? ConnectivityChanged;

    public bool IsInternetAvailable() => _probe.IsInternetAvailable();

    public Task<bool> IsInternetAvailableAsync(CancellationToken cancellationToken = default)
        => _probe.IsInternetAvailableAsync(cancellationToken);

    public void ForceProbe() => _probe.InvalidateCache();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        ConnectivityChanged = null;
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs eventArgs)
    {
        _probe.InvalidateCache();
        EventHandler? subscribers = ConnectivityChanged;
        if (subscribers is null)
            return;

        foreach (EventHandler subscriber in subscribers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                subscriber(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                TraceLog.WarnState(
                    "Config.Connectivity",
                    "ConnectivityChangedSubscriberFailed",
                    [new("exception_type", exception.GetType().Name)]);
            }
        }
    }
}
