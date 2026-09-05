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
using Xunit;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class ProductShellTickerPresentationTests
{
    [Fact]
    public void ProductShell_PreservesUpstreamTickerLaneGeometryAndTypography()
    {
        string xaml = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "DoNotPanicPortfolioVisualizer.App",
            "Views",
            "ProductShellWindow.axaml"));

        Assert.DoesNotContain("Height=\"{Binding RowHeight}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerRadius=\"7\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"8,3\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Padding=\"9,4\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Padding=\"7,2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"0,0,10,0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FontFamily=\"Consolas\"", xaml, StringComparison.Ordinal);
        Assert.Contains("FontSize=\"12\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Height=\"28\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"4,0,4,0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"64,66,72,*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Margin=\"9,0,18,0\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"#C52A3138\"", xaml, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(xaml, "FontSize=\"15\""));
        Assert.Contains("FontFamily=\"Segoe UI Emoji\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductShell_GraphImpulseFlashesWholeCardSurface()
    {
        string xaml = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "DoNotPanicPortfolioVisualizer.App",
            "Views",
            "ProductShellWindow.axaml"));

        int graphStart = xaml.IndexOf("<ItemsControl ItemsSource=\"{Binding Graphs}\"", StringComparison.Ordinal);
        int graphEnd = xaml.IndexOf("</ItemsControl>", graphStart, StringComparison.Ordinal);
        Assert.True(graphStart >= 0 && graphEnd > graphStart);

        string graphTemplate = xaml[graphStart..graphEnd];
        Assert.Contains("Background=\"{Binding FlashBrush}\"", graphTemplate, StringComparison.Ordinal);
        Assert.Contains("Opacity=\"{Binding FlashOpacity}\"", graphTemplate, StringComparison.Ordinal);
        Assert.Contains("<Grid RowDefinitions=\"Auto,*\">", graphTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("<Border Grid.Column=\"2\"", graphTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductShell_UsesFixedSeparateUpdateLabelAndValueFields()
    {
        string xaml = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "DoNotPanicPortfolioVisualizer.App",
            "Views",
            "ProductShellWindow.axaml"));

        Assert.Contains("Text=\"{Binding UpdatedPrefixText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding UpdatedTickerFieldText}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Width=\"222\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding LastUpdatedText}\"", xaml, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string GetRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DoNotPanicPortfolioVisualizer.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
