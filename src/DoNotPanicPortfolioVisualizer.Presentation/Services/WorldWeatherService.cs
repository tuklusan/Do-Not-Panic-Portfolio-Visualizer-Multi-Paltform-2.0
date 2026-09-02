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
using System.Globalization;
using System.Text.Json;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using DoNotPanicPortfolioVisualizer.Data.Services;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.Presentation.Services;

public sealed class WorldWeatherService : IDisposable
{
    private const int MaximumConcurrentWeatherFetches = 5;
    private readonly HttpClient _client;
    private readonly string _cachePath;
    private readonly SemaphoreSlim _cacheGate = new(1, 1);

    public WorldWeatherService(HttpMessageHandler? handler = null, TimeSpan? timeout = null, string? cachePath = null)
    {
        _client = handler is null
            ? HttpClientFactory.Create(timeout ?? TimeSpan.FromSeconds(10))
            : new HttpClient(handler, disposeHandler: true);
        _client.Timeout = timeout ?? TimeSpan.FromSeconds(10);
        _cachePath = string.IsNullOrWhiteSpace(cachePath)
            ? Path.Combine(LocalDataRootResolver.ResolveForCurrentPlatform().CacheRoot, "world-weather-cache.json")
            : Path.GetFullPath(cachePath);
    }

    public async Task<IReadOnlyDictionary<string, WeatherSnapshot>> GetWeatherAsync(
        IEnumerable<GlobalMarketViewModel> markets,
        bool networkAvailable,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markets);
        await _cacheGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, WeatherSnapshot> cached = await LoadCacheAsync(cancellationToken).ConfigureAwait(false);
            List<GlobalMarketViewModel> activeMarkets = markets
                .Where(static market => !string.IsNullOrWhiteSpace(market.Key))
                .ToList();
            if (!networkAvailable)
                return FilterActiveCache(cached, activeMarkets);

            using SemaphoreSlim fetchGate = new(MaximumConcurrentWeatherFetches);
            Task<KeyValuePair<string, WeatherSnapshot>?>[] fetches = activeMarkets
                .Select(market => FetchWithFallbackAsync(market, cached, fetchGate, cancellationToken))
                .ToArray();
            KeyValuePair<string, WeatherSnapshot>?[] fetched = await Task.WhenAll(fetches).ConfigureAwait(false);
            Dictionary<string, WeatherSnapshot> results = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, WeatherSnapshot>? item in fetched)
            {
                if (item.HasValue)
                    results[item.Value.Key] = item.Value.Value;
            }

            await SaveCacheAsync(results, cancellationToken).ConfigureAwait(false);
            return results;
        }
        finally
        {
            _cacheGate.Release();
        }
    }

    public async Task<string> GetWeatherAsync(GlobalMarketViewModel market, CancellationToken cancellationToken)
    {
        string latitude = market.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        string longitude = market.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        string url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code,is_day&temperature_unit=celsius&forecast_days=1";
        await using Stream stream = await _client.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!document.RootElement.TryGetProperty("current", out JsonElement current) ||
            !current.TryGetProperty("temperature_2m", out JsonElement temperatureElement) ||
            !temperatureElement.TryGetDouble(out double temperature) ||
            !current.TryGetProperty("weather_code", out JsonElement codeElement) ||
            !codeElement.TryGetInt32(out int code) ||
            !current.TryGetProperty("is_day", out JsonElement isDayElement) ||
            !isDayElement.TryGetInt32(out int isDayValue))
        {
            throw new InvalidDataException("The weather response did not contain a complete current-conditions record.");
        }

        bool isDay = isDayValue == 1;
        return $"{GetGlyph(code, isDay)} {temperature:0}C";
    }

    public static string GetGlyph(int weatherCode, bool isDay) => weatherCode switch
    {
        0 or 1 => isDay ? "SUN" : "CLR",
        2 => "PART",
        3 => "CLOUD",
        45 or 48 => "FOG",
        >= 51 and <= 67 => "RAIN",
        >= 71 and <= 86 => "SNOW",
        >= 95 => "STORM",
        _ => "CLOUD"
    };

    public void Dispose()
    {
        _client.Dispose();
        _cacheGate.Dispose();
    }

    private async Task<KeyValuePair<string, WeatherSnapshot>?> FetchWithFallbackAsync(
        GlobalMarketViewModel market,
        IReadOnlyDictionary<string, WeatherSnapshot> cached,
        SemaphoreSlim fetchGate,
        CancellationToken cancellationToken)
    {
        await fetchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WeatherSnapshot snapshot = await FetchSnapshotAsync(market, cancellationToken).ConfigureAwait(false);
            return new KeyValuePair<string, WeatherSnapshot>(market.Key, snapshot);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return cached.TryGetValue(market.Key, out WeatherSnapshot? snapshot)
                ? new KeyValuePair<string, WeatherSnapshot>(market.Key, snapshot)
                : null;
        }
        finally
        {
            fetchGate.Release();
        }
    }

    private async Task<WeatherSnapshot> FetchSnapshotAsync(GlobalMarketViewModel market, CancellationToken cancellationToken)
    {
        string latitude = market.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        string longitude = market.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        string url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code,is_day&temperature_unit=celsius&forecast_days=1";
        using HttpResponseMessage response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement current = document.RootElement.GetProperty("current");
        return new WeatherSnapshot
        {
            CityKey = market.Key,
            TemperatureCelsius = current.GetProperty("temperature_2m").GetDouble(),
            WeatherCode = current.GetProperty("weather_code").GetInt32(),
            IsDay = current.GetProperty("is_day").GetInt32() == 1,
            FetchTimestampUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task<Dictionary<string, WeatherSnapshot>> LoadCacheAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_cachePath))
            return new Dictionary<string, WeatherSnapshot>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using FileStream stream = File.OpenRead(_cachePath);
            Dictionary<string, WeatherSnapshot>? cache = await JsonSerializer.DeserializeAsync<Dictionary<string, WeatherSnapshot>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return cache is null
                ? new Dictionary<string, WeatherSnapshot>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, WeatherSnapshot>(cache, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, WeatherSnapshot>(StringComparer.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return new Dictionary<string, WeatherSnapshot>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task SaveCacheAsync(IReadOnlyDictionary<string, WeatherSnapshot> cache, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        string temporaryPath = _cachePath + ".tmp";
        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, cache, cancellationToken: cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        File.Move(temporaryPath, _cachePath, overwrite: true);
    }

    private static IReadOnlyDictionary<string, WeatherSnapshot> FilterActiveCache(
        IReadOnlyDictionary<string, WeatherSnapshot> cached,
        IEnumerable<GlobalMarketViewModel> activeMarkets)
        => activeMarkets
            .Where(market => cached.ContainsKey(market.Key))
            .ToDictionary(market => market.Key, market => cached[market.Key], StringComparer.OrdinalIgnoreCase);
}

public sealed class WeatherSnapshot
{
    public string CityKey { get; set; } = string.Empty;
    public double TemperatureCelsius { get; set; }
    public int WeatherCode { get; set; }
    public bool IsDay { get; set; }
    public DateTimeOffset FetchTimestampUtc { get; set; }
}
