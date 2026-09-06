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
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Services;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

public interface IAiNewsAccessValidationService
{
    Task<AiNewsAccessValidationResult> ValidateAsync(
        AppSettings settings,
        bool networkAvailable,
        CancellationToken cancellationToken = default);
}

public sealed class AiNewsAccessValidationService : IAiNewsAccessValidationService
{
    private readonly Func<TimeSpan, HttpClient> _httpClientFactory;

    public AiNewsAccessValidationService(Func<TimeSpan, HttpClient>? httpClientFactory = null)
        => _httpClientFactory = httpClientFactory ?? (timeout => new HttpClient { Timeout = timeout });

    public async Task<AiNewsAccessValidationResult> ValidateAsync(
        AppSettings settings,
        bool networkAvailable,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        if (settings.NewsScrollerMode != NewsScrollerMode.SummarizedFinancialNews)
            return AiNewsAccessValidationResult.Skipped("AI summarized news is not selected.");

        string apiKey = (settings.AiApiKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            return AiNewsAccessValidationResult.Failed("Enter an AI API key, or switch Finance News to RSS Feed.");

        if (!TryNormalizeEndpoint(settings.AiEndpointUrl, out string endpointUrl))
            return AiNewsAccessValidationResult.Failed("Enter a valid AI endpoint URL.");

        string modelId = string.IsNullOrWhiteSpace(settings.AiModelId) ? Defaults.DefaultAiModelId : settings.AiModelId.Trim();
        if (string.IsNullOrWhiteSpace(modelId))
            return AiNewsAccessValidationResult.Failed("Enter a valid AI model ID.");
        if (!networkAvailable)
            return AiNewsAccessValidationResult.Failed("Connect to the internet before validating AI summarized financial news.");

        string operationId = Guid.NewGuid().ToString("N");
        string effectiveModelId = modelId;
        using HttpClient client = _httpClientFactory(TimeSpan.FromSeconds(Math.Max(3, settings.HttpTimeoutSeconds)));
        try
        {
            OpenRouterResolvedModel resolved = await OpenRouterModelResolver.ResolveAsync(
                client, endpointUrl, modelId, cancellationToken,
                (eventName, detail) => Trace(eventName, operationId, endpointUrl, detail, "model-resolution"));
            effectiveModelId = resolved.ModelId;

            using HttpRequestMessage request = new(HttpMethod.Post, BuildChatCompletionsUri(endpointUrl));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            OpenRouterModelResolver.AddAttributionHeaders(request, endpointUrl);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = effectiveModelId,
                messages = new[] { new { role = "user", content = "Reply with OK only." } },
                max_tokens = 8,
                temperature = 0
            }), Encoding.UTF8, "application/json");

            Trace("AiAccessValidationStart", operationId, endpointUrl, effectiveModelId, "start");
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.NotFound)
            {
                string reason = response.StatusCode == HttpStatusCode.NotFound ? "http-404" : "http-429";
                Trace("AiAccessValidationRateLimited", operationId, endpointUrl, effectiveModelId, reason);
                return AiNewsAccessValidationResult.Skipped("AI provider temporarily rate-limited or did not expose the requested route. Settings were not rejected; the app will retry summarized news at runtime.");
            }
            if (!response.IsSuccessStatusCode)
            {
                Trace("AiAccessValidationFailed", operationId, endpointUrl, effectiveModelId, $"http-{(int)response.StatusCode}");
                return AiNewsAccessValidationResult.Failed($"AI access was rejected by the provider ({(int)response.StatusCode}). Check the API key, endpoint URL, and model ID.");
            }

            Trace("AiAccessValidationSucceeded", operationId, endpointUrl, effectiveModelId, "success");
            return AiNewsAccessValidationResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Trace("AiAccessValidationCancelled", operationId, endpointUrl, effectiveModelId, "cancelled");
            throw;
        }
        catch (TaskCanceledException)
        {
            Trace("AiAccessValidationFailed", operationId, endpointUrl, effectiveModelId, "timeout");
            return AiNewsAccessValidationResult.Failed("AI access validation timed out. Check the endpoint/model or switch Finance News to RSS Feed.");
        }
        catch (Exception exception)
        {
            Trace("AiAccessValidationFailed", operationId, endpointUrl, effectiveModelId, exception.GetType().Name);
            return AiNewsAccessValidationResult.Failed("AI access validation failed. Check the API key, endpoint URL, and model ID.");
        }
    }

    private static bool TryNormalizeEndpoint(string? value, out string endpointUrl)
    {
        string candidate = string.IsNullOrWhiteSpace(value) ? Defaults.DefaultAiEndpointUrl : value.Trim();
        if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            endpointUrl = candidate.TrimEnd('/');
            return true;
        }

        endpointUrl = string.Empty;
        return false;
    }

    private static Uri BuildChatCompletionsUri(string endpointUrl)
        => new(endpointUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? endpointUrl
            : endpointUrl + "/chat/completions");

    private static void Trace(string eventName, string operationId, string endpointUrl, string modelId, string detail)
    {
        Uri.TryCreate(endpointUrl, UriKind.Absolute, out Uri? uri);
        TraceLog.InfoState("Config.Validation", eventName,
        [
            new("operation_id", operationId),
            new("endpoint", uri is null ? "invalid" : $"{uri.Scheme}://{uri.Host}"),
            new("model_id", modelId),
            new("detail", detail)
        ]);
    }
}

public sealed class AiNewsAccessValidationResult
{
    public bool IsValid { get; init; }
    public bool ValidationSkipped { get; init; }
    public string Message { get; init; } = string.Empty;

    public static AiNewsAccessValidationResult Success() => new() { IsValid = true };
    public static AiNewsAccessValidationResult Skipped(string message) => new() { IsValid = true, ValidationSkipped = true, Message = message };
    public static AiNewsAccessValidationResult Failed(string message) => new() { IsValid = false, Message = message };
}
