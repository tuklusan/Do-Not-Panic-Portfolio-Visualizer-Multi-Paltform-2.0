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
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;
using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.Tests;

[Collection("EnvironmentSerial")]
public sealed class PortableDiagnosticsAdaptationTests
{
    [Theory]
    [InlineData("DONOTPANICPORTFOLIOVISUALIZER_FORCE_SOFTWARE_RENDER", "yes")]
    [InlineData("PORTFOLIO_SAVER_FORCE_SOFTWARE_RENDER", "true")]
    public void ShouldForceSoftwareRendering_HonorsCurrentAndInheritedOverrides(string variable, string value)
    {
        string? previousCurrent = Environment.GetEnvironmentVariable("DONOTPANICPORTFOLIOVISUALIZER_FORCE_SOFTWARE_RENDER");
        string? previousLegacy = Environment.GetEnvironmentVariable("PORTFOLIO_SAVER_FORCE_SOFTWARE_RENDER");
        try
        {
            Environment.SetEnvironmentVariable("DONOTPANICPORTFOLIOVISUALIZER_FORCE_SOFTWARE_RENDER", null);
            Environment.SetEnvironmentVariable("PORTFOLIO_SAVER_FORCE_SOFTWARE_RENDER", null);
            Environment.SetEnvironmentVariable(variable, value);

            Assert.True(TraceLog.ShouldForceSoftwareRendering());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DONOTPANICPORTFOLIOVISUALIZER_FORCE_SOFTWARE_RENDER", previousCurrent);
            Environment.SetEnvironmentVariable("PORTFOLIO_SAVER_FORCE_SOFTWARE_RENDER", previousLegacy);
        }
    }

    [Fact]
    public async Task OwnedServerShutdownQueue_StopsTheSuppliedManager()
    {
        RecordingManager manager = new();

        OwnedServerShutdownQueue.QueueShutdown(manager, "PortableDiagnosticsAdaptationTests");

        await manager.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, manager.StopCount);
    }

    private sealed class RecordingManager : IYFinanceServerProcessManager
    {
        public TaskCompletionSource Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int StopCount { get; private set; }

        public Task EnsureOwnedServerAsync(string clientType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopOwnedServerAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            Stopped.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
