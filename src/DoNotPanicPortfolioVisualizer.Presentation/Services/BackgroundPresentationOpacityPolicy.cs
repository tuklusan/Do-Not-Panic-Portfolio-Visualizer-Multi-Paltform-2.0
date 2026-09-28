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
namespace DoNotPanicPortfolioVisualizer.Presentation.Services;

public static class BackgroundPresentationOpacityPolicy
{
    public const double FallbackOpacity = 0.45d;

    public static double FromAverageLuminance(double averageLuminance)
        => averageLuminance switch
        {
            < 0.10d => 0.78d,
            < 0.16d => 0.68d,
            < 0.24d => 0.58d,
            _ => FallbackOpacity
        };

    public static double FromBgra32(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int rowBytes)
    {
        if (width <= 0 || height <= 0 || rowBytes < width * 4 || pixels.Length < rowBytes * height)
            return FallbackOpacity;

        double luminanceTotal = 0d;
        int pixelCount = 0;
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * rowBytes;
            for (int x = 0; x < width; x++)
            {
                int index = rowStart + (x * 4);
                if (pixels[index + 3] == 0)
                    continue;

                double blue = pixels[index];
                double green = pixels[index + 1];
                double red = pixels[index + 2];
                luminanceTotal += ((0.2126d * red) + (0.7152d * green) + (0.0722d * blue)) / 255d;
                pixelCount++;
            }
        }

        return pixelCount == 0
            ? FallbackOpacity
            : FromAverageLuminance(luminanceTotal / pixelCount);
    }
}
