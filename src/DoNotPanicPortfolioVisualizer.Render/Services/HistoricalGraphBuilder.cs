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

public sealed class HistoricalGraphBuilder
{
    public FloatingGraphViewModel Build(
        string tapeName,
        TickerHistorySnapshot snapshot,
        decimal? changePercent,
        int sequence)
    {
        const double width = FloatingGraphViewModel.ChartWidth;
        const double height = FloatingGraphViewModel.ChartHeight;
        FloatingGraphViewModel graph = new()
        {
            Symbol = snapshot.Symbol,
            TapeName = tapeName,
            OverlayText = $"{tapeName} | {snapshot.LookbackDays}D"
        };

        if (snapshot.Points.Count == 0)
            return graph;

        decimal min = snapshot.Points.Min(static point => point.Close);
        decimal max = snapshot.Points.Max(static point => point.Close);
        decimal range = Math.Max(0.0001m, max - min);
        StringBuilder path = new();
        for (int index = 0; index < snapshot.Points.Count; index++)
        {
            double x = snapshot.Points.Count == 1 ? width / 2 : width * index / (snapshot.Points.Count - 1d);
            double y = height - ((double)((snapshot.Points[index].Close - min) / range) * height);
            path.Append(index == 0 ? "M " : " L ")
                .Append(x.ToString("0.##", CultureInfo.InvariantCulture)).Append(',')
                .Append(y.ToString("0.##", CultureInfo.InvariantCulture));
        }

        decimal latest = snapshot.Points[^1].Close;
        graph.PathData = path.ToString();
        graph.LastText = latest.ToString("0.00", CultureInfo.InvariantCulture);
        graph.ChangeText = changePercent.HasValue
            ? $"{(changePercent >= 0 ? "+" : string.Empty)}{changePercent:0.00}%"
            : "--";
        graph.AccentBrush = changePercent switch
        {
            > 0m => "#39E75F",
            < 0m => "#FF5A36",
            _ => "#D4DEE5"
        };
        graph.RangeText = $"{min:0.##} - {max:0.##}";
        graph.MaxScaleText = max.ToString("0.##", CultureInfo.InvariantCulture);
        graph.MidScaleText = ((min + max) / 2m).ToString("0.##", CultureInfo.InvariantCulture);
        graph.MinScaleText = min.ToString("0.##", CultureInfo.InvariantCulture);
        graph.LeftTimeScaleText = snapshot.Points[0].TimestampUtc.ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture);
        graph.MiddleTimeScaleText = snapshot.Points[snapshot.Points.Count / 2].TimestampUtc.ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture);
        graph.RightTimeScaleText = snapshot.Points[^1].TimestampUtc.ToLocalTime().ToString("MM/dd", CultureInfo.InvariantCulture);
        return graph;
    }
}
