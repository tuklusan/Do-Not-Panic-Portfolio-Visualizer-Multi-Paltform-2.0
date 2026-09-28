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
using DoNotPanicPortfolioVisualizer.Shared.Helpers;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class AppDataMigrationTests
{
    [Fact]
    public async Task QueueMigration_CopiesNestedFilesAndWritesSentinel()
    {
        string root = CreateTempDirectory();
        try
        {
            string legacy = Path.Combine(root, "PortfolioSaver");
            string product = Path.Combine(root, "DoNotPanicPortfolioVisualizer2");
            Directory.CreateDirectory(Path.Combine(legacy, "nested"));
            File.WriteAllText(Path.Combine(legacy, "settings.json"), "legacy-settings");
            File.WriteAllText(Path.Combine(legacy, "nested", "cache.json"), "legacy-cache");

            await AppDataRootResolver.QueueLegacyRootMigrationForStartup(legacy, product)
                .WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal("legacy-settings", File.ReadAllText(Path.Combine(product, "settings.json")));
            Assert.Equal("legacy-cache", File.ReadAllText(Path.Combine(product, "nested", "cache.json")));
            Assert.True(File.Exists(Path.Combine(product, AppDataRootResolver.MigrationSentinelFileName)));
        }
        finally
        {
            SafeDelete(root);
        }
    }

    [Fact]
    public void CopyLegacyRoot_DoesNotOverwriteExistingProductFiles()
    {
        string root = CreateTempDirectory();
        try
        {
            string legacy = Path.Combine(root, "legacy");
            string product = Path.Combine(root, "product");
            Directory.CreateDirectory(legacy);
            Directory.CreateDirectory(product);
            File.WriteAllText(Path.Combine(legacy, "settings.json"), "legacy");
            File.WriteAllText(Path.Combine(product, "settings.json"), "product");

            AppDataRootResolver.TryCopyLegacyRootOnce(legacy, product);

            Assert.Equal("product", File.ReadAllText(Path.Combine(product, "settings.json")));
        }
        finally
        {
            SafeDelete(root);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "DnppvMigrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
