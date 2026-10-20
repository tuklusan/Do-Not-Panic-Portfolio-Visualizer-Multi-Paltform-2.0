using System.Net;
using System.Text;
using System.Text.Json;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Presentation.Services;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;
using Xunit;

namespace DoNotPanicPortfolioVisualizer.Tests.Services;

public sealed class FinanceNewsServiceTests
{
    [Fact]
    public async Task PromptAndReasoningContentAreNeverUsedAsTickerSummary()
    {
        RssPlaybackSnapshot rss = CreateRssSnapshot();
        AppSettings settings = CreateSettings();

        SequenceResponseHandler reasoningHandler = new([
            CreateResponse(content: string.Empty, reasoning: "ANALYZE THE REQUEST: TASK: SUMMARIZE FINANCIAL NEWS."),
            CreateResponse(content: string.Empty, reasoning: "ANALYZE THE REQUEST: TASK: SUMMARIZE FINANCIAL NEWS.")
        ]);
        using FinanceNewsService reasoningService = CreateService(reasoningHandler);
        Assert.Same(rss, await reasoningService.ApplyAiSummaryAsync(settings, rss, CancellationToken.None));
        Assert.Equal(2, reasoningHandler.RequestCount);

        SequenceResponseHandler echoedPromptHandler = new([
            CreateResponse(content: "1. **ANALYZE THE INPUT DATA:** TASK: SUMMARIZE FINANCIAL NEWS. STYLE: CONCISE DOUGLAS ADAMS-INSPIRED STYLE."),
            CreateResponse(content: "1. **ANALYZE THE INPUT DATA:** TASK: SUMMARIZE FINANCIAL NEWS. STYLE: CONCISE DOUGLAS ADAMS-INSPIRED STYLE.")
        ]);
        using FinanceNewsService echoedPromptService = CreateService(echoedPromptHandler);
        Assert.Same(rss, await echoedPromptService.ApplyAiSummaryAsync(settings, rss, CancellationToken.None));
        Assert.Equal(1, echoedPromptHandler.RequestCount);

        SequenceResponseHandler validHandler = new([CreateResponse(content: "Investors weighed fresh chip demand against rising costs.")]);
        using FinanceNewsService validService = CreateService(validHandler);
        RssPlaybackSnapshot validResponse = await validService.ApplyAiSummaryAsync(settings, rss, CancellationToken.None);
        Assert.Equal("Investors weighed fresh chip demand against rising costs.", validResponse.Headlines[0]);

        using JsonDocument request = JsonDocument.Parse(validHandler.RequestBodies[0]);
        string systemPrompt = request.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
        Assert.Contains("Output only the final ticker-ready paragraph", systemPrompt, StringComparison.Ordinal);
        Assert.Contains("do not include a plan, analysis, reasoning", systemPrompt, StringComparison.Ordinal);
        JsonElement reasoningConfig = request.RootElement.GetProperty("reasoning");
        Assert.Equal("none", reasoningConfig.GetProperty("effort").GetString());
        Assert.True(reasoningConfig.GetProperty("exclude").GetBoolean());
    }

    [Fact]
    public void MacroDialNeedleMovesOnItsOwnFrameStep()
    {
        MacroQuoteViewModel dial = new("VIX", "^VIX", -10m, 10m);
        dial.Apply(new QuoteSnapshot { ChangePercent = -5m });
        string initialNeedle = dial.NeedlePath;

        dial.Apply(new QuoteSnapshot { ChangePercent = 5m });
        Assert.Equal(initialNeedle, dial.NeedlePath);

        dial.Step(TimeSpan.FromMilliseconds(16));
        Assert.NotEqual(initialNeedle, dial.NeedlePath);
    }

    private static AppSettings CreateSettings() => new()
    {
        NewsScrollerMode = NewsScrollerMode.SummarizedFinancialNews,
        AiApiKey = "test-key",
        AiEndpointUrl = "https://openrouter.ai/api/v1",
        AiModelId = "mock-model"
    };

    private static RssPlaybackSnapshot CreateRssSnapshot() => new(
        ["Chipmakers report stronger demand as costs rise."],
        new RssFeedFreshnessSnapshot(RssFeedFreshnessState.Fresh, DateTimeOffset.UtcNow));

    private static FinanceNewsService CreateService(HttpMessageHandler handler)
        => new(handler, cachePath: Path.Combine(Path.GetTempPath(), $"dnppv-news-test-{Guid.NewGuid():N}.json"));

    private static string CreateResponse(string content, string? reasoning = null)
    {
        Dictionary<string, object?> message = new() { ["content"] = content };
        if (reasoning is not null)
            message["reasoning_content"] = reasoning;
        return JsonSerializer.Serialize(new { choices = new[] { new { message } } });
    }

    private sealed class SequenceResponseHandler(IEnumerable<string> responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);
        public List<string> RequestBodies { get; } = [];
        public int RequestCount => RequestBodies.Count;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            string response = _responses.Dequeue();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            };
        }
    }
}
