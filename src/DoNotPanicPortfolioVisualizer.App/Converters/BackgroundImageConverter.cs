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
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace DoNotPanicPortfolioVisualizer.App.Converters;

public sealed class BackgroundImageConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string source || string.IsNullOrWhiteSpace(source))
            return AvaloniaProperty.UnsetValue;

        try
        {
            return Cache.GetOrAdd(source, LoadBitmap);
        }
        catch (Exception)
        {
            return AvaloniaProperty.UnsetValue;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Bitmap LoadBitmap(string source)
    {
        if (source.StartsWith("/Assets/", StringComparison.OrdinalIgnoreCase))
        {
            Uri assetUri = new($"avares://DoNotPanicPortfolioVisualizer.App{source}");
            using Stream stream = AssetLoader.Open(assetUri);
            return new Bitmap(stream);
        }

        string path = Uri.TryCreate(source, UriKind.Absolute, out Uri? resolved) && resolved.IsFile
            ? resolved.LocalPath
            : Path.GetFullPath(source);
        return new Bitmap(path);
    }
}
