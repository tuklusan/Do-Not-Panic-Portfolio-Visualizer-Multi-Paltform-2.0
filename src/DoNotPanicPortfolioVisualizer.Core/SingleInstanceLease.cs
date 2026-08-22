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

namespace DoNotPanicPortfolioVisualizer.Core;

public sealed class SingleInstanceLease : IDisposable
{
    private Mutex? _mutex;
    private bool _ownsMutex;

    private SingleInstanceLease(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    public static bool TryAcquire(string name, out SingleInstanceLease? lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Mutex mutex = new(initiallyOwned: true, name, out bool createdNew);
        bool acquired = createdNew;
        if (!acquired)
        {
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
        }

        if (!acquired)
        {
            mutex.Dispose();
            lease = null;
            return false;
        }

        lease = new SingleInstanceLease(mutex, ownsMutex: true);
        return true;
    }

    public static bool TryAcquireForCurrentUser(string baseName, out SingleInstanceLease? lease)
        => TryAcquire(
            ResolvePlatformName(baseName, OperatingSystem.IsWindows(), Environment.UserName),
            out lease);

    public static string ResolvePlatformName(string baseName, bool isWindows, string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        byte[] userHash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(userName));
        string userSuffix = Convert.ToHexString(userHash.AsSpan(0, 8));
        return isWindows
            ? $"Local\\{baseName}.{userSuffix}"
            : $"{baseName}.{userSuffix}";
    }

    public void Dispose()
    {
        Mutex? mutex = Interlocked.Exchange(ref _mutex, null);
        if (mutex is null)
            return;

        if (_ownsMutex)
        {
            _ownsMutex = false;
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // A process-abort path may have already relinquished ownership.
            }
        }

        mutex.Dispose();
    }
}
