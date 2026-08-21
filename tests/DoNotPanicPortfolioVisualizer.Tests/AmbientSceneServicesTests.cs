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
        Assert.Equal("100.00", graph.LastText);
        Assert.Equal("-1.25%", graph.ChangeText);
        Assert.Equal("#FF5A36", graph.AccentBrush);
    }

    [Fact]
    public void HistoricalGraphBuilder_KeepsSixteenthCardInsideGraphStage()
    {
        TickerHistorySnapshot snapshot = new()
        {
            Symbol = "VWO",
            Points = [new HistoricalPricePoint { TimestampUtc = DateTimeOffset.UtcNow, Close = 42m }]
        };

        var graph = new HistoricalGraphBuilder().Build("Satellite", snapshot, 0.5m, 15);

        Assert.InRange(graph.AnchorX, 0, 700);
        Assert.InRange(graph.AnchorY, 0, 138);
        Assert.InRange(graph.AnchorX + 216 + 3, 0, 920);
        Assert.InRange(graph.AnchorY + 42 + 1, 0, 184);
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
