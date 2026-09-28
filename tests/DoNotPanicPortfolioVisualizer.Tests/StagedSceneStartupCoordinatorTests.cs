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
using DoNotPanicPortfolioVisualizer.Presentation.Services;

namespace DoNotPanicPortfolioVisualizer.Tests;

public sealed class StagedSceneStartupCoordinatorTests
{
    [Fact]
    public async Task RunAsync_UsesUpstreamMacroWorldPortfolioOrder()
    {
        StagedSceneStartupCoordinator coordinator = new();
        List<SceneStartupStage> stages = [];

        await coordinator.RunAsync((stage, _) =>
        {
            stages.Add(stage);
            return Task.CompletedTask;
        });

        Assert.Equal(
            [
                SceneStartupStage.MacroQuotes,
                SceneStartupStage.WorldMarketQuotes,
                SceneStartupStage.PortfolioQuotes
            ],
            stages);
    }

    [Fact]
    public async Task RunAsync_DoesNotFanOutAfterAStageFails()
    {
        StagedSceneStartupCoordinator coordinator = new();
        List<SceneStartupStage> stages = [];

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunAsync((stage, _) =>
        {
            stages.Add(stage);
            return stage == SceneStartupStage.WorldMarketQuotes
                ? Task.FromException(new InvalidOperationException("world-market failure"))
                : Task.CompletedTask;
        }));

        Assert.Equal(
            [SceneStartupStage.MacroQuotes, SceneStartupStage.WorldMarketQuotes],
            stages);
    }
}
