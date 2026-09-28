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
namespace DoNotPanicPortfolioVisualizer.Presentation.Services;

public sealed class BackgroundCinemaController
{
    public const double MinimumScale = 1.00d;
    public const double MaximumScale = 1.05d;
    public const double ZoomStepPerTick = 0.00075d;
    public static readonly TimeSpan ZoomTickInterval = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(450);

    private readonly IReadOnlyList<string> _catalog;
    private readonly bool _shuffle;
    private readonly Random _random;
    private int _catalogIndex;
    private bool _activeIsA = true;
    private double _zoomDirection = 1d;
    private double _zoomAccumulatorSeconds;
    private double _transitionSeconds;

    public BackgroundCinemaController(
        IReadOnlyList<string> catalog,
        bool shuffle,
        int randomSeed = 1979,
        double presentationOpacity = 0.45d)
    {
        if (catalog is null || catalog.Count == 0)
            throw new ArgumentException("At least one background is required.", nameof(catalog));

        _catalog = catalog
            .Where(static source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (_catalog.Count == 0)
            throw new ArgumentException("At least one non-empty background is required.", nameof(catalog));

        _shuffle = shuffle;
        _random = new Random(randomSeed);
        PresentationOpacity = Math.Clamp(presentationOpacity, 0d, 1d);
        SourceA = _catalog[0];
        OpacityA = PresentationOpacity;
        ScaleA = 1.01d;
        ScaleB = 1.01d;
    }

    public string SourceA { get; private set; }
    public string? SourceB { get; private set; }
    public double OpacityA { get; private set; }
    public double OpacityB { get; private set; }
    public double ScaleA { get; private set; }
    public double ScaleB { get; private set; }
    public double PresentationOpacity { get; }
    public bool IsTransitioning { get; private set; }
    public string CurrentSource => _activeIsA ? SourceA : SourceB ?? SourceA;

    public bool BeginRotation()
    {
        if (IsTransitioning || _catalog.Count < 2)
            return false;

        int nextIndex = SelectNextIndex();
        string next = _catalog[nextIndex];
        _catalogIndex = nextIndex;
        if (_activeIsA)
        {
            SourceB = next;
            OpacityB = 0d;
            ScaleB = ScaleA;
        }
        else
        {
            SourceA = next;
            OpacityA = 0d;
            ScaleA = ScaleB;
        }

        _transitionSeconds = 0d;
        IsTransitioning = true;
        return true;
    }

    public void Step(TimeSpan elapsed)
    {
        double seconds = Math.Clamp(elapsed.TotalSeconds, 0d, 0.25d);
        if (seconds <= 0d)
            return;

        if (IsTransitioning)
            StepTransition(seconds);
        else
            StepZoom(seconds);
    }

    private int SelectNextIndex()
    {
        if (!_shuffle)
            return (_catalogIndex + 1) % _catalog.Count;

        int selected;
        do
        {
            selected = _random.Next(_catalog.Count);
        }
        while (selected == _catalogIndex);
        return selected;
    }

    private void StepTransition(double seconds)
    {
        _transitionSeconds += seconds;
        double linear = Math.Clamp(_transitionSeconds / TransitionDuration.TotalSeconds, 0d, 1d);
        double eased = 1d - Math.Pow(1d - linear, 3d);
        if (_activeIsA)
            OpacityB = PresentationOpacity * eased;
        else
            OpacityA = PresentationOpacity * eased;

        if (linear < 1d)
            return;

        if (_activeIsA)
        {
            OpacityA = 0d;
            OpacityB = PresentationOpacity;
        }
        else
        {
            OpacityB = 0d;
            OpacityA = PresentationOpacity;
        }

        _activeIsA = !_activeIsA;
        IsTransitioning = false;
        _transitionSeconds = 0d;
        _zoomAccumulatorSeconds = 0d;
    }

    private void StepZoom(double seconds)
    {
        _zoomAccumulatorSeconds += seconds;
        int ticks = (int)(_zoomAccumulatorSeconds / ZoomTickInterval.TotalSeconds);
        if (ticks <= 0)
            return;

        _zoomAccumulatorSeconds -= ticks * ZoomTickInterval.TotalSeconds;
        double scale = (_activeIsA ? ScaleA : ScaleB) + (ticks * ZoomStepPerTick * _zoomDirection);
        if (scale >= MaximumScale)
        {
            scale = MaximumScale;
            _zoomDirection = -1d;
        }
        else if (scale <= MinimumScale)
        {
            scale = MinimumScale;
            _zoomDirection = 1d;
        }

        if (_activeIsA)
            ScaleA = scale;
        else
            ScaleB = scale;
    }
}
