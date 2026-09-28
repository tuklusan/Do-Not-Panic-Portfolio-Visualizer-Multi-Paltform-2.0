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
using CommunityToolkit.Mvvm.ComponentModel;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Render.Services;
using System.Globalization;
using System.Text;

namespace DoNotPanicPortfolioVisualizer.Render.ViewModels;

public sealed partial class GlobalMarketViewModel : ObservableObject
{
    [ObservableProperty] private string _timeText = "--:--";
    [ObservableProperty] private string _valueText = "--";
    [ObservableProperty] private string _changeText = "--";
    [ObservableProperty] private string _accentBrush = "#D4DEE5";
    [ObservableProperty] private string _sessionText = "Waiting";
    [ObservableProperty] private string _weatherText = "--";
    [ObservableProperty] private string _miniGraphPath = "M 0,6 L 120,6";

    private readonly Queue<decimal> _graphSamples = new();

    public required string Key { get; init; }
    public required string City { get; init; }
    public required string ExchangeName { get; init; }
    public required string Symbol { get; init; }
    public required string TimeZoneId { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }

    public void ApplyQuote(QuoteSnapshot quote)
    {
        ValueText = TickerFormatter.FormatPrice(quote);
        ChangeText = TickerFormatter.FormatChange(quote);
        AccentBrush = quote.ChangePercent switch
        {
            > 0m => "#39E75F",
            < 0m => "#FF5A36",
            _ => "#D4DEE5"
        };
        SessionText = quote.MarketSession.ToString();

        decimal? value = quote.Last ?? quote.PreviousClose;
        if (value.HasValue)
        {
            if (_graphSamples.Count == 0 && quote.PreviousClose.HasValue)
                _graphSamples.Enqueue(quote.PreviousClose.Value);

            _graphSamples.Enqueue(value.Value);
            while (_graphSamples.Count > 12)
                _graphSamples.Dequeue();

            MiniGraphPath = BuildMiniGraphPath(_graphSamples);
        }
    }

    private static string BuildMiniGraphPath(IEnumerable<decimal> samples)
    {
        decimal[] values = samples.ToArray();
        if (values.Length == 0)
            return "M 0,6 L 120,6";

        decimal minimum = values.Min();
        decimal maximum = values.Max();
        decimal range = Math.Max(0.0001m, maximum - minimum);
        StringBuilder path = new();
        for (int index = 0; index < values.Length; index++)
        {
            double x = values.Length == 1 ? 60d : 120d * index / (values.Length - 1d);
            double y = 10d - (double)((values[index] - minimum) / range * 8m);
            path.Append(index == 0 ? "M " : " L ")
                .Append(x.ToString("0.##", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(y.ToString("0.##", CultureInfo.InvariantCulture));
        }

        return path.ToString();
    }
}
