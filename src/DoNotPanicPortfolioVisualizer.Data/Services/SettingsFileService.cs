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
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Services;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

public sealed class SettingsFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly ConcurrentDictionary<string, object> SyncRoots = new(StringComparer.OrdinalIgnoreCase);

    private readonly ProviderSecretStoreService _providerSecretStoreService;
    private readonly object _sync;
    private readonly string _settingsPath;

    public SettingsFileService(
        string? settingsPath = null,
        ProviderSecretStoreService? providerSecretStoreService = null)
    {
        _settingsPath = StorageOverridePathValidator.ResolveFilePath(
            settingsPath,
            Path.Combine(LocalDataRootResolver.ResolveForCurrentPlatform().DataRoot, "settings.json"));
        _providerSecretStoreService = providerSecretStoreService ?? new ProviderSecretStoreService();
        _sync = SyncRoots.GetOrAdd(_settingsPath, static _ => new object());
    }

    public string SettingsPath => _settingsPath;

    public AppSettings Load()
    {
        lock (_sync)
        {
            AppSettings settings = Defaults.CreateSettings();
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                try
                {
                    settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? settings;
                }
                catch (JsonException)
                {
                    settings = Defaults.CreateSettings();
                }

                MigrateLegacySerializedAiSettings(settings, json);
            }

            _providerSecretStoreService.OverlaySecrets(settings);
            return AppSettingsNormalizer.Normalize(settings);
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            _providerSecretStoreService.Save(settings);

            AppSettings persisted = CreateSanitizedCopy(settings);
            string json = JsonSerializer.Serialize(
                persisted,
                new JsonSerializerOptions { WriteIndented = true });
            WriteAllTextAtomically(SettingsPath, json);
        }
    }

    private static AppSettings CreateSanitizedCopy(AppSettings settings)
    {
        AppSettings copy = JsonSerializer.Deserialize<AppSettings>(
            JsonSerializer.Serialize(settings),
            JsonOptions) ?? Defaults.CreateSettings();

        copy.AiApiKey = string.Empty;
        copy.AiEndpointUrl = SanitizePersistedUri(copy.AiEndpointUrl, Defaults.DefaultAiEndpointUrl, allowNonSensitiveQueryParameters: false);
        copy.NewsFeedUrl = SanitizePersistedUri(copy.NewsFeedUrl, Defaults.DefaultNewsFeedUrl, allowNonSensitiveQueryParameters: true);
        copy.AiModelId = LooksSensitiveValue(copy.AiModelId)
            ? Defaults.DefaultAiModelId
            : (copy.AiModelId ?? string.Empty).Trim();
        return copy;
    }

    private static void MigrateLegacySerializedAiSettings(AppSettings settings, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (string.IsNullOrWhiteSpace(settings.AiApiKey))
                settings.AiApiKey = GetString(root, "DeepSeekApiKey");
            if (string.IsNullOrWhiteSpace(settings.AiEndpointUrl) ||
                string.Equals(settings.AiEndpointUrl, Defaults.DefaultAiEndpointUrl, StringComparison.OrdinalIgnoreCase))
            {
                settings.AiEndpointUrl = GetString(root, "DeepSeekEndpointUrl", settings.AiEndpointUrl);
            }

            if (string.IsNullOrWhiteSpace(settings.AiModelId) ||
                string.Equals(settings.AiModelId, Defaults.DefaultAiModelId, StringComparison.OrdinalIgnoreCase))
            {
                settings.AiModelId = GetString(root, "DeepSeekModelId", settings.AiModelId);
            }

            if (root.TryGetProperty("DeepSeekWritingStyle", out JsonElement styleElement) &&
                ShouldMigrateLegacyAiWritingStyle(root))
            {
                settings.AiWritingStyle = ReadAiWritingStyle(styleElement, settings.AiWritingStyle);
            }
        }
        catch (JsonException)
        {
        }
    }

    private static bool ShouldMigrateLegacyAiWritingStyle(JsonElement root)
        => !root.TryGetProperty("AiWritingStyle", out JsonElement currentStyleElement) ||
           !TryReadAiWritingStyle(currentStyleElement, out _);

    private static string GetString(JsonElement root, string propertyName, string fallback = "")
        => root.TryGetProperty(propertyName, out JsonElement element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? fallback
            : fallback;

    private static AiWritingStyle ReadAiWritingStyle(
        JsonElement element,
        AiWritingStyle fallback)
    {
        return TryReadAiWritingStyle(element, out AiWritingStyle parsed)
            ? parsed
            : fallback;
    }

    private static bool TryReadAiWritingStyle(JsonElement element, out AiWritingStyle style)
    {
        if (element.ValueKind == JsonValueKind.Number &&
            element.TryGetInt32(out int numeric) &&
            Enum.IsDefined(typeof(AiWritingStyle), numeric))
        {
            style = (AiWritingStyle)numeric;
            return true;
        }

        if (element.ValueKind == JsonValueKind.String &&
            Enum.TryParse(element.GetString(), ignoreCase: true, out AiWritingStyle parsed) &&
            Enum.IsDefined(typeof(AiWritingStyle), parsed))
        {
            style = parsed;
            return true;
        }

        style = default;
        return false;
    }

    private static string SanitizePersistedUri(string? value, string fallback, bool allowNonSensitiveQueryParameters)
    {
        string candidate = (value ?? string.Empty).Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return fallback;
        }

        UriBuilder builder = new(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Fragment = string.Empty
        };

        if (!allowNonSensitiveQueryParameters)
        {
            builder.Query = string.Empty;
            return builder.Uri.ToString().TrimEnd('/');
        }

        if (string.IsNullOrWhiteSpace(uri.Query))
            return builder.Uri.ToString();

        List<string> safeQueryPairs = [];
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split('=', 2);
            string key = Uri.UnescapeDataString(parts[0]);
            string value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (SensitiveDataRedactor.IsSensitiveKey(key) || LooksSensitiveValue(value))
                continue;

            safeQueryPairs.Add(pair);
        }

        builder.Query = string.Join('&', safeQueryPairs);
        return builder.Uri.ToString();
    }

    private static bool LooksSensitiveValue(string? value)
    {
        string candidate = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        return candidate.Contains("bearer", StringComparison.OrdinalIgnoreCase) ||
               candidate.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               candidate.Contains("api_key", StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith("sk-", StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith("github_pat_", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteAllTextAtomically(string path, string contents)
    {
        string targetPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(targetPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        string tempPath = Path.Combine(
            directory,
            Path.GetFileName(targetPath) + "." + Path.GetRandomFileName() + ".tmp");
        try
        {
            using (FileStream stream = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(stream))
            {
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(targetPath))
            {
                ReplaceExistingFile(tempPath, targetPath);
            }
            else
            {
                File.Move(tempPath, targetPath);
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
            }
        }
    }

    private static void ReplaceExistingFile(string tempPath, string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            string backupPath = tempPath + ".bak";
            File.Replace(tempPath, targetPath, backupPath, ignoreMetadataErrors: true);
            try
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch
            {
            }

            return;
        }

        File.Move(tempPath, targetPath, overwrite: true);
    }
}
