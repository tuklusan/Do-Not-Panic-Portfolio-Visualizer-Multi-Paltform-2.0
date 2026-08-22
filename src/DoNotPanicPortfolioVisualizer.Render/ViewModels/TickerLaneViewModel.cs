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
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Render.Services;

namespace DoNotPanicPortfolioVisualizer.Render.ViewModels;

public sealed partial class TickerLaneViewModel : ObservableObject
{
    public const double ItemWidth = 230d;
    public const double CopySpacing = 20d;

    private readonly TickerMotionController _motion = new();
    private int _sideCopies;

    [ObservableProperty]
    private double _trackOffset;

    [ObservableProperty]
    private double _trackWidth;

    public TickerLaneViewModel(TickerGroup group)
    {
        Title = group.Name;
        Direction = group.Direction;
        Speed = group.Speed;
        RowHeight = group.RowHeight <= 0d ? 56d : group.RowHeight;
        Quotes = new ObservableCollection<TickerQuoteViewModel>(
            group.Tickers.Where(static ticker => ticker.Enabled && !string.IsNullOrWhiteSpace(ticker.Symbol))
                .Select(static ticker => new TickerQuoteViewModel(ticker)));
        TrackItems = [];
        ConfigureViewport(1024d);
    }

    public string Title { get; }
    public ScrollDirection Direction { get; }
    public double Speed { get; }
    public double RowHeight { get; }
    public ObservableCollection<TickerQuoteViewModel> Quotes { get; }
    public ObservableCollection<TickerTrackItemViewModel> TrackItems { get; }
    public double MotionProgress => _motion.Progress;

    public void ConfigureViewport(double viewportWidth)
    {
        if (Quotes.Count == 0)
        {
            TrackItems.Clear();
            TrackWidth = 0d;
            TrackOffset = 0d;
            return;
        }

        double sequenceWidth = Quotes.Count * ItemWidth;
        double cycleDistance = sequenceWidth + CopySpacing;
        int sideCopies = Math.Max(2, (int)Math.Ceiling(Math.Max(1d, viewportWidth) / cycleDistance) + 2);
        if (sideCopies != _sideCopies || TrackItems.Count == 0)
        {
            _sideCopies = sideCopies;
            TrackItems.Clear();
            for (int copy = 0; copy < (sideCopies * 2) + 1; copy++)
            {
                for (int index = 0; index < Quotes.Count; index++)
                {
                    double width = ItemWidth + (index == Quotes.Count - 1 ? CopySpacing : 0d);
                    TrackItems.Add(new TickerTrackItemViewModel(Quotes[index], width));
                }
            }
        }

        TrackWidth = ((sideCopies * 2) + 1) * cycleDistance;
        _motion.Configure(cycleDistance, Speed, Direction, sideCopies);
        TrackOffset = _motion.Offset;
    }

    public void Step(TimeSpan elapsed, bool isVisible = true)
    {
        _motion.Step(elapsed, isVisible);
        TrackOffset = _motion.Offset;
        foreach (TickerQuoteViewModel quote in Quotes)
            quote.StepVisuals(elapsed);
    }
}

public sealed record TickerTrackItemViewModel(TickerQuoteViewModel Quote, double Width);
