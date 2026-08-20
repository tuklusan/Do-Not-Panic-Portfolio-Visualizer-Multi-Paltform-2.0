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

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class AmbientSceneServicesTests
{
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
        Assert.InRange(graph.AnchorY, 0, 184);
        Assert.InRange(graph.AnchorX + 216 + 3, 0, 920);
        Assert.InRange(graph.AnchorY + 54 + 2, 0, 244);
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
