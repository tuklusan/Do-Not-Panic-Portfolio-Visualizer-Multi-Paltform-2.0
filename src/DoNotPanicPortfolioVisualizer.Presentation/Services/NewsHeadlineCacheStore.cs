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
using DoNotPanicPortfolioVisualizer.Core.Storage;

namespace DoNotPanicPortfolioVisualizer.Presentation.Services;

internal sealed class NewsHeadlineCacheStore
{
    private const int MaximumHeadlineCount = 24;
    private const int MaximumHeadlineLength = 512;
    private const long MaximumCacheBytes = 256 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _cachePath;

    public NewsHeadlineCacheStore(string? cachePath = null)
    {
        _cachePath = string.IsNullOrWhiteSpace(cachePath)
            ? Path.Combine(LocalDataRootResolver.ResolveForCurrentPlatform().CacheRoot, "finance-news-cache.json")
            : Path.GetFullPath(cachePath);
    }

    public async Task<NewsHeadlineCacheEntry?> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_cachePath))
            return null;

        FileInfo info = new(_cachePath);
        if (info.Length <= 0 || info.Length > MaximumCacheBytes)
            return null;

        try
        {
            await using FileStream stream = File.OpenRead(_cachePath);
            NewsHeadlineCacheEntry? entry = await JsonSerializer.DeserializeAsync<NewsHeadlineCacheEntry>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (entry is null || string.IsNullOrWhiteSpace(entry.ModeKey) || entry.Headlines.Count == 0)
                return null;

            entry.Headlines = entry.Headlines
                .Where(static headline => !string.IsNullOrWhiteSpace(headline))
                .Select(static headline => headline.Trim()[..Math.Min(headline.Trim().Length, MaximumHeadlineLength)])
                .Take(MaximumHeadlineCount)
                .ToList();
            return entry.Headlines.Count == 0 ? null : entry;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public async Task SaveAsync(NewsHeadlineCacheEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Headlines = entry.Headlines
            .Where(static headline => !string.IsNullOrWhiteSpace(headline))
            .Select(static headline => headline.Trim()[..Math.Min(headline.Trim().Length, MaximumHeadlineLength)])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumHeadlineCount)
            .ToList();
        if (entry.Headlines.Count == 0)
            return;

        string? directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = _cachePath + ".tmp";
        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, entry, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, _cachePath, overwrite: true);
    }
}

internal sealed class NewsHeadlineCacheEntry
{
    public string ModeKey { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public DateTimeOffset FetchTimestampUtc { get; set; }
    public DateTimeOffset? LatestPublicationUtc { get; set; }
    public List<string> Headlines { get; set; } = [];
}
