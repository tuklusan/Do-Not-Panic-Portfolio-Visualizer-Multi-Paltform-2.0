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
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.Render.Services;

public sealed class FloatingGraphMotionController
{
    public const double SafeInset = 12d;
    public const double RefreshTravelMinimumVelocity = 260d;
    public const double RefreshTravelTargetSeconds = 1.4d;
    public const double RefreshTravelMaximumSeconds = 4d;
    public const double CardFlashDurationSeconds = 1.68d;
    private const double MaximumFrameSeconds = 0.1d;
    private readonly Random _random;
    private readonly double _minimumVelocity;
    private readonly double _maximumVelocity;
    private readonly bool _bounceWithinViewport;
    private GraphMotionBounds _bounds;

    public FloatingGraphMotionController(
        double minimumVelocity,
        double maximumVelocity,
        bool bounceWithinViewport,
        int randomSeed = 20260821)
    {
        _minimumVelocity = Math.Max(0d, minimumVelocity);
        _maximumVelocity = Math.Max(_minimumVelocity, maximumVelocity);
        _bounceWithinViewport = bounceWithinViewport;
        _random = new Random(randomSeed);
    }

    public GraphMotionBounds Bounds => _bounds;

    public void ConfigureViewport(double width, double height, IReadOnlyList<FloatingGraphViewModel> graphs)
    {
        _bounds = new GraphMotionBounds(
            SafeInset,
            SafeInset,
            Math.Max(20d, width - (SafeInset * 2d)),
            Math.Max(20d, height - (SafeInset * 2d)));
        SeedMissingLayouts(graphs);
        foreach (FloatingGraphViewModel graph in graphs)
        {
            ClampToBounds(graph);
            if (graph.RefreshTravelDirection != 0)
                graph.RefreshTravelTargetY = GetTravelTarget(graph, graph.RefreshTravelDirection);
        }
        ResolveOverlaps(graphs);
    }

