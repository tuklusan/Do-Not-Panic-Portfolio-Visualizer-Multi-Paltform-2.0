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
using System.Collections.Concurrent;
using System.Text.Json;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using DoNotPanicPortfolioVisualizer.Data.Interfaces;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

public sealed class ProviderSecretStoreService
{
    internal const string OpenRouterApiKeyEnvironmentVariable = "DNPPV_OPENROUTER_API_KEY";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private static readonly ConcurrentDictionary<string, object> SyncRoots = new(StringComparer.OrdinalIgnoreCase);

    private readonly ISettingsProtectionService _settingsProtectionService;
    private readonly object _sync;

    public ProviderSecretStoreService(
        ISettingsProtectionService? settingsProtectionService = null,
        string? secretsPath = null)
    {
        _settingsProtectionService = settingsProtectionService ?? new SettingsProtectionService();
        SecretsPath = StorageOverridePathValidator.ResolveFilePath(
            secretsPath,
            Path.Combine(LocalDataRootResolver.ResolveForCurrentPlatform().SecretRoot, "provider-secrets.json"));
        _sync = SyncRoots.GetOrAdd(SecretsPath, static _ => new object());
    }

    public string SecretsPath { get; }

    public void OverlaySecrets(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            ProviderSecretsDto dto = LoadSecretsDto();
            ApplySecret(settings, dto.AiApiKey, static s => s.AiApiKey, static (s, v) => s.AiApiKey = v);
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                string? processKey = Environment.GetEnvironmentVariable(OpenRouterApiKeyEnvironmentVariable)
                    ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
                    ?? Environment.GetEnvironmentVariable("OPENROUTER_AI_API_KEY");
                if (!string.IsNullOrWhiteSpace(processKey))
                    settings.AiApiKey = processKey.Trim();
            }

            if (string.Equals(Environment.GetEnvironmentVariable("DNPPV_SOAK_REQUIRE_AI_NEWS"), "1", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(settings.AiApiKey))
            {
                settings.NewsScrollerMode = NewsScrollerMode.SummarizedFinancialNews;
            }
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            ProviderSecretsDto dto = LoadSecretsDto();
            dto.AiApiKey = ResolvePersistedProtectedValue(settings.AiApiKey, dto.AiApiKey);

            if (!dto.HasAnySecrets())
            {
                if (File.Exists(SecretsPath))
                    File.Delete(SecretsPath);

                return;
            }

            string? directory = Path.GetDirectoryName(SecretsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string json = JsonSerializer.Serialize(dto, JsonOptions);
            File.WriteAllText(SecretsPath, json);
        }
    }

    private void ApplySecret(
        AppSettings settings,
        string protectedValue,
        Func<AppSettings, string> getter,
        Action<AppSettings, string> setter)
    {
        if (!string.IsNullOrWhiteSpace(getter(settings)))
            return;

        string unprotected = UnprotectSafe(protectedValue);
        if (!string.IsNullOrWhiteSpace(unprotected))
            setter(settings, unprotected);
    }

    private string ResolvePersistedProtectedValue(string currentValue, string persistedProtectedValue)
    {
        string trimmed = (currentValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        if (string.Equals(UnprotectSafe(persistedProtectedValue), trimmed, StringComparison.Ordinal))
            return persistedProtectedValue;

        return _settingsProtectionService.Protect(trimmed);
    }

    private ProviderSecretsDto LoadSecretsDto()
    {
        if (!File.Exists(SecretsPath))
            return new ProviderSecretsDto();

        try
        {
            string json = File.ReadAllText(SecretsPath);
            ProviderSecretsDto dto = JsonSerializer.Deserialize<ProviderSecretsDto>(json, JsonOptions) ?? new ProviderSecretsDto();
            MigrateLegacySerializedAiSecrets(dto, json);
            return dto;
        }
        catch
        {
            return new ProviderSecretsDto();
        }
    }

    private static void MigrateLegacySerializedAiSecrets(ProviderSecretsDto dto, string json)
    {
        if (!string.IsNullOrWhiteSpace(dto.AiApiKey) || string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("DeepSeekApiKey", out JsonElement element) &&
                element.ValueKind == JsonValueKind.String)
            {
                dto.AiApiKey = element.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
        }
    }

    private string UnprotectSafe(string protectedValue)
    {
        if (string.IsNullOrWhiteSpace(protectedValue))
            return string.Empty;

        try
        {
            return _settingsProtectionService.Unprotect(protectedValue);
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class ProviderSecretsDto
    {
        public string AiApiKey { get; set; } = string.Empty;

        public bool HasAnySecrets() => !string.IsNullOrWhiteSpace(AiApiKey);
    }
}
