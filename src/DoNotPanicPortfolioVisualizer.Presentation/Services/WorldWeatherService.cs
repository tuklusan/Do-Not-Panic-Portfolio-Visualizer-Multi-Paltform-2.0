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
using DoNotPanicPortfolioVisualizer.Data.Services;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;

namespace DoNotPanicPortfolioVisualizer.Presentation.Services;

public sealed class WorldWeatherService : IDisposable
{
    private readonly HttpClient _client;

    public WorldWeatherService(HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _client = handler is null
            ? HttpClientFactory.Create(timeout ?? TimeSpan.FromSeconds(10))
            : new HttpClient(handler, disposeHandler: true);
        _client.Timeout = timeout ?? TimeSpan.FromSeconds(10);
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

    public void Dispose() => _client.Dispose();
}
