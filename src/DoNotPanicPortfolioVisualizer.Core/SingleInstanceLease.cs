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
    private FileStream? _lockStream;

    private SingleInstanceLease(FileStream lockStream)
    {
        _lockStream = lockStream;
    }

    public static bool TryAcquire(string lockFilePath, out SingleInstanceLease? lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);

        string fullPath = Path.GetFullPath(lockFilePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("The lock file must have a parent directory.", nameof(lockFilePath));

        Directory.CreateDirectory(directory);
        try
        {
            FileStream stream = new(
                fullPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 1,
                FileOptions.None);
            lease = new SingleInstanceLease(stream);
            return true;
        }
        catch (IOException)
        {
            lease = null;
            return false;
        }
    }

    public static string ResolveLockFileName(string baseFileName, bool isWindows, int sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseFileName);
        if (baseFileName.IndexOfAny(['/', '\\']) >= 0)
            throw new ArgumentException("The lock file name cannot contain path separators.", nameof(baseFileName));
        if (sessionId < 0)
            throw new ArgumentOutOfRangeException(nameof(sessionId));

        if (!isWindows)
            return baseFileName;

        string extension = Path.GetExtension(baseFileName);
        string stem = Path.GetFileNameWithoutExtension(baseFileName);
        return $"{stem}.session-{sessionId}{extension}";
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _lockStream, null)?.Dispose();
    }
}
