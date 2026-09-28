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
using System.Globalization;
using System.Text;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.Render.Services;

public sealed class HistoricalGraphBuildCache
{
    public const int DefaultCapacity = 64;
    private readonly int _capacity;
    private readonly Dictionary<GraphBuildCacheKey, CachedGraph> _entries = [];
    private long _sequence;

    public HistoricalGraphBuildCache(int capacity = DefaultCapacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _capacity = capacity;
    }

    public int Count => _entries.Count;

    public FloatingGraphViewModel GetOrBuild(
        string tapeName,
        TickerHistorySnapshot snapshot,
        decimal? changePercent,
        bool bounceWithinViewport,
        Func<FloatingGraphViewModel> factory)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(factory);

        GraphBuildCacheKey key = new(tapeName, snapshot.Symbol);
        string signature = BuildSignature(snapshot, changePercent, bounceWithinViewport);
        if (_entries.TryGetValue(key, out CachedGraph? cached) &&
            string.Equals(cached.Signature, signature, StringComparison.Ordinal))
        {
            _entries[key] = cached with { LastUsed = ++_sequence };
            return cached.Graph;
        }

        FloatingGraphViewModel graph = factory();
        _entries[key] = new(signature, graph, ++_sequence);
        Trim();
        return graph;
    }

    public void Clear() => _entries.Clear();

    private void Trim()
    {
        while (_entries.Count > _capacity)
        {
            GraphBuildCacheKey oldest = _entries
                .OrderBy(static pair => pair.Value.LastUsed)
                .Select(static pair => pair.Key)
                .First();
            _entries.Remove(oldest);
        }
    }

    private static string BuildSignature(
        TickerHistorySnapshot snapshot,
        decimal? changePercent,
        bool bounceWithinViewport)
    {
        StringBuilder builder = new();
        Append(builder, snapshot.Symbol.ToUpperInvariant());
        Append(builder, snapshot.FetchTimestampUtc.UtcTicks.ToString(CultureInfo.InvariantCulture));
        Append(builder, snapshot.LookbackDays.ToString(CultureInfo.InvariantCulture));
        Append(builder, snapshot.SeriesKind.ToString());
        Append(builder, snapshot.ExchangeTimeZoneId);
        Append(builder, changePercent?.ToString(CultureInfo.InvariantCulture) ?? "<null>");
        Append(builder, bounceWithinViewport ? "1" : "0");
        Append(builder, snapshot.Points.Count.ToString(CultureInfo.InvariantCulture));
        foreach (HistoricalPricePoint point in snapshot.Points)
        {
            Append(builder, point.TimestampUtc.UtcTicks.ToString(CultureInfo.InvariantCulture));
            Append(builder, point.Close.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string? value)
    {
        string text = value ?? string.Empty;
        builder.Append(text.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(text);
    }

    private readonly record struct GraphBuildCacheKey(string TapeName, string Symbol)
    {
        public bool Equals(GraphBuildCacheKey other)
            => string.Equals(TapeName, other.TapeName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Symbol, other.Symbol, StringComparison.OrdinalIgnoreCase);

        public override int GetHashCode()
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(TapeName ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(Symbol ?? string.Empty));
    }

    private sealed record CachedGraph(string Signature, FloatingGraphViewModel Graph, long LastUsed);
}
