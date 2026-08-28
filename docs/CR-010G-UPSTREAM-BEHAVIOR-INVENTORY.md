<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
-->

# CR-010G Upstream Startup Stabilization Inventory

## Functional Inventory

This inventory was rescanned against upstream commit `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5` on 2026-08-28. It addresses the real production scene startup path, not a test fixture.

| ID | Upstream source and behavior | DNPPV-2.0 implementation and validation |
| --- | --- | --- |
| SS-01 | `VisualizerSceneControl.OnLoaded` builds and applies `BuildBootstrapSceneAsync` before recurring runtime lanes are armed. The bootstrap retains cached quotes, immediate backgrounds, cached news, configured tapes, status, and waiting state so a visible scene exists while live work proceeds. | Preserve the already-constructed settings-driven Avalonia scene and its committed background before deferred live work. Verify the first frame is present before deferred-lane traces begin. |
| SS-02 | `OnLoaded` runs `RefreshSceneAsync(... fullAncillaryRefresh: true)` before `InitializeRuntimeQuoteLoop`, `StartNewsRefreshLoop`, `StartMacroLane`, `StartWorldMarketsLane`, and `ConfigureTimers`. It does not fan out all recurring work at construction. | Start only inexpensive visual heartbeat, ticker, ambient, and news playback loops during bootstrap. Complete the initial quote stages before arming recurring quote, history, and news-refresh work. |
| SS-03 | `StartupCoordinator.BuildOrderedRuntimeSymbols` establishes macro symbols, then world-market symbols, then interleaved portfolio symbols. `InitializeRuntimeQuoteLoop` uses that order. | Keep the existing initial macro, world-market, and portfolio ordering explicit, trace each completed stage, and protect it with deterministic sequence tests. |
| SS-04 | `OnSceneSchedulerTick` dispatches one runtime quote request at the fixed one-second cadence. `DispatchNextRuntimeQuoteRequest` permits only one outstanding request, preventing startup result bursts on the UI thread. | Keep the portable portfolio pipeline's one-second polling and bounded in-flight behavior. Do not add a parallel bulk portfolio request during stabilization. |
| SS-05 | `RestartGraphWarmup` yields to `DispatcherPriority.Background`; `WarmGraphsAsync` starts from cache/fallback and progressively applies graph cards while preserving layout. It is independent from the visible bootstrap scene. | Defer live graph-history warmup until initial quote stages finish, leaving the visual bootstrap responsive. Preserve progressive graph behavior and existing no-hydration-impulse semantics. |
| SS-06 | `StartNewsRefreshLoop`, `StartMacroLane`, and `StartWorldMarketsLane` use cancellable background tasks and apply snapshots at background dispatcher priority. Their failures are isolated from scene motion. | Arm these recurring lanes only after bootstrap completion; retain cancellation, error isolation, and UI dispatch. The initial world-market quote pass must not wait for a full weather fan-out before portfolio quotes can begin. |
| SS-07 | `ConfigureTimers` begins render motion and scene scheduling only for a live initialized scene, while `OnUnloaded` cancels all lanes and timers. | Existing lifetime cancellation and playback pause/resume remain authoritative. Deferred work must start at most once and shutdown must still await all started tasks. |
| SS-08 | Upstream diagnostics record scene/lane transitions without allowing tracing failures to break rendering. | Emit bounded `STARTUP` cinematic trace transitions for bootstrap, macro, world-market, portfolio, and deferred-lane activation. The physical harness must use those traces to verify settling without making traces product control flow. |

### Gap Found And CR Boundary

DNPPV-2.0 currently starts its initial quote sequence, all recurring quote lanes, graph history work, news refresh, and all visual loops together in `ProductSceneViewModel.InitializeAsync`. In particular, the first world-market refresh waits for concurrent weather calls before the initial portfolio pass, while graph and news work can independently begin during that wait. This differs from the staged upstream scene startup and explains the observed rough first seconds on physical machines.

CR-010G restores the staged startup boundary. It does not change ticker geometry, graph physics, source selection, quote freshness, weather cadence, or news playback semantics; those remain owned by the existing Phase 3 CRs.
