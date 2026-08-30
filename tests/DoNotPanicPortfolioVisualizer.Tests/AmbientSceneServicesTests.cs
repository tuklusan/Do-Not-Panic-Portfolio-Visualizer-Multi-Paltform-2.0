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
using System.Net;
using System.Text;
using System.Text.Json;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Media.Services;
using DoNotPanicPortfolioVisualizer.Presentation.Services;
using DoNotPanicPortfolioVisualizer.Render.Services;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class AmbientSceneServicesTests
{
    [Fact]
    public void TickerMotionController_WrapsAtAuditedSpeedInBothDirections()
    {
        TickerMotionController left = new();
        left.Configure(100d, 0.5d, ScrollDirection.Left, 2);
        for (int index = 0; index < 10; index++)
            left.Step(TimeSpan.FromMilliseconds(100));
        Assert.Equal(-236d, left.Offset, 6);

        TickerMotionController right = new();
        right.Configure(100d, 0.5d, ScrollDirection.Right, 2);
        for (int index = 0; index < 10; index++)
            right.Step(TimeSpan.FromMilliseconds(100));
        Assert.Equal(-164d, right.Offset, 6);

        double pausedOffset = left.Offset;
        left.Step(TimeSpan.FromSeconds(5), isVisible: false);
        Assert.Equal(pausedOffset, left.Offset);
        left.Step(TimeSpan.FromMilliseconds(100));
        Assert.Equal(pausedOffset - 3.6d, left.Offset, 6);

        for (int index = 0; index < 10; index++)
            left.Step(TimeSpan.FromMilliseconds(100));
        Assert.InRange(left.Progress, 0d, 99.999999d);
    }

    [Fact]
    public void TickerLane_ResizeAndQuoteRefreshPreserveMotionProgress()
    {
        TickerGroup group = new()
        {
            Name = "TEST",
            Speed = 0.5d,
            Direction = ScrollDirection.Left,
            Tickers = [new TickerItem { Symbol = "TEST", DisplayName = "TEST", Enabled = true }]
        };
        TickerLaneViewModel lane = new(group);
        lane.Step(TimeSpan.FromMilliseconds(100));
        double beforeRefresh = lane.MotionProgress;

        lane.Quotes[0].Apply(new QuoteSnapshot { Symbol = "TEST", Last = 10m, ChangePercent = 1m });
        lane.ConfigureViewport(2560d);

        Assert.NotEqual(0d, beforeRefresh);
        Assert.Equal(beforeRefresh, lane.MotionProgress, 6);
        Assert.True(lane.TrackItems.Count > lane.Quotes.Count * 3);
    }

    [Fact]
    public void GlobalMarketsMotion_DuplicatesForViewportAndWrapsWithoutResumeJump()
    {
        GlobalMarketsMotionController motion = new();

        Assert.True(motion.Configure(700d, 7));
        Assert.Equal(1246d, motion.SequenceWidth);
        Assert.Equal(2, motion.RequiredCopies);
        for (int index = 0; index < 10; index++)
            motion.Step(TimeSpan.FromMilliseconds(100));
        Assert.Equal(-30d, motion.Offset, 6);

        double beforeResize = motion.Offset;
        Assert.True(motion.Configure(2500d, 7));
        Assert.Equal(4, motion.RequiredCopies);
        Assert.Equal(beforeResize, motion.Offset, 6);

        motion.Step(TimeSpan.FromSeconds(30));
        Assert.Equal(beforeResize - 3d, motion.Offset, 6);
        for (int index = 0; index < 500; index++)
            motion.Step(TimeSpan.FromMilliseconds(100));
        Assert.InRange(motion.Offset, -motion.SequenceWidth, 0d);
    }

    [Fact]
    public void RenderHeartbeat_UsesAuditedGraceThresholdCadenceAndAttemptLimit()
    {
        DateTimeOffset started = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        RenderSurfaceHeartbeatController controller = new();
        controller.Start(started);

        Assert.Equal(RenderSurfaceHeartbeatSignal.None, controller.Inspect(started.AddSeconds(9), true).Signal);
        Assert.Equal(RenderSurfaceHeartbeatSignal.RecoveryRequested, controller.Inspect(started.AddSeconds(10), true).Signal);
        Assert.Equal(RenderSurfaceHeartbeatSignal.None, controller.Inspect(started.AddSeconds(39), true).Signal);
        Assert.Equal(RenderSurfaceHeartbeatSignal.RecoveryRequested, controller.Inspect(started.AddSeconds(40), true).Signal);
        Assert.Equal(RenderSurfaceHeartbeatSignal.RecoveryRequested, controller.Inspect(started.AddSeconds(70), true).Signal);
        Assert.Equal(RenderSurfaceHeartbeatSignal.None, controller.Inspect(started.AddSeconds(100), true).Signal);
        Assert.Equal(3, controller.RecoveryCount);
        Assert.Equal(RenderSurfaceHeartbeatController.MaximumRecoveryAttemptsPerEpisode, controller.EpisodeAttempts);
    }

    [Fact]
    public void RenderHeartbeat_RecoveryAndPauseResetTheMissingFrameEpisode()
    {
        DateTimeOffset started = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);
        RenderSurfaceHeartbeatController controller = new();
        controller.Start(started);
        Assert.Equal(RenderSurfaceHeartbeatSignal.RecoveryRequested, controller.Inspect(started.AddSeconds(10), true).Signal);

        RenderSurfaceHeartbeatResult recovered = controller.AcceptFrame(started.AddSeconds(11));
        Assert.Equal(RenderSurfaceHeartbeatSignal.Recovered, recovered.Signal);
        Assert.False(controller.IsHeartbeatMissing);
        Assert.Equal(0, controller.EpisodeAttempts);

        controller.Pause();
        Assert.Equal(RenderSurfaceHeartbeatSignal.None, controller.Inspect(started.AddMinutes(1), true).Signal);
        controller.Resume(started.AddMinutes(1));
        Assert.Equal(RenderSurfaceHeartbeatSignal.None, controller.Inspect(started.AddMinutes(1).AddSeconds(9), true).Signal);
        Assert.Equal(RenderSurfaceHeartbeatSignal.RecoveryRequested, controller.Inspect(started.AddMinutes(1).AddSeconds(10), true).Signal);
    }

    [Fact]
    public void NewsPlayback_VisitsTelegraphPhasesAndAdvancesHeadline()
    {
        NewsPlaybackController playback = new();
        playback.ConfigureViewport(192d);
        playback.SetHeadlines(
        [
            "Markets rally as a deliberately long headline crosses several wrapped lines",
            "Second headline"
        ]);
        HashSet<NewsPlaybackPhase> visited = [];
        double minimumOffset = 0d;

        for (int index = 0; index < 1000 && playback.HeadlineIndex == 0; index++)
        {
            playback.Step(TimeSpan.FromMilliseconds(40));
            visited.Add(playback.Phase);
            minimumOffset = Math.Min(minimumOffset, playback.VerticalOffset);
        }

        Assert.Contains(NewsPlaybackPhase.Typing, visited);
        Assert.Contains(NewsPlaybackPhase.PauseBeforeScroll, visited);
        Assert.Contains(NewsPlaybackPhase.Scrolling, visited);
        Assert.Contains(NewsPlaybackPhase.PauseAfterScroll, visited);
        Assert.Contains(NewsPlaybackPhase.PauseBetweenHeadlines, visited);
        Assert.Contains(NewsPlaybackPhase.AdvanceHeadline, visited);
        Assert.InRange(minimumOffset, -NewsPlaybackController.VisibleLineHeight, -18.9d);
        Assert.Equal(1, playback.HeadlineIndex);
    }

    [Fact]
    public void NewsPlayback_EquivalentRefreshPreservesCurrentPlayback()
    {
        NewsPlaybackController playback = new();
        playback.ConfigureViewport(400d);
        playback.SetHeadlines(["Mixed case headline for the teleprinter"]);
        for (int index = 0; index < 8; index++)
            playback.Step(TimeSpan.FromMilliseconds(40));

        NewsPlaybackPhase phase = playback.Phase;
        string text = playback.DisplayText;
        playback.SetHeadlines(["Mixed case headline for the teleprinter"]);

        Assert.Equal(phase, playback.Phase);
        Assert.Equal(text, playback.DisplayText);
        Assert.Equal(text, text.ToUpperInvariant());
    }

    [Fact]
    public void TickerQuote_FlashesOnlyAfterHydrationAndThenClears()
    {
        TickerQuoteViewModel quote = new(new TickerItem { Symbol = "TEST", Enabled = true });
        quote.Apply(new QuoteSnapshot { Symbol = "TEST", Last = 10m, ChangePercent = 1m });
        Assert.Equal(0, quote.UpdateSequence);

        quote.Apply(new QuoteSnapshot { Symbol = "TEST", Last = 11m, ChangePercent = 2m });
        quote.StepVisuals(TimeSpan.FromMilliseconds(100));
        quote.StepVisuals(TimeSpan.FromMilliseconds(80));
        Assert.Equal(1, quote.UpdateSequence);
        Assert.InRange(quote.FlashOpacity, 0.93d, 0.95d);

        for (int index = 0; index < 16; index++)
            quote.StepVisuals(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0d, quote.FlashOpacity);
    }

    [Fact]
    public void BackgroundImageService_FiltersSupportedFilesAndSortsResults()
    {
        using TemporaryDirectoryScope directory = new();
        File.WriteAllText(Path.Combine(directory.Path, "zeta.png"), string.Empty);
        File.WriteAllText(Path.Combine(directory.Path, "alpha.jpg"), string.Empty);
        File.WriteAllText(Path.Combine(directory.Path, "notes.txt"), string.Empty);

        IReadOnlyList<string> images = new BackgroundImageService().GetImages(directory.Path, false);

        Assert.Equal(2, images.Count);
        Assert.EndsWith("alpha.jpg", images[0], StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("zeta.png", images[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BackgroundCinemaController_CrossFadesAndCanonicalizesLayers()
    {
        BackgroundCinemaController controller = new(["first.jpg", "second.jpg"], shuffle: false);

        Assert.True(controller.BeginRotation());
        controller.Step(TimeSpan.FromMilliseconds(225));
        Assert.True(controller.IsTransitioning);
        Assert.InRange(controller.OpacityB, 0.38d, 0.40d);

        controller.Step(TimeSpan.FromMilliseconds(225));
        Assert.False(controller.IsTransitioning);
        Assert.Equal(0d, controller.OpacityA);
        Assert.Equal(0.45d, controller.OpacityB);
        Assert.Equal("second.jpg", controller.CurrentSource);
    }

    [Fact]
    public void BackgroundCinemaController_ZoomsWithinAuditedBoundsAndReverses()
    {
        BackgroundCinemaController controller = new(["first.jpg"], shuffle: false);

        for (int index = 0; index < 100; index++)
            controller.Step(BackgroundCinemaController.ZoomTickInterval);

        Assert.InRange(controller.ScaleA, BackgroundCinemaController.MinimumScale, BackgroundCinemaController.MaximumScale);
        double upperScale = controller.ScaleA;
        for (int index = 0; index < 100; index++)
            controller.Step(BackgroundCinemaController.ZoomTickInterval);

        Assert.InRange(controller.ScaleA, BackgroundCinemaController.MinimumScale, BackgroundCinemaController.MaximumScale);
        Assert.NotEqual(upperScale, controller.ScaleA);
    }

    [Theory]
    [InlineData(0.09d, 0.78d)]
    [InlineData(0.10d, 0.68d)]
    [InlineData(0.15d, 0.68d)]
    [InlineData(0.16d, 0.58d)]
    [InlineData(0.23d, 0.58d)]
    [InlineData(0.24d, 0.45d)]
    public void BackgroundPresentationOpacity_UsesAuditedLuminanceThresholds(
        double luminance,
        double expectedOpacity)
        => Assert.Equal(expectedOpacity, BackgroundPresentationOpacityPolicy.FromAverageLuminance(luminance));

    [Fact]
    public void BackgroundPresentationOpacity_IgnoresTransparentPixelsAndFallsBackForEmptySamples()
    {
        byte[] pixels =
        [
            0, 0, 0, 0,
            25, 25, 25, 255
        ];

        Assert.Equal(0.78d, BackgroundPresentationOpacityPolicy.FromBgra32(pixels, 2, 1, 8));
        Assert.Equal(0.45d, BackgroundPresentationOpacityPolicy.FromBgra32([0, 0, 0, 0], 1, 1, 4));
        Assert.Equal(0.45d, BackgroundPresentationOpacityPolicy.FromBgra32([], 1, 1, 4));
    }

    [Fact]
    public void HistoricalGraphBuilder_ProducesPortablePathAndTrendState()
    {
        TickerHistorySnapshot snapshot = new()
        {
            Symbol = "VOO",
            LookbackDays = 14,
            Points =
            [
                new HistoricalPricePoint { TimestampUtc = DateTimeOffset.UtcNow.AddDays(-2), Close = 98m },
                new HistoricalPricePoint { TimestampUtc = DateTimeOffset.UtcNow.AddDays(-1), Close = 101m },
                new HistoricalPricePoint { TimestampUtc = DateTimeOffset.UtcNow, Close = 100m }
            ]
        };

        var graph = new HistoricalGraphBuilder().Build("Core", snapshot, -1.25m, 2);

        Assert.StartsWith("M ", graph.PathData, StringComparison.Ordinal);
        Assert.Contains(" L ", graph.PathData, StringComparison.Ordinal);
        Assert.Single(graph.GreenSegmentPaths);
        Assert.Single(graph.RedSegmentPaths);
        Assert.Equal(graph.RedSegmentPaths[0], graph.LatestSegmentPath);
        Assert.Equal("#FF5A36", graph.LatestSegmentBrush);
        Assert.Equal("100.00", graph.LastText);
        Assert.Equal("-1.25%", graph.ChangeText);
        Assert.Equal("#FF5A36", graph.AccentBrush);
    }

    [Fact]
    public void HistoricalGraphBuilder_UsesAuditedCardAndPlotGeometry()
    {
        TickerHistorySnapshot snapshot = new()
        {
            Symbol = "VWO",
            Points = [new HistoricalPricePoint { TimestampUtc = DateTimeOffset.UtcNow, Close = 42m }]
        };

        FloatingGraphViewModel graph = new HistoricalGraphBuilder().Build("Satellite", snapshot, 0.5m, 15);

        Assert.Equal(186d, graph.Width);
        Assert.Equal(78d, graph.Height);
        Assert.Equal(132d, graph.PlotWidth);
        Assert.Equal(40d, graph.PlotHeight);
        Assert.NotEmpty(graph.LeftTimeScaleText);
        Assert.NotEmpty(graph.RightTimeScaleText);
    }

    [Fact]
    public void FloatingGraphMotion_SeedsSixteenCardsInsideFullSceneAtAuditedVelocity()
    {
        FloatingGraphMotionController controller = new(22d, 48d, bounceWithinViewport: true, randomSeed: 17);
        List<FloatingGraphViewModel> graphs = Enumerable.Range(0, 16)
            .Select(index => new FloatingGraphViewModel
            {
                Symbol = $"T{index}",
                TapeName = $"Tape{index % 4}"
            })
            .ToList();

        controller.ConfigureViewport(1024d, 746d, graphs);

        Assert.All(graphs, graph =>
        {
            Assert.True(graph.HasMotionState);
            Assert.InRange(graph.X, controller.Bounds.Left, controller.Bounds.Right - graph.Width);
            Assert.InRange(graph.Y, controller.Bounds.Top, controller.Bounds.Bottom - graph.Height);
            Assert.InRange(Math.Abs(graph.VelocityX), 22d, 48d);
            Assert.InRange(Math.Abs(graph.VelocityY), 22d, 48d);
        });
    }

    [Fact]
    public void FloatingGraphMotion_BouncesAndResolvesCollisions()
    {
        FloatingGraphMotionController controller = new(22d, 48d, bounceWithinViewport: true, randomSeed: 2);
        FloatingGraphViewModel first = new()
        {
            Symbol = "ONE",
            TapeName = "Tape",
            X = 12d,
            Y = 12d,
            VelocityX = -30d,
            VelocityY = -25d,
            NominalVelocityX = -30d,
            NominalVelocityY = -25d,
            HasMotionState = true
        };
        FloatingGraphViewModel second = new()
        {
            Symbol = "TWO",
            TapeName = "Tape",
            X = 80d,
            Y = 20d,
            VelocityX = -28d,
            VelocityY = 24d,
            NominalVelocityX = -28d,
            NominalVelocityY = 24d,
            HasMotionState = true
        };
        List<FloatingGraphViewModel> graphs = [first, second];

        controller.ConfigureViewport(800d, 500d, graphs);
        controller.Step(graphs, TimeSpan.FromMilliseconds(100));

        Assert.True(first.VelocityX > 0d);
        Assert.InRange(first.X, controller.Bounds.Left, controller.Bounds.Right - first.Width);
        Assert.InRange(second.X, controller.Bounds.Left, controller.Bounds.Right - second.Width);
        Assert.False(
            first.X < second.X + second.Width && first.X + first.Width > second.X &&
            first.Y < second.Y + second.Height && first.Y + first.Height > second.Y);
    }

    [Fact]
    public void FloatingGraphMotion_RisingAndFallingQuotesTravelToOppositeBoundsThenResume()
    {
        FloatingGraphMotionController controller = new(22d, 48d, bounceWithinViewport: true, randomSeed: 3);
        FloatingGraphViewModel rising = new() { Symbol = "UP", TapeName = "Tape" };
        FloatingGraphViewModel falling = new() { Symbol = "DOWN", TapeName = "Tape" };
        List<FloatingGraphViewModel> graphs = [rising, falling];
        controller.ConfigureViewport(1000d, 700d, graphs);
        controller.ApplyQuote(rising, 100m, 1m, suppressMotionCue: true);
        controller.ApplyQuote(falling, 100m, -1m, suppressMotionCue: true);

        Assert.True(controller.ApplyQuote(rising, 101m, 1.2m));
        Assert.True(controller.ApplyQuote(falling, 99m, -1.2m));
        controller.Step(graphs, TimeSpan.FromMilliseconds(100));
        Assert.Equal(0d, rising.VelocityX);
        Assert.True(rising.VelocityY <= -FloatingGraphMotionController.RefreshTravelMinimumVelocity);
        Assert.Equal(0d, falling.VelocityX);
        Assert.True(falling.VelocityY >= FloatingGraphMotionController.RefreshTravelMinimumVelocity);

        for (int index = 0; index < 50; index++)
            controller.Step(graphs, TimeSpan.FromMilliseconds(100));

        Assert.False(rising.IsRefreshTravelFlashActive);
        Assert.False(falling.IsRefreshTravelFlashActive);
        Assert.Null(rising.RefreshTravelTargetY);
        Assert.Null(falling.RefreshTravelTargetY);
        Assert.True(rising.VelocityY > 0d);
        Assert.True(falling.VelocityY < 0d);
    }

    [Fact]
    public void FloatingGraphMotion_NeutralQuoteUsesUpstreamMultiPulseFlashTiming()
    {
        FloatingGraphMotionController controller = new(22d, 48d, bounceWithinViewport: true, randomSeed: 3);
        FloatingGraphViewModel graph = new() { Symbol = "FLAT", TapeName = "Tape" };
        controller.ApplyQuote(graph, 100m, 0m, suppressMotionCue: true);

        Assert.True(controller.ApplyQuote(graph, 101m, 0m));
        Assert.True(graph.IsCardFlashActive);
        Assert.False(graph.IsRefreshTravelFlashActive);

        controller.Step([graph], TimeSpan.FromMilliseconds(100));
        controller.Step([graph], TimeSpan.FromMilliseconds(80));
        Assert.InRange(graph.FlashOpacity, 0.92d, 0.93d);
        controller.Step([graph], TimeSpan.FromMilliseconds(100));
        controller.Step([graph], TimeSpan.FromMilliseconds(100));
        controller.Step([graph], TimeSpan.FromMilliseconds(100));
        controller.Step([graph], TimeSpan.FromMilliseconds(100));
        controller.Step([graph], TimeSpan.FromMilliseconds(40));
        Assert.InRange(graph.FlashOpacity, 0d, 0.05d);

        for (int index = 0; index < 12; index++)
            controller.Step([graph], TimeSpan.FromMilliseconds(100));

        Assert.False(graph.IsCardFlashActive);
        Assert.Equal(0d, graph.FlashOpacity);
    }

    [Fact]
    public void FloatingGraphMotion_DirectedImpulseCrossesCrowdedSceneWithoutTimingOut()
    {
        FloatingGraphMotionController controller = new(22d, 48d, bounceWithinViewport: true, randomSeed: 8);
        FloatingGraphViewModel falling = new()
        {
            Symbol = "DOWN",
            TapeName = "Tape",
            X = 180d,
            Y = 80d,
            VelocityX = 25d,
            VelocityY = 25d,
            NominalVelocityX = 25d,
            NominalVelocityY = 25d,
            HasMotionState = true
        };
        List<FloatingGraphViewModel> graphs = [falling];
        for (int index = 0; index < 6; index++)
        {
            graphs.Add(new FloatingGraphViewModel
            {
                Symbol = $"BLOCK{index}",
                TapeName = "Tape",
                X = 180d,
                Y = 170d + (index * 82d),
                VelocityX = index % 2 == 0 ? 22d : -22d,
                VelocityY = 22d,
                NominalVelocityX = index % 2 == 0 ? 22d : -22d,
                NominalVelocityY = 22d,
                HasMotionState = true
            });
        }

        controller.ConfigureViewport(1000d, 700d, graphs);
        controller.ApplyQuote(falling, 100m, -1m, suppressMotionCue: true);
        Assert.True(controller.ApplyQuote(falling, 99m, -1m));

        int frames = 0;
        while (falling.IsRefreshTravelFlashActive && frames < 30)
        {
            controller.Step(graphs, TimeSpan.FromMilliseconds(100));
            frames++;
        }

        Assert.InRange(frames, 1, 30);
        Assert.Equal(controller.Bounds.Bottom - falling.Height, falling.Y, 6);
        Assert.False(falling.IsRefreshTravelFlashActive);
        Assert.True(falling.VelocityY < 0d);
    }

    [Fact]
    public void FloatingGraphMotion_HydrationAndContentReplacementPreserveMotionWithoutImpulse()
    {
        FloatingGraphMotionController controller = new(22d, 48d, bounceWithinViewport: true, randomSeed: 4);
        FloatingGraphViewModel graph = new() { Symbol = "KEEP", TapeName = "Tape" };
        controller.ConfigureViewport(900d, 600d, [graph]);
        controller.ApplyQuote(graph, 10m, 0.5m, suppressMotionCue: true);
        double x = graph.X;
        double y = graph.Y;
        double velocityX = graph.VelocityX;
        double velocityY = graph.VelocityY;
        FloatingGraphViewModel replacement = new()
        {
            Symbol = "KEEP",
            TapeName = "Tape",
            PathData = "M 0,0 L 1,1",
            RangeText = "9 - 11"
        };

        graph.CopyContentFrom(replacement);

        Assert.Equal(x, graph.X);
        Assert.Equal(y, graph.Y);
        Assert.Equal(velocityX, graph.VelocityX);
        Assert.Equal(velocityY, graph.VelocityY);
        Assert.False(graph.IsRefreshTravelFlashActive);
        Assert.Equal(replacement.PathData, graph.PathData);
    }

    [Fact]
    public async Task FinanceNewsService_ReadsDistinctRssHeadlines()
    {
        const string rss = "<rss><channel><item><title>Markets rally</title></item><item><title>Markets rally</title></item><item><title>Rates move</title></item></channel></rss>";
        using FinanceNewsService service = new(new StaticResponseHandler(rss));

        IReadOnlyList<string> headlines = await service.GetHeadlinesAsync("https://example.test/feed", CancellationToken.None);

        Assert.Equal(["Markets rally", "Rates move"], headlines);
    }

    [Fact]
    public async Task FinanceNewsService_ReportsStaleButSyntacticallyValidRssContent()
    {
        const string rss = "<rss><channel><item><title>Older market news</title><pubDate>Mon, 03 Aug 2026 10:00:00 GMT</pubDate></item></channel></rss>";
        using FinanceNewsService service = new(
            new StaticResponseHandler(rss),
            () => new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        IReadOnlyList<string> headlines = await service.GetPlaybackHeadlinesAsync(
            new AppSettings { NewsFeedUrl = "https://example.test/feed" },
            CancellationToken.None);

        Assert.Equal(RssFeedFreshnessState.Stale, service.LastRssFreshnessState);
        Assert.Equal(new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero), service.LatestRssPublicationUtc);
        Assert.Single(headlines);
        Assert.Contains("stale", headlines[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinanceNewsService_ConvertsExplicitPublicationOffsetToUtc()
    {
        const string rss = "<rss><channel><item><title>Offset market news</title><pubDate>Sat, 29 Aug 2026 10:00:00 +0200</pubDate></item></channel></rss>";
        using FinanceNewsService service = new(
            new StaticResponseHandler(rss),
            () => new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        IReadOnlyList<string> headlines = await service.GetPlaybackHeadlinesAsync(
            new AppSettings { NewsFeedUrl = "https://example.test/feed" },
            CancellationToken.None);

        Assert.Equal(RssFeedFreshnessState.Fresh, service.LastRssFreshnessState);
        Assert.Equal(new DateTimeOffset(2026, 8, 29, 8, 0, 0, TimeSpan.Zero), service.LatestRssPublicationUtc);
        Assert.Equal(["Offset market news"], headlines);
    }

    [Fact]
    public async Task FinanceNewsService_ReportsMissingPublicationDatesWhileRetainingLegacyPlayback()
    {
        const string rss = "<rss><channel><item><title>Undated market news</title></item></channel></rss>";
        using FinanceNewsService service = new(new StaticResponseHandler(rss));

        IReadOnlyList<string> headlines = await service.GetPlaybackHeadlinesAsync(
            new AppSettings { NewsFeedUrl = "https://example.test/feed" },
            CancellationToken.None);

        Assert.Equal(RssFeedFreshnessState.MissingPublicationDate, service.LastRssFreshnessState);
        Assert.Equal(["Undated market news"], headlines);
    }

    [Fact]
    public async Task FinanceNewsService_RejectsFuturePublicationDatesAsCurrentNews()
    {
        const string rss = "<rss><channel><item><title>Future market news</title><pubDate>Mon, 31 Aug 2026 10:00:00 GMT</pubDate></item></channel></rss>";
        using FinanceNewsService service = new(
            new StaticResponseHandler(rss),
            () => new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        IReadOnlyList<string> headlines = await service.GetPlaybackHeadlinesAsync(
            new AppSettings { NewsFeedUrl = "https://example.test/feed" },
            CancellationToken.None);

        Assert.Equal(RssFeedFreshnessState.FuturePublicationDate, service.LastRssFreshnessState);
        Assert.Null(service.LatestRssPublicationUtc);
        Assert.Contains("future", headlines[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FinanceNewsService_IgnoresFutureOutlierWhenCurrentPublicationDateExists()
    {
        const string rss = "<rss><channel><item><title>Current market news</title><pubDate>Fri, 28 Aug 2026 10:00:00 GMT</pubDate></item><item><title>Future market news</title><pubDate>Mon, 31 Aug 2026 10:00:00 GMT</pubDate></item></channel></rss>";
        using FinanceNewsService service = new(
            new StaticResponseHandler(rss),
            () => new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        IReadOnlyList<string> headlines = await service.GetPlaybackHeadlinesAsync(
            new AppSettings { NewsFeedUrl = "https://example.test/feed" },
            CancellationToken.None);

        Assert.Equal(RssFeedFreshnessState.Fresh, service.LastRssFreshnessState);
        Assert.Equal(new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero), service.LatestRssPublicationUtc);
        Assert.Equal(["Current market news"], headlines);
    }

    [Fact]
    public async Task FinanceNewsService_RejectsNonHttpFeedUrls()
    {
        using FinanceNewsService service = new(new StaticResponseHandler("<rss />"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetHeadlinesAsync("file:///tmp/private.xml", CancellationToken.None));
    }

    [Fact]
    public async Task FinanceNewsService_UsesConfiguredAiStyleForSummaries()
    {
        const string rss = "<rss><channel><item><title>Markets rally</title></item></channel></rss>";
        SummaryResponseHandler handler = new(rss, "A rally, briefly illuminated.");
        using FinanceNewsService service = new(handler);
        AppSettings settings = new()
        {
            NewsFeedUrl = "https://example.test/feed",
            NewsScrollerMode = NewsScrollerMode.SummarizedFinancialNews,
            AiApiKey = "test-key",
            AiEndpointUrl = "https://example.test/v1",
            AiModelId = "test-model",
            AiWritingStyle = AiWritingStyle.WilliamShakespeare
        };

        string text = await service.GetNewsTextAsync(settings, CancellationToken.None);

        Assert.Equal("A rally, briefly illuminated.", text);
        Assert.Contains("William Shakespeare", handler.RequestBody, StringComparison.Ordinal);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
    }

    [Fact]
    public async Task FinanceNewsService_FallsBackToRssWhenSummaryFails()
    {
        const string rss = "<rss><channel><item><title>Markets rally</title></item></channel></rss>";
        using FinanceNewsService service = new(new SummaryResponseHandler(rss, null));
        AppSettings settings = new()
        {
            NewsFeedUrl = "https://example.test/feed",
            NewsScrollerMode = NewsScrollerMode.SummarizedFinancialNews,
            AiApiKey = "test-key",
            AiEndpointUrl = "https://example.test/v1",
            AiModelId = "test-model"
        };

        string text = await service.GetNewsTextAsync(settings, CancellationToken.None);

        Assert.Equal("Markets rally", text);
    }

    [Theory]
    [InlineData(0, true, "SUN")]
    [InlineData(0, false, "CLR")]
    [InlineData(63, true, "RAIN")]
    [InlineData(95, true, "STORM")]
    public void WorldWeatherService_MapsWeatherCodes(int code, bool isDay, string expected)
        => Assert.Equal(expected, WorldWeatherService.GetGlyph(code, isDay));

    private sealed class StaticResponseHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/rss+xml")
            });
    }

    private sealed class SummaryResponseHandler(string rss, string? summary) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        public string? AuthorizationScheme { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(rss, Encoding.UTF8, "application/rss+xml")
                };
            }

            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            if (summary is null)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            string json = JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content = summary } } }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class TemporaryDirectoryScope : IDisposable
    {
        public TemporaryDirectoryScope()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dnppv2-ambient-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
