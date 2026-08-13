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
using System.Xml;
using System.Net;
using DoNotPanicPortfolioVisualizer.Core.Constants;
using System.Net.Sockets;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

public sealed class NewsFeedValidationService
{
    private const int MaxFeedBytes = 1024 * 1024;
    private static readonly HttpClient SharedHttpClient = new();
    private readonly Func<TimeSpan, HttpClient> _httpClientFactory;

    public NewsFeedValidationService(Func<TimeSpan, HttpClient>? httpClientFactory = null)
    {
        _httpClientFactory = httpClientFactory ?? CreateDefaultHttpClient;
    }

    public async Task<NewsFeedValidationResult> ValidateAsync(
        string? feedUrl,
        int timeoutSeconds,
        bool networkAvailable,
        CancellationToken cancellationToken = default)
    {
        string candidate = (feedUrl ?? string.Empty).Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return NewsFeedValidationResult.ResetToDefault(
                "The RSS feed URL was not a valid http or https address, so it was reset to the default finance feed.");
        }

        if (!networkAvailable)
        {
            return new NewsFeedValidationResult
            {
                IsValid = true,
                ValidationSkipped = true,
                ResolvedFeedUrl = uri.ToString(),
                Message = "RSS feed validation was skipped because no network connection was detected."
            };
        }

        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, timeoutSeconds)));
        CancellationToken requestCancellationToken = timeoutSource.Token;

        if (await ResolvesToPrivateNetworkAsync(uri, requestCancellationToken).ConfigureAwait(false))
        {
            return NewsFeedValidationResult.ResetToDefault(
                "The RSS feed URL resolved to a local or private network target, so it was reset to the default finance feed.");
        }

        HttpClient client = _httpClientFactory(TimeSpan.FromSeconds(Math.Max(3, timeoutSeconds)));
        try
        {
            using HttpResponseMessage response = await client.GetAsync(uri, requestCancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string xml = await ReadContentWithLimitAsync(response.Content, MaxFeedBytes, requestCancellationToken).ConfigureAwait(false);

            using StringReader stringReader = new(xml);
            using XmlReader xmlReader = XmlReader.Create(
                stringReader,
                new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    MaxCharactersInDocument = MaxFeedBytes,
                    MaxCharactersFromEntities = 1024,
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    XmlResolver = null
                });
            XDocument document = XDocument.Load(xmlReader, LoadOptions.PreserveWhitespace);
            EnsureReasonableDepth(document.Root, maxDepth: 32);
            bool hasItemTitles = document.Descendants("item")
                .Elements("title")
                .Select(element => (element.Value ?? string.Empty).Trim())
                .Any(title => !string.IsNullOrWhiteSpace(title));
            bool hasAtomTitles = document.Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "entry", StringComparison.OrdinalIgnoreCase))
                .Any(entry => entry.Elements().Any(element =>
                    string.Equals(element.Name.LocalName, "title", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace((element.Value ?? string.Empty).Trim())));

            if (!hasItemTitles && !hasAtomTitles)
            {
                return NewsFeedValidationResult.ResetToDefault(
                    "The RSS feed did not contain any readable headlines, so it was reset to the default finance feed.");
            }

            return new NewsFeedValidationResult
            {
                IsValid = true,
                ResolvedFeedUrl = uri.ToString()
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return NewsFeedValidationResult.ResetToDefault(
                "The RSS feed could not be read as a valid news feed, so it was reset to the default finance feed.");
        }
        finally
        {
            if (!ReferenceEquals(client, SharedHttpClient))
                client.Dispose();
        }
    }

    private static HttpClient CreateDefaultHttpClient(TimeSpan timeout)
    {
        _ = timeout;
        return SharedHttpClient;
    }

    private static async Task<string> ReadContentWithLimitAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[8192];
        int totalBytes = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalBytes += read;
            if (totalBytes > maxBytes)
                throw new InvalidOperationException("The RSS feed exceeded the allowed validation size.");

            buffer.Write(chunk, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void EnsureReasonableDepth(XElement? root, int maxDepth)
    {
        if (root is null)
            throw new InvalidOperationException("The RSS feed did not contain a document root.");

        if (ComputeDepth(root) > maxDepth)
            throw new InvalidOperationException("The RSS feed exceeded the allowed nesting depth.");
    }

    private static int ComputeDepth(XElement element)
    {
        if (!element.Elements().Any())
            return 1;

        return 1 + element.Elements().Max(ComputeDepth);
    }

    private static async Task<bool> ResolvesToPrivateNetworkAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.IsLoopback)
            return true;

        if (IPAddress.TryParse(uri.Host, out IPAddress? literalAddress))
            return IsPrivateOrLocalAddress(literalAddress);

        try
        {
            IPAddress[] resolvedAddresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken).ConfigureAwait(false);
            return resolvedAddresses.Any(IsPrivateOrLocalAddress);
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool IsPrivateOrLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                return true;

            byte[] bytes = address.GetAddressBytes();
            return bytes.Length > 0 && (bytes[0] & 0xFE) == 0xFC;
        }

        byte[] octets = address.GetAddressBytes();
        if (octets.Length != 4)
            return false;

        return octets[0] == 10 ||
               octets[0] == 127 ||
               (octets[0] == 169 && octets[1] == 254) ||
               (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31) ||
               (octets[0] == 192 && octets[1] == 168);
    }
}

public sealed class NewsFeedValidationResult
{
    public bool IsValid { get; init; }
    public bool ValidationSkipped { get; init; }
    public bool WasResetToDefault { get; init; }
    public string ResolvedFeedUrl { get; init; } = Defaults.DefaultNewsFeedUrl;
    public string Message { get; init; } = string.Empty;

    public static NewsFeedValidationResult ResetToDefault(string message)
        => new()
        {
            IsValid = false,
            WasResetToDefault = true,
            ResolvedFeedUrl = Defaults.DefaultNewsFeedUrl,
            Message = message
        };
}
