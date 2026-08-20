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

    public void Dispose() => _client.Dispose();
}