    public void SeedMissingLayouts(IReadOnlyList<FloatingGraphViewModel> graphs)
    {
        if (!_bounds.IsUsable || graphs.Count == 0)
            return;

        List<GraphRect> occupied = graphs
            .Where(static graph => graph.HasMotionState)
            .Select(GetRect)
            .ToList();
        string[] tapeNames = graphs.Select(static graph => graph.TapeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (int tapeIndex = 0; tapeIndex < tapeNames.Length; tapeIndex++)
        {
            FloatingGraphViewModel[] tapeGraphs = graphs
                .Where(graph => string.Equals(graph.TapeName, tapeNames[tapeIndex], StringComparison.OrdinalIgnoreCase))
                .ToArray();
            double segmentTop = _bounds.Top + (tapeIndex * _bounds.Height / Math.Max(1, tapeNames.Length));
            double segmentHeight = _bounds.Height / Math.Max(1, tapeNames.Length);
            for (int graphIndex = 0; graphIndex < tapeGraphs.Length; graphIndex++)
            {
                FloatingGraphViewModel graph = tapeGraphs[graphIndex];
                if (graph.HasMotionState)
                    continue;

                double preferredX = _bounds.Left + ((graphIndex + 1d) / (tapeGraphs.Length + 1d)) *
                    Math.Max(0d, _bounds.Width - graph.Width);
                double preferredY = segmentTop + Math.Max(0d, segmentHeight - graph.Height) / 2d;
                GraphMotionBounds segmentBounds = new(
                    _bounds.Left + 8d,
                    segmentTop + 10d,
                    Math.Max(graph.Width + 4d, _bounds.Width - 16d),
                    Math.Max(graph.Height + 4d, segmentHeight - 20d));
                PlaceGraph(graph, segmentBounds, preferredX, preferredY, occupied);
                graph.VelocityX = NextVelocity();
                graph.VelocityY = NextVelocity();
                graph.NominalVelocityX = graph.VelocityX;
                graph.NominalVelocityY = graph.VelocityY;
                graph.HasMotionState = true;
            }
        }
    }

    public bool ApplyQuote(
        FloatingGraphViewModel graph,
        decimal? last,
        decimal? changePercent,
        bool suppressMotionCue = false)
    {
        bool changed = graph.RawLastValue.HasValue && last.HasValue && graph.RawLastValue.Value != last.Value;
        graph.RawLastValue = last;
        graph.LastText = last?.ToString("0.00", CultureInfo.InvariantCulture) ?? graph.LastText;
        graph.ChangeText = changePercent.HasValue
            ? $"{(changePercent >= 0m ? "+" : string.Empty)}{changePercent:0.00}%"
            : "--";
        graph.AccentBrush = changePercent switch
        {
            > 0m => "#39E75F",
            < 0m => "#FF5A36",
            _ => "#D4DEE5"
        };
        graph.LatestSegmentBrush = graph.AccentBrush;

        if (!changed || suppressMotionCue || graph.IsRefreshTravelFlashActive)
        {
            return false;
        }

        graph.FlashBrush = changePercent switch
        {
            > 0m => "#F039E75F",
            < 0m => "#F0FF5A36",
            _ => "#F0D4DEE5"
        };
        if (changePercent is null or 0m || !_bounds.IsUsable)
        {
            graph.CardFlashElapsedSeconds = 0d;
            graph.IsCardFlashActive = true;
            graph.FlashOpacity = 0d;
            return true;
        }

        graph.RefreshTravelDirection = changePercent > 0m ? -1 : 1;
        graph.RefreshTravelTargetY = GetTravelTarget(graph, graph.RefreshTravelDirection);
        graph.RefreshTravelElapsedSeconds = 0d;
        graph.CardFlashElapsedSeconds = 0d;
        graph.IsCardFlashActive = false;
        graph.IsRefreshTravelFlashActive = true;
        return true;
    }

    public void Step(IReadOnlyList<FloatingGraphViewModel> graphs, TimeSpan elapsed)
    {
        double seconds = Math.Clamp(elapsed.TotalSeconds, 0d, MaximumFrameSeconds);
        if (_bounds.IsUsable)
            ResolveOverlaps(graphs);
        foreach (FloatingGraphViewModel graph in graphs)
        {
            if (graph.IsCardFlashActive)
                ApplyCardFlash(graph, seconds);

            if (!_bounds.IsUsable)
                continue;

            if (!graph.HasMotionState)
                continue;

            if (graph.IsRefreshTravelFlashActive)
                ApplyRefreshTravel(graph, seconds);

            graph.X += graph.VelocityX * seconds;
            graph.Y += graph.VelocityY * seconds;
            BounceAndClamp(graph);
            CompleteRefreshTravelAtBoundary(graph);
        }
    }

    private static void ApplyCardFlash(FloatingGraphViewModel graph, double seconds)
    {
        graph.CardFlashElapsedSeconds += seconds;
        double elapsed = graph.CardFlashElapsedSeconds;
        graph.FlashOpacity = elapsed switch
        {
            <= 0.18d => Interpolate(elapsed, 0d, 0.18d, 0d, 0.925d),
            <= 0.62d => Interpolate(elapsed, 0.18d, 0.62d, 0.925d, 0d),
            <= 0.98d => Interpolate(elapsed, 0.62d, 0.98d, 0d, 0.925d),
            <= CardFlashDurationSeconds => Interpolate(elapsed, 0.98d, CardFlashDurationSeconds, 0.925d, 0d),
            _ => 0d
        };
        if (elapsed < CardFlashDurationSeconds)
            return;

        graph.CardFlashElapsedSeconds = 0d;
        graph.IsCardFlashActive = false;
        graph.FlashOpacity = 0d;
    }

    private static double Interpolate(double value, double start, double end, double from, double to)
    {
        double progress = Math.Clamp((value - start) / (end - start), 0d, 1d);
        return from + ((to - from) * progress);
    }

    public void ResolveOverlaps(IReadOnlyList<FloatingGraphViewModel> graphs)
    {
        if (!_bounds.IsUsable)
            return;

        for (int iteration = 0; iteration < 6; iteration++)
        {
            bool movedAny = false;
            for (int firstIndex = 0; firstIndex < graphs.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1; secondIndex < graphs.Count; secondIndex++)
                {
                    FloatingGraphViewModel first = graphs[firstIndex];
                    FloatingGraphViewModel second = graphs[secondIndex];
                    if (first.IsRefreshTravelFlashActive || second.IsRefreshTravelFlashActive)
                        continue;

                    GraphRect firstRect = GetRect(first);
                    GraphRect secondRect = GetRect(second);
                    if (!firstRect.Intersects(secondRect))
                        continue;

                    double overlapX = Math.Min(firstRect.Right, secondRect.Right) - Math.Max(firstRect.Left, secondRect.Left);
                    double overlapY = Math.Min(firstRect.Bottom, secondRect.Bottom) - Math.Max(firstRect.Top, secondRect.Top);
                    if (overlapX <= 0d || overlapY <= 0d)
                        continue;

                    if (overlapX <= overlapY)
                        SeparateHorizontally(first, second, firstRect, secondRect, overlapX);
                    else
                        SeparateVertically(first, second, firstRect, secondRect, overlapY);
                    ClampToBounds(first);
                    ClampToBounds(second);
                    movedAny = true;
                }
            }

            if (!movedAny)
                return;
        }
    }

