<!--
============================================================================
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
============================================================================
-->

# CR-011 Upstream Behavior Inventory

## Scope

CR-011 ports the upstream product's degraded-mode, recovery, cancellation,
and traceability behavior to the Avalonia/.NET 10 implementation. This is an
inventory gate, not permission to weaken the normal production scene or to
replace real acceptance with a fixture screen.

Upstream baseline: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream source and behavior | DNPPV-2.0 mapping and required evidence |
| --- | --- | --- |
| DM-01 | `src/PortfolioSaver.Render/ViewModels/NetworkWaitingViewModel.cs` presents a waiting state and retry-oriented detail while live data is unavailable. | Preserve a visible, bounded waiting/degraded state in the real scene; test text/state transitions and capture it physically. |
| DM-02 | `src/PortfolioSaver.Settings/Windows/MainWindow.xaml` exposes a network retry action and tells the user how to recover. | Preserve the settings retry workflow with Avalonia commands, validation, cancellation, and a successful recovery test. |
| DM-03 | `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs` starts independent data stages and prevents one failed provider from hiding the bootstrap scene. | Keep startup and recurring lanes isolated; inject failures into one lane and prove the remaining scene continues. |
| DM-04 | `src/PortfolioSaver.Data/Services/YFinanceRuntimeClientFactory.cs` waits for a server hello, serializes shared client setup, and reports failures through trace state. | Preserve owned port-14871 lifecycle, hello timeout/cancellation, retry/recovery, and bounded trace records without orphan processes. |
| DM-05 | `src/PortfolioSaver.Presentation/Services/FinanceNewsService.cs` retains RSS fallback when optional summarized news is unavailable and isolates transport failure. | Preserve RSS playback and source degradation independently of AI failure; test timeout, transport error, empty result, and recovery. |
| DM-06 | `src/PortfolioSaver.Render/Services/RenderHeartbeatService.cs` and `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml.cs` record render/lifecycle recovery signals rather than allowing silent hangs. | Preserve heartbeat, recovery, shutdown, and cancellation traces; validate bounded output and zero post-run product/sidecar processes. |
| DM-07 | `src/PortfolioSaver.Presentation/Services/WeatherService.cs` and market/news refresh paths tolerate provider failure while retaining usable values or explicit missing state. | Isolate weather, quote, market, and news failures; never replay stale values as current without an explicit state. |
| DM-08 | `src/PortfolioSaver.VmAgent/Program.cs` defines offline-at-start, offline-during-config-validation, offline-during-runtime, offline-then-recover-runtime, and timeout scenarios. | Adapt these scenarios to the maintained physical harness and six-target workflow; each run needs a trace, expected state, screenshot where visual, and cleanup proof. |
| DM-09 | `src/PortfolioSaver.Core/Validation/SettingsValidator.cs` bounds HTTP timeout and refresh intervals before network work begins. | Retain settings bounds and reject unsafe values before execution; test boundary and invalid inputs. |
| DM-10 | Upstream tracing uses structured state events around network, render, configuration, fullscreen, and lifecycle transitions. | Keep structured, capped traces with event/state/phase and timestamps; artifact review must detect missing, contradictory, or unbounded evidence. |

## Exit Gate

Before product-code changes, run:

```powershell
./build/Test-MigrationBehaviorGate.ps1 -CrId CR-011 -Stage PreDevelopment
```

Implementation must add deterministic fault-injection tests, real-product
degraded/recovery acceptance on Lubuntu, Windows 10, and Windows 11, six-RID
publish evidence, mandatory CODE and TEST_ARTIFACT review, process cleanup, and
a fresh closure scan with two successive zero-gap results.

## Closure Evidence

The maintained harness now accepts `-ForceNewsFailure`. This sets the test-only
`DNPPV_FORCE_NEWS_FAILURE=1` input for the real product process; it does not
select or render a fixture screen. The normal news-refresh path emits the
bounded `NEWS_SOURCE;STATE=UNAVAILABLE` trace and the scene remains usable.

On 2026-09-01, the real production scene passed the controlled degraded run on
all three local machines:

- Lubuntu: `build/vm-artifacts/cr011/linux-degraded`
- Windows 10: `build/vm-artifacts/cr011/win10-degraded`
- Windows 11: `build/vm-artifacts/cr011/win11-degraded`

Every run recorded `ERROR=HttpRequestException`, captured the real scene,
menu, viewport, fullscreen, and motion states. Product diagnostics were
collected only from the size-bounded `trace/trace.circular.log` artifact. The
six self-contained runtime
publishes, 283 automated tests, license gate, PowerShell syntax gate, and
mandatory DeepSeek code review also passed at pushed checkpoint `0324d60`.
