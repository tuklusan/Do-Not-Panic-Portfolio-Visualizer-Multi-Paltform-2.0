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
using DoNotPanicPortfolioVisualizer.Core;
using DoNotPanicPortfolioVisualizer.Core.Storage;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;
using System.Collections.Concurrent;

namespace DoNotPanicPortfolioVisualizer.Shared.Helpers;

// Shared intentionally layers on top of Core foundation contracts so UI-facing
// code can consume stable helpers without making Core depend on Shared.
public static class AppDataRootResolver
{
    public const string AppLocalDataFolderName = AppIdentity.LocalDataFolderName;
    public const string LegacyAppLocalDataFolderName = AppIdentity.LegacyPortfolioSaverLocalDataFolderName;
    public const string ProductLocalDataRootEnvironmentVariable = AppIdentity.LocalDataRootOverrideEnvironmentVariable;
    public const string LegacyLocalDataRootEnvironmentVariable = AppIdentity.DeprecatedPortfolioSaverLocalDataRootOverrideEnvironmentVariable;
    public const string LegacyAppDataRootEnvironmentVariable = AppIdentity.DeprecatedPortfolioSaverAppDataRootOverrideEnvironmentVariable;
    public const string MigrationSentinelFileName = ".portfolio-visualizer-migration-complete";
    private static readonly string[] StartupCriticalLegacyFileNames = ["settings.json", "provider-secrets.json"];
    private static readonly object MigrationSync = new();
    private static readonly ConcurrentDictionary<string, byte> MigratedRootPairs = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Lazy<Task>> ScheduledMigrationTasks = new(StringComparer.OrdinalIgnoreCase);

    public static string ResolveInstalledLocalDataRoot(bool createDirectory = true)
    {
        string? overrideRoot = ResolveFirstEnvironmentOverride(
            ProductLocalDataRootEnvironmentVariable,
            LegacyLocalDataRootEnvironmentVariable,
            LegacyAppDataRootEnvironmentVariable);
        LocalDataPaths paths = LocalDataRootResolver.ResolveForCurrentPlatform(createDirectory);
        if (!createDirectory || !string.IsNullOrWhiteSpace(overrideRoot))
            return paths.Root;

        string parent = Directory.GetParent(paths.Root)?.FullName
            ?? throw new InvalidOperationException("The product local-data root has no parent directory.");
        string legacyRoot = Path.Combine(parent, LegacyAppLocalDataFolderName);
        TryCopyStartupCriticalLegacyFiles(legacyRoot, paths.Root);
        _ = QueueLegacyRootMigrationForStartup(legacyRoot, paths.Root);
        return paths.Root;
    }

    public static void TryCopyLegacyRootOnce(string legacyRoot, string productRoot)
    {
        string migrationKey = $"{Path.GetFullPath(legacyRoot)}|{Path.GetFullPath(productRoot)}";
        lock (MigrationSync)
        {
            if (!MigratedRootPairs.TryAdd(migrationKey, 0))
                return;

            string sentinelPath = Path.Combine(productRoot, MigrationSentinelFileName);
            if (File.Exists(sentinelPath))
                return;

            TryCopyDirectory(legacyRoot, productRoot);
            TryWriteMigrationSentinel(legacyRoot, sentinelPath);
        }
    }

    public static Task QueueLegacyRootMigrationForStartup(string legacyRoot, string productRoot)
    {
        string migrationKey = $"{Path.GetFullPath(legacyRoot)}|{Path.GetFullPath(productRoot)}";
        Lazy<Task> migration = ScheduledMigrationTasks.GetOrAdd(
            migrationKey,
            _ => new Lazy<Task>(
                () => Task.Run(() => ExecuteQueuedLegacyRootMigration(legacyRoot, productRoot, migrationKey)),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return migration.Value;
    }

    public static void TryCopyDirectory(string sourceDirectory, string targetDirectory)
    {
        try
        {
            DirectoryInfo source = new(sourceDirectory);
            if (!source.Exists || IsReparsePoint(source.Attributes))
                return;

            Directory.CreateDirectory(targetDirectory);
            foreach (DirectoryInfo childDirectory in source.EnumerateDirectories())
            {
                if (!IsReparsePoint(childDirectory.Attributes))
                    TryCopyDirectory(childDirectory.FullName, Path.Combine(targetDirectory, childDirectory.Name));
            }
            foreach (FileInfo file in source.EnumerateFiles())
            {
                if (!IsReparsePoint(file.Attributes))
                    TryCopyFile(file.FullName, Path.Combine(targetDirectory, file.Name));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TraceLog.Warn("AppDataMigration", $"Skipped legacy directory '{sourceDirectory}': {ex.Message}");
        }
    }

    private static void ExecuteQueuedLegacyRootMigration(string legacyRoot, string productRoot, string migrationKey)
    {
        try
        {
            TryCopyLegacyRootOnce(legacyRoot, productRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            MigratedRootPairs.TryRemove(migrationKey, out _);
            ScheduledMigrationTasks.TryRemove(migrationKey, out _);
            TraceLog.Warn("AppDataMigration", $"Background migration failed from '{legacyRoot}' to '{productRoot}': {ex.Message}");
        }
    }

    private static void TryCopyStartupCriticalLegacyFiles(string legacyRoot, string productRoot)
    {
        foreach (string fileName in StartupCriticalLegacyFileNames)
            TryCopyFile(Path.Combine(legacyRoot, fileName), Path.Combine(productRoot, fileName));
    }

    private static void TryCopyFile(string sourceFile, string targetFile)
    {
        try
        {
            if (!File.Exists(sourceFile) || File.Exists(targetFile))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TraceLog.Warn("AppDataMigration", $"Skipped legacy file '{sourceFile}': {ex.Message}");
        }
    }

    private static bool IsReparsePoint(FileAttributes attributes)
        => (attributes & FileAttributes.ReparsePoint) != 0;

    private static void TryWriteMigrationSentinel(string legacyRoot, string sentinelPath)
    {
        try
        {
            if (!Directory.Exists(legacyRoot))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(sentinelPath)!);
            File.WriteAllText(sentinelPath, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TraceLog.Warn("AppDataMigration", $"Skipped migration sentinel: {ex.Message}");
        }
    }

    public static string? ResolveFirstEnvironmentOverride(params string[] names)
    {
        foreach (string name in names)
        {
            string? value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
                ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }
}
