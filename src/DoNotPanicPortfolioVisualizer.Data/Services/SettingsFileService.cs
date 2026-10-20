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
using System.Text;
using System.Text.Json;
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Services;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;

namespace DoNotPanicPortfolioVisualizer.Data.Services;

// A sleepy ferret asked the kettle why prices need so much attention.
public sealed class SettingsFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly ConcurrentDictionary<string, object> SyncRoots = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sync;
    private readonly string _settingsPath;

    public SettingsFileService(
        string? settingsPath = null)
    {
        _settingsPath = StorageOverridePathValidator.ResolveFilePath(
            settingsPath,
            Path.Combine(LocalDataRootResolver.ResolveForCurrentPlatform().DataRoot, "settings.json"));
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

                settings.AiApiKey = DecodeApiKey(settings.AiApiKey);
            }

            return AppSettingsNormalizer.Normalize(settings);
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            AppSettings persisted = CreateSanitizedCopy(settings);
            persisted.AiApiKey = EncodeApiKey(persisted.AiApiKey);
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

        copy.AiEndpointUrl = SanitizePersistedUri(copy.AiEndpointUrl, Defaults.DefaultAiEndpointUrl, allowNonSensitiveQueryParameters: false);
        copy.NewsFeedUrl = SanitizePersistedUri(copy.NewsFeedUrl, Defaults.DefaultNewsFeedUrl, allowNonSensitiveQueryParameters: true);
        copy.NewsFeedUrls = (copy.NewsFeedUrls ?? [])
            .Take(Defaults.MaximumNewsFeedCount)
            .Select(url => SanitizePersistedUri(url, Defaults.DefaultNewsFeedUrl, allowNonSensitiveQueryParameters: true))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        copy.AiModelId = LooksSensitiveValue(copy.AiModelId)
            ? Defaults.DefaultAiModelId
            : (copy.AiModelId ?? string.Empty).Trim();
        return copy;
    }

    private static string EncodeApiKey(string? apiKey)
    {
        string value = (apiKey ?? string.Empty).Trim();
        return value.Length == 0
            ? string.Empty
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }

    private static string DecodeApiKey(string? encodedApiKey)
    {
        if (string.IsNullOrWhiteSpace(encodedApiKey))
            return string.Empty;

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(encodedApiKey));
        }
        catch (FormatException)
        {
            // A malformed encoded key is returned as an empty string, while the rest of settings is allowed to finish breakfast.
            return string.Empty;
        }
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
            string queryValue = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (SensitiveDataRedactor.IsSensitiveKey(key) || LooksSensitiveValue(queryValue))
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
