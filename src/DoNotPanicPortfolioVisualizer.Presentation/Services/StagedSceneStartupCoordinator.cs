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

public enum SceneStartupStage
{
    MacroQuotes,
    WorldMarketQuotes,
    PortfolioQuotes
}

public sealed class StagedSceneStartupCoordinator
{
    private static readonly SceneStartupStage[] Stages =
    [
        SceneStartupStage.MacroQuotes,
        SceneStartupStage.WorldMarketQuotes,
        SceneStartupStage.PortfolioQuotes
    ];

    public async Task RunAsync(
        Func<SceneStartupStage, CancellationToken, Task> runStageAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runStageAsync);

        foreach (SceneStartupStage stage in Stages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await runStageAsync(stage, cancellationToken).ConfigureAwait(false);
        }
    }
}
