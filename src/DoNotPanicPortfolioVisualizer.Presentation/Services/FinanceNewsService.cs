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
using System.Xml.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;

namespace DoNotPanicPortfolioVisualizer.Presentation.Services;

public sealed class FinanceNewsService : IDisposable
{
    private readonly HttpClient _client;

    public FinanceNewsService(HttpMessageHandler? handler = null)
    {
        _client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: true);
        _client.Timeout = TimeSpan.FromSeconds(15);
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("DNPPV-2.0/2.0");
    }

    public async Task<IReadOnlyList<string>> GetHeadlinesAsync(string feedUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out Uri? feedUri) ||
            (feedUri.Scheme != Uri.UriSchemeHttp && feedUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The news feed must be an absolute HTTP or HTTPS URL.", nameof(feedUrl));
        }

        using Stream stream = await _client.GetStreamAsync(feedUri, cancellationToken).ConfigureAwait(false);
        XDocument document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken).ConfigureAwait(false);
        return document.Descendants()
            .Where(static element => element.Name.LocalName == "item")
            .Select(static item => item.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Value)
            .Where(static title => !string.IsNullOrWhiteSpace(title))
            .Select(static title => title!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();
    }

    public async Task<string> GetNewsTextAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        IReadOnlyList<string> headlines = await GetHeadlinesAsync(settings.NewsFeedUrl, cancellationToken)
            .ConfigureAwait(false);
        string rssText = headlines.Count == 0
            ? "France 24 business feed returned no headlines"
            : string.Join("     |     ", headlines);

        if (settings.NewsScrollerMode != NewsScrollerMode.SummarizedFinancialNews ||
            string.IsNullOrWhiteSpace(settings.AiApiKey) ||
            string.IsNullOrWhiteSpace(settings.AiModelId))
        {
            return rssText;
        }

        try
        {
            return await SummarizeAsync(settings, headlines, cancellationToken).ConfigureAwait(false) ?? rssText;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return rssText;
        }
    }

    private async Task<string?> SummarizeAsync(
        AppSettings settings,
        IReadOnlyList<string> headlines,
        CancellationToken cancellationToken)
    {
        if (headlines.Count == 0 ||
            !Uri.TryCreate(settings.AiEndpointUrl, UriKind.Absolute, out Uri? endpoint) ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        Uri requestUri = endpoint.AbsolutePath.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? endpoint
            : new Uri($"{endpoint.AbsoluteUri.TrimEnd('/')}/chat/completions", UriKind.Absolute);
        string style = settings.AiWritingStyle == AiWritingStyle.WilliamShakespeare
            ? "in a concise William Shakespeare-inspired style"
            : "in a concise Douglas Adams-inspired style";
        object payload = new
        {
            model = settings.AiModelId,
            messages = new object[]
            {
                new { role = "system", content = $"Summarize financial news {style}. Preserve factual meaning. Return one short ticker-ready paragraph." },
                new { role = "user", content = string.Join("\n", headlines.Take(12)) }
            }
        };

        using HttpRequestMessage request = new(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AiApiKey);
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using JsonDocument document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        string? summary = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        return string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
    }

    public void Dispose() => _client.Dispose();
}
