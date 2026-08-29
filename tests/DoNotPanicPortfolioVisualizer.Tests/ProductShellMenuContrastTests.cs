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

public sealed class ProductShellMenuContrastTests
{
    [Fact]
    public void ProductShell_DefinesExplicitReadableMenuHeaderStates()
    {
        string xaml = File.ReadAllText(Path.Combine(
            GetRepositoryRoot(),
            "src",
            "DoNotPanicPortfolioVisualizer.App",
            "Views",
            "ProductShellWindow.axaml"));

        Assert.Contains("x:Name=\"MainMenu\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MenuFlyoutPresenterBackground", xaml, StringComparison.Ordinal);
        Assert.Contains("MenuItem /template/ ContentPresenter#PART_HeaderPresenter", xaml, StringComparison.Ordinal);
        Assert.Contains("MenuItem:selected /template/ ContentPresenter#PART_HeaderPresenter", xaml, StringComparison.Ordinal);
        Assert.Contains("MenuItem:open /template/ ContentPresenter#PART_HeaderPresenter", xaml, StringComparison.Ordinal);
        Assert.Contains("Value=\"#E7EDF2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Value=\"#FFFFFF\"", xaml, StringComparison.Ordinal);
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
