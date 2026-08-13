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
using System.Net.Http;
using System.Text;
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Data.Runtime;
using DoNotPanicPortfolioVisualizer.Data.Services;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class SettingsPersistenceAndValidationTests
{
    [Fact]
    public void SettingsProtectionService_RoundTripsPlaintext()
    {
        using TemporaryDirectoryScope directory = new();
        SettingsProtectionService service = new(Path.Combine(directory.Path, "settings-protection.key"));

        string protectedValue = service.Protect("super-secret");
        string roundTripped = service.Unprotect(protectedValue);

        Assert.NotEqual("super-secret", protectedValue);
        Assert.Equal("super-secret", roundTripped);
    }

    [Fact]
    public void SettingsProtectionService_ThrowsWhenExistingKeyFileIsCorrupted()
    {
        using TemporaryDirectoryScope directory = new();
        string keyPath = Path.Combine(directory.Path, "settings-protection.key");
        File.WriteAllText(keyPath, "not-base64");
        SettingsProtectionService service = new(keyPath);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => service.Protect("super-secret"));

        Assert.Contains("corrupted", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProviderSecretStoreService_SavesProtectedSecretAndOverlaysItBack()
    {
        using TemporaryDirectoryScope directory = new();
        SettingsProtectionService protection = new(Path.Combine(directory.Path, "settings-protection.key"));
        ProviderSecretStoreService secretStore = new(
            protection,
            Path.Combine(directory.Path, "provider-secrets.json"));
        AppSettings settings = Defaults.CreateSettings();
        settings.AiApiKey = "top-secret";

        secretStore.Save(settings);

        string storedJson = File.ReadAllText(secretStore.SecretsPath);
        Assert.DoesNotContain("top-secret", storedJson, StringComparison.Ordinal);

        AppSettings reloaded = Defaults.CreateSettings();
        secretStore.OverlaySecrets(reloaded);
        Assert.Equal("top-secret", reloaded.AiApiKey);
    }

    [Fact]
    public void SettingsFileService_SaveSanitizesPersistedSecretsAndLoadOverlaysThem()
    {
        using TemporaryDirectoryScope directory = new();
        SettingsProtectionService protection = new(Path.Combine(directory.Path, "settings-protection.key"));
        ProviderSecretStoreService secretStore = new(
            protection,
            Path.Combine(directory.Path, "provider-secrets.json"));
        SettingsFileService fileService = new(
            Path.Combine(directory.Path, "settings.json"),
            secretStore);
        AppSettings settings = Defaults.CreateSettings();
        settings.AiApiKey = "live-key";
        settings.AiEndpointUrl = "https://openrouter.ai/api/v1/chat/completions";
        settings.AiModelId = "openrouter/free";
        settings.NewsFeedUrl = "https://example.com/rss.xml?edition=en&token=secret-token";

        fileService.Save(settings);
        string persistedJson = File.ReadAllText(fileService.SettingsPath);
        AppSettings reloaded = fileService.Load();

        Assert.DoesNotContain("live-key", persistedJson, StringComparison.Ordinal);
        Assert.Contains("\"AiApiKey\": \"\"", persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", persistedJson, StringComparison.Ordinal);
        Assert.Contains("edition=en", persistedJson, StringComparison.Ordinal);
        Assert.Equal("live-key", reloaded.AiApiKey);
        Assert.Equal("https://openrouter.ai/api/v1", reloaded.AiEndpointUrl);
    }

    [Fact]
    public void SettingsFileService_LoadMigratesLegacyDeepSeekFields()
    {
        using TemporaryDirectoryScope directory = new();
        SettingsFileService fileService = new(Path.Combine(directory.Path, "settings.json"));
        File.WriteAllText(
            fileService.SettingsPath,
            """
            {
              "DeepSeekApiKey": "legacy-key",
              "DeepSeekEndpointUrl": "https://api.deepseek.com/chat/completions",
              "DeepSeekModelId": "deepseek-v4-flash",
              "DeepSeekWritingStyle": "DouglasAdams"
            }
            """);

        AppSettings loaded = fileService.Load();

        Assert.Equal("legacy-key", loaded.AiApiKey);
        Assert.Equal(Defaults.DefaultAiEndpointUrl, loaded.AiEndpointUrl);
        Assert.Equal(Defaults.DefaultAiModelId, loaded.AiModelId);
        Assert.Equal(AiWritingStyle.DouglasAdams, loaded.AiWritingStyle);
    }

    [Fact]
    public void SettingsFileService_LoadFallsBackToDefaultsWhenJsonIsInvalid()
    {
        using TemporaryDirectoryScope directory = new();
        SettingsFileService fileService = new(Path.Combine(directory.Path, "settings.json"));
        File.WriteAllText(fileService.SettingsPath, "{ invalid json");

        AppSettings loaded = fileService.Load();

        Assert.Equal(Defaults.DefaultAiEndpointUrl, loaded.AiEndpointUrl);
        Assert.Equal(Defaults.DefaultNewsFeedUrl, loaded.NewsFeedUrl);
    }

    [Fact]
    public void SettingsFileService_LoadUsesLegacyWritingStyleWhenNewValueIsInvalid()
    {
        using TemporaryDirectoryScope directory = new();
        SettingsFileService fileService = new(Path.Combine(directory.Path, "settings.json"));
        File.WriteAllText(
            fileService.SettingsPath,
            """
            {
              "AiWritingStyle": 999,
              "DeepSeekWritingStyle": "WilliamShakespeare"
            }
            """);

        AppSettings loaded = fileService.Load();

        Assert.Equal(AiWritingStyle.WilliamShakespeare, loaded.AiWritingStyle);
    }

    [Fact]
    public async Task NewsFeedValidationService_ResetsInvalidUrlToDefault()
    {
        NewsFeedValidationService service = new();

        NewsFeedValidationResult result = await service.ValidateAsync("not-a-url", 10, true);

        Assert.False(result.IsValid);
        Assert.True(result.WasResetToDefault);
        Assert.Equal(Defaults.DefaultNewsFeedUrl, result.ResolvedFeedUrl);
    }

    [Fact]
    public async Task NewsFeedValidationService_SkipsWhenOffline()
    {
        NewsFeedValidationService service = new();

        NewsFeedValidationResult result = await service.ValidateAsync("https://example.com/rss.xml", 10, false);

        Assert.True(result.IsValid);
        Assert.True(result.ValidationSkipped);
        Assert.Equal("https://example.com/rss.xml", result.ResolvedFeedUrl);
    }

    [Fact]
    public async Task NewsFeedValidationService_AcceptsReadableRss()
    {
        NewsFeedValidationService service = new(_ => CreateHttpClient(
            """
            <rss><channel><item><title>Headline</title></item></channel></rss>
            """));

        NewsFeedValidationResult result = await service.ValidateAsync("https://example.com/rss.xml", 10, true);

        Assert.True(result.IsValid);
        Assert.False(result.WasResetToDefault);
        Assert.Equal("https://example.com/rss.xml", result.ResolvedFeedUrl);
    }

    [Fact]
    public async Task NewsFeedValidationService_ResetsNonXmlResponseToDefault()
    {
        NewsFeedValidationService service = new(_ => CreateHttpClient("not xml at all"));

        NewsFeedValidationResult result = await service.ValidateAsync("https://example.com/rss.xml", 10, true);

        Assert.False(result.IsValid);
        Assert.True(result.WasResetToDefault);
    }

    [Fact]
    public async Task NewsFeedValidationService_RejectsAtomFeedWithoutEntryTitle()
    {
        NewsFeedValidationService service = new(_ => CreateHttpClient(
            """
            <feed xmlns="http://www.w3.org/2005/Atom">
              <title>Channel Title</title>
              <entry><summary>No title here</summary></entry>
            </feed>
            """));

        NewsFeedValidationResult result = await service.ValidateAsync("https://example.com/feed.atom", 10, true);

        Assert.False(result.IsValid);
        Assert.True(result.WasResetToDefault);
    }

    [Fact]
    public async Task YahooSymbolValidationService_ValidatesAliasesAndMarksMissingSymbols()
    {
        FakeYFinanceRuntimeClient runtimeClient = new()
        {
            QuotesAsync = (symbols, _) =>
            {
                Assert.Equal(["^TNX", "AAPL", "MISSING"], symbols);
                return Task.FromResult(
                    new YFinanceQuotesResponse(
                    [
                        new("^TNX", 42.3m, 41.8m, 0.5m, null, "USD", "America/New_York", "REGULAR", new YFinanceCacheMetadata(false)),
                        new("AAPL", 210m, 208m, 2m, 0.96m, "USD", "America/New_York", "REGULAR", new YFinanceCacheMetadata(false))
                    ]));
            }
        };
        YahooSymbolValidationService service = new(runtimeClient);

        YahooSymbolValidationResult result = await service.ValidateAsync(["US10Y", "AAPL", "MISSING"], 10);

        Assert.True(result.Entries["US10Y"].IsValid);
        Assert.True(result.Entries["AAPL"].IsValid);
        Assert.Contains("MISSING", result.InvalidSymbols);
        Assert.Equal(4.23m, result.ValidatedQuotes["US10Y"].Last);
    }

    [Fact]
    public async Task YahooSymbolValidationService_ResolvesKnownRequestAliases()
    {
        FakeYFinanceRuntimeClient runtimeClient = new()
        {
            QuotesAsync = (symbols, _) =>
            {
                Assert.Equal(["BRK-B"], symbols);
                return Task.FromResult(
                    new YFinanceQuotesResponse(
                    [
                        new("BRK-B", 400m, 395m, 5m, 1.26m, "USD", "America/New_York", "REGULAR", new YFinanceCacheMetadata(false))
                    ]));
            }
        };
        YahooSymbolValidationService service = new(runtimeClient);

        YahooSymbolValidationResult result = await service.ValidateAsync(["BRK.B"], 10);

        Assert.True(result.Entries["BRK.B"].IsValid);
        Assert.Equal(400m, result.ValidatedQuotes["BRK.B"].Last);
    }

    [Fact]
    public async Task YahooSymbolValidationService_MarksRateLimitedBatchesAsDeferred()
    {
        int callCount = 0;
        FakeYFinanceRuntimeClient runtimeClient = new()
        {
            QuotesAsync = (_, _) =>
            {
                callCount++;
                throw new HttpRequestException("429 too many requests", null, HttpStatusCode.TooManyRequests);
            }
        };
        YahooSymbolValidationService service = new(runtimeClient);

        List<string> symbols = Enumerable.Range(0, 30).Select(index => $"SYM{index}").ToList();
        YahooSymbolValidationResult result = await service.ValidateAsync(symbols, 10);

        Assert.True(result.WasRateLimited);
        Assert.Equal(1, callCount);
        Assert.Equal(symbols.OrderBy(symbol => symbol, StringComparer.OrdinalIgnoreCase), result.DeferredSymbols);
    }

    private static HttpClient CreateHttpClient(string content)
        => new(new FixedResponseHandler(content)) { Timeout = TimeSpan.FromSeconds(5) };

    private sealed class FixedResponseHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/xml")
            });
    }

    private sealed class TemporaryDirectoryScope : IDisposable
    {
        public TemporaryDirectoryScope()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "dnppv2-settings-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