    private void ApplyRefreshTravel(FloatingGraphViewModel graph, double seconds)
    {
        if (graph.RefreshTravelTargetY is not double targetY || graph.RefreshTravelDirection == 0)
        {
            ClearRefreshTravel(graph);
            return;
        }

        graph.RefreshTravelElapsedSeconds += seconds;
        if (graph.RefreshTravelElapsedSeconds >= RefreshTravelMaximumSeconds)
        {
            RestoreNominalMotion(graph, targetY);
            return;
        }

        double distance = Math.Abs(targetY - graph.Y);
        double velocity = Math.Max(RefreshTravelMinimumVelocity, distance / RefreshTravelTargetSeconds);
        graph.VelocityX = 0d;
        graph.VelocityY = graph.RefreshTravelDirection * velocity;
        double pulse = 0.28d + (0.66d * Math.Abs(Math.Sin(graph.RefreshTravelElapsedSeconds * Math.PI / 0.22d)));
        graph.FlashOpacity = Math.Clamp(pulse, 0d, 0.94d);
    }

    private void CompleteRefreshTravelAtBoundary(FloatingGraphViewModel graph)
    {
        if (graph.RefreshTravelTargetY is not double targetY)
            return;

        bool reached = graph.RefreshTravelDirection < 0
            ? graph.Y <= targetY + 1d
            : graph.Y >= targetY - 1d;
        if (reached)
            RestoreNominalMotion(graph, targetY);
    }

    private void RestoreNominalMotion(FloatingGraphViewModel graph, double targetY)
    {
        bool traveledToTop = targetY <= _bounds.Top + 1d;
        graph.VelocityX = graph.NominalVelocityX;
        graph.VelocityY = traveledToTop
            ? Math.Abs(graph.NominalVelocityY)
            : -Math.Abs(graph.NominalVelocityY);
        graph.NominalVelocityY = graph.VelocityY;
        ClearRefreshTravel(graph);
    }

    private static void ClearRefreshTravel(FloatingGraphViewModel graph)
    {
        graph.RefreshTravelTargetY = null;
        graph.RefreshTravelDirection = 0;
        graph.RefreshTravelElapsedSeconds = 0d;
        graph.IsRefreshTravelFlashActive = false;
        graph.FlashOpacity = 0d;
    }

    private void BounceAndClamp(FloatingGraphViewModel graph)
    {
        double maxX = Math.Max(_bounds.Left, _bounds.Right - graph.Width);
        double maxY = Math.Max(_bounds.Top, _bounds.Bottom - graph.Height);
        if (graph.X <= _bounds.Left)
        {
            graph.X = _bounds.Left;
            if (_bounceWithinViewport && !graph.IsRefreshTravelFlashActive)
                SetVelocityX(graph, Math.Abs(graph.VelocityX));
        }
        else if (graph.X >= maxX)
        {
            graph.X = maxX;
            if (_bounceWithinViewport && !graph.IsRefreshTravelFlashActive)
                SetVelocityX(graph, -Math.Abs(graph.VelocityX));
        }

        if (graph.Y <= _bounds.Top)
        {
            graph.Y = _bounds.Top;
            if (_bounceWithinViewport && !graph.IsRefreshTravelFlashActive)
                SetVelocityY(graph, Math.Abs(graph.VelocityY));
        }
        else if (graph.Y >= maxY)
        {
            graph.Y = maxY;
            if (_bounceWithinViewport && !graph.IsRefreshTravelFlashActive)
                SetVelocityY(graph, -Math.Abs(graph.VelocityY));
        }
    }

