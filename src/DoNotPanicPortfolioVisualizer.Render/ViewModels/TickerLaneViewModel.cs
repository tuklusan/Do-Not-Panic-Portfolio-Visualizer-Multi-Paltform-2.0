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
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Render.Services;

namespace DoNotPanicPortfolioVisualizer.Render.ViewModels;

public sealed partial class TickerLaneViewModel : ObservableObject
{
    public const int MinimumSequenceItemCount = 18;
    public const double ItemWidth = 230d;
    public const double CopySpacing = 20d;
    private const int MaximumVisibleTickerItems = 4;
    private const double LabelCharacterWidth = 7.2d;
    private const double LabelHorizontalPadding = 14d;
    private const double LabelToViewportGap = 0d;
    private const double LaneHorizontalPadding = 4d;

    private readonly TickerMotionController _motion = new();
    private int _sideCopies;
    private double _viewportWidth;

    [ObservableProperty]
    private double _trackOffset;

    [ObservableProperty]
    private double _trackWidth;

    [ObservableProperty]
    private double _laneWidth;

    [ObservableProperty]
    private double _contentViewportWidth;

    public TickerLaneViewModel(TickerGroup group)
    {
        Title = group.Name;
        Direction = group.Direction;
        Speed = group.Speed;
        RowHeight = group.RowHeight <= 0d ? 56d : group.RowHeight;
        Quotes = new ObservableCollection<TickerQuoteViewModel>(
            group.Tickers.Where(static ticker => ticker.Enabled && !string.IsNullOrWhiteSpace(ticker.Symbol))
                .Take(Defaults.MaxTickersPerTape)
                .Select(static ticker => new TickerQuoteViewModel(ticker)));
        Quotes.CollectionChanged += (_, _) => ConfigureViewport(_viewportWidth);
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
        if (double.IsNaN(viewportWidth) || double.IsInfinity(viewportWidth))
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport width must be finite.");
        if (viewportWidth < 0d)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport width cannot be negative.");

        _viewportWidth = Math.Max(1d, viewportWidth);
        if (Quotes.Count == 0)
        {
            TrackItems.Clear();
            TrackWidth = 0d;
            TrackOffset = 0d;
            LaneWidth = GetLabelWidth() + LaneHorizontalPadding;
            ContentViewportWidth = 0d;
            _sideCopies = 0;
            _motion.Stop();
            return;
        }

        IReadOnlyList<TickerQuoteViewModel> sequence = BuildVisualSequence();
        double sequenceWidth = sequence.Count * ItemWidth;
        double cycleDistance = sequenceWidth + CopySpacing;
        double measuredContentWidth = Math.Min(
            Quotes.Count * ItemWidth,
            MaximumVisibleTickerItems * ItemWidth);
        ContentViewportWidth = measuredContentWidth;
        int sideCopies = Math.Max(2, (int)Math.Ceiling(_viewportWidth / cycleDistance) + 2);
        if (sideCopies != _sideCopies || TrackItems.Count == 0)
        {
            _sideCopies = sideCopies;
            TrackItems.Clear();
            for (int copy = 0; copy < (sideCopies * 2) + 1; copy++)
            {
                for (int index = 0; index < sequence.Count; index++)
                {
                    double width = ItemWidth + (index == sequence.Count - 1 ? CopySpacing : 0d);
                    TrackItems.Add(new TickerTrackItemViewModel(sequence[index], width));
                }
            }
        }

        TrackWidth = ((sideCopies * 2) + 1) * cycleDistance;
        LaneWidth = GetLabelWidth() + LabelToViewportGap + ContentViewportWidth + LaneHorizontalPadding;
        _motion.Configure(cycleDistance, Speed, Direction, sideCopies);
        TrackOffset = _motion.Offset;
    }

    public void Step(TimeSpan elapsed, bool isVisible = true)
    {
        if (Quotes.Count == 0)
        {
            _motion.Stop();
            TrackOffset = 0d;
            return;
        }

        _motion.Step(elapsed, isVisible);
        TrackOffset = _motion.Offset;
        foreach (TickerQuoteViewModel quote in Quotes)
            quote.StepVisuals(elapsed);
    }

    private IReadOnlyList<TickerQuoteViewModel> BuildVisualSequence()
    {
        int count = Math.Max(MinimumSequenceItemCount, Quotes.Count);
        List<TickerQuoteViewModel> sequence = new(count);
        for (int index = 0; index < count; index++)
            sequence.Add(Quotes[index % Quotes.Count]);

        return sequence;
    }

    private double GetLabelWidth()
        => (Title.Length * LabelCharacterWidth) + LabelHorizontalPadding;
}

public sealed record TickerTrackItemViewModel(TickerQuoteViewModel Quote, double Width);
