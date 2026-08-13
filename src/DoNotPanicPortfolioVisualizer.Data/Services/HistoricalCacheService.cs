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
using System.Text.Json;
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Data.Interfaces;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

public sealed class HistoricalCacheService : IHistoricalCacheService, IDisposable
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);
    private readonly string _rootFolder;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public HistoricalCacheService(string? rootFolder = null)
    {
        string configuredRoot = string.IsNullOrWhiteSpace(rootFolder)
            ? Defaults.GetHistoricalCacheFolder()
            : Environment.ExpandEnvironmentVariables(rootFolder);

        _rootFolder = configuredRoot;
        Directory.CreateDirectory(_rootFolder);
    }

    internal Action? PurgeStartedForTesting { get; set; }
    internal Action? PurgeIterationForTesting { get; set; }

    public void Dispose() => _gate.Dispose();

    public async Task<TickerHistorySnapshot?> LoadAsync(string symbol, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string path = GetPath(symbol);
            if (!File.Exists(path))
                return null;

            FileInfo info = new(path);
            if (DateTimeOffset.UtcNow - info.LastWriteTimeUtc > MaxAge)
            {
                TryDelete(path);
                return null;
            }

            if (info.Length == 0)
            {
                TryDelete(path);
                return null;
            }

            try
            {
                await using FileStream stream = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<TickerHistorySnapshot>(
                    stream,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                TryDelete(path);
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(TickerHistorySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string path = GetPath(snapshot.Symbol);
            string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            try
            {
                await using FileStream stream = File.Create(tempPath);
                await JsonSerializer.SerializeAsync(stream, snapshot, cancellationToken: cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                File.Move(tempPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                    TryDelete(tempPath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PurgeExpired(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void PurgeExpired(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_rootFolder))
            return;

        PurgeStartedForTesting?.Invoke();
        foreach (string file in Directory.EnumerateFiles(_rootFolder, "*.json", SearchOption.TopDirectoryOnly))
        {
            PurgeIterationForTesting?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();

            FileInfo info = new(file);
            if (DateTimeOffset.UtcNow - info.LastWriteTimeUtc > MaxAge)
                TryDelete(file);
        }
    }

    private string GetPath(string symbol)
    {
        string safe = string.Concat((symbol ?? string.Empty).Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));
        if (string.IsNullOrWhiteSpace(safe))
            safe = "unknown";

        return Path.Combine(_rootFolder, $"{safe.ToUpperInvariant()}.json");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