    private void PlaceGraph(
        FloatingGraphViewModel graph,
        GraphMotionBounds placementBounds,
        double preferredX,
        double preferredY,
        ICollection<GraphRect> occupied)
    {
        (double X, double Y)[] offsets =
        [
            (0d, 0d), (36d, 0d), (-36d, 0d), (72d, 0d), (-72d, 0d),
            (0d, 28d), (0d, -28d), (72d, 28d), (-72d, 28d),
            (72d, -28d), (-72d, -28d), (144d, 0d), (-144d, 0d),
            (0d, 56d), (0d, -56d)
        ];
        GraphRect fallback = ClampRect(new GraphRect(preferredX, preferredY, graph.Width, graph.Height), placementBounds);
        foreach ((double offsetX, double offsetY) in offsets)
        {
            GraphRect candidate = ClampRect(
                new GraphRect(preferredX + offsetX, preferredY + offsetY, graph.Width, graph.Height),
                placementBounds);
            if (occupied.All(existing => !existing.Intersects(candidate)))
            {
                ApplyRect(graph, candidate);
                occupied.Add(candidate);
                return;
            }
        }

        ApplyRect(graph, fallback);
        occupied.Add(fallback);
    }

    private static void SeparateHorizontally(
        FloatingGraphViewModel first,
        FloatingGraphViewModel second,
        GraphRect firstRect,
        GraphRect secondRect,
        double overlap)
    {
        double push = (overlap / 2d) + 10d;
        bool firstIsLeft = firstRect.Left <= secondRect.Left;
        first.X += firstIsLeft ? -push : push;
        second.X += firstIsLeft ? push : -push;
        if (!first.IsRefreshTravelFlashActive)
            SetVelocityX(first, firstIsLeft ? -Math.Abs(first.VelocityX) : Math.Abs(first.VelocityX));
        if (!second.IsRefreshTravelFlashActive)
            SetVelocityX(second, firstIsLeft ? Math.Abs(second.VelocityX) : -Math.Abs(second.VelocityX));
    }

    private static void SeparateVertically(
        FloatingGraphViewModel first,
        FloatingGraphViewModel second,
        GraphRect firstRect,
        GraphRect secondRect,
        double overlap)
    {
        double push = (overlap / 2d) + 10d;
        bool firstIsTop = firstRect.Top <= secondRect.Top;
        first.Y += firstIsTop ? -push : push;
        second.Y += firstIsTop ? push : -push;
        if (!first.IsRefreshTravelFlashActive)
            SetVelocityY(first, firstIsTop ? -Math.Abs(first.VelocityY) : Math.Abs(first.VelocityY));
        if (!second.IsRefreshTravelFlashActive)
            SetVelocityY(second, firstIsTop ? Math.Abs(second.VelocityY) : -Math.Abs(second.VelocityY));
    }

    private static void SetVelocityX(FloatingGraphViewModel graph, double velocity)
    {
        graph.VelocityX = velocity;
        graph.NominalVelocityX = velocity;
    }

    private static void SetVelocityY(FloatingGraphViewModel graph, double velocity)
    {
        graph.VelocityY = velocity;
        graph.NominalVelocityY = velocity;
    }

    private double NextVelocity()
    {
        double magnitude = _minimumVelocity;
        if (_maximumVelocity > _minimumVelocity)
            magnitude += _random.NextDouble() * (_maximumVelocity - _minimumVelocity);
        return _random.Next(0, 2) == 0 ? magnitude : -magnitude;
    }

    private double GetTravelTarget(FloatingGraphViewModel graph, int direction)
        => direction < 0 ? _bounds.Top : Math.Max(_bounds.Top, _bounds.Bottom - graph.Height);

    private void ClampToBounds(FloatingGraphViewModel graph)
        => ApplyRect(graph, ClampRect(GetRect(graph), _bounds));

    private static GraphRect ClampRect(GraphRect rect, GraphMotionBounds bounds)
    {
        double maxX = Math.Max(bounds.Left, bounds.Right - rect.Width);
        double maxY = Math.Max(bounds.Top, bounds.Bottom - rect.Height);
        return rect with
        {
            X = Math.Clamp(rect.X, bounds.Left, maxX),
            Y = Math.Clamp(rect.Y, bounds.Top, maxY)
        };
    }

    private static GraphRect GetRect(FloatingGraphViewModel graph)
        => new(graph.X, graph.Y, graph.Width, graph.Height);

    private static void ApplyRect(FloatingGraphViewModel graph, GraphRect rect)
    {
        graph.X = rect.X;
        graph.Y = rect.Y;
    }
}

public readonly record struct GraphMotionBounds(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public bool IsUsable => Width > 0d && Height > 0d;
}

internal readonly record struct GraphRect(double X, double Y, double Width, double Height)
{
    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Intersects(GraphRect other)
        => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
}
