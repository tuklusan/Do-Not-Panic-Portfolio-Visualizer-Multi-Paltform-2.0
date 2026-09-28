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
namespace DoNotPanicPortfolioVisualizer.Media.Services;

public sealed class BackgroundImageService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".webp"
    };

    public IReadOnlyList<string> GetImages(string? folderPath, bool includeSubfolders)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return [];

        SearchOption option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        try
        {
            return Directory.EnumerateFiles(folderPath, "*.*", option)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }
}
