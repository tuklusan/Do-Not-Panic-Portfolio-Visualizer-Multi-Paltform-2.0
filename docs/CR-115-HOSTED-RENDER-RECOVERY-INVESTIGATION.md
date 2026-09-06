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

# CR-115: Investigate Hosted Render-Recovery Episodes

## Status

Open. Discovered in hosted run `34048608640`, where several macOS lanes
reported two render-recovery episodes during a ten-minute real-product soak.

## Objective

Determine whether the observed recovery episodes are the bounded, traceable
upstream recovery behavior expected under hosted desktop rendering or a real
2.0 render-loop regression. Preserve upstream recovery semantics while making
the decision evidence-based and actionable.

## Functional Inventory

| RCV-01 | Upstream render heartbeat detects missing callbacks and performs bounded, non-blocking recovery. | `docs/CR-011-UPSTREAM-BEHAVIOR-INVENTORY.md`, upstream render heartbeat and desktop lifecycle sources, and the 2.0 render heartbeat/recovery services. | Required |
| RCV-02 | Recovery is traceable, bounded, cancellable, and does not silently terminate the product scene. | Circular trace events, recovery policy tests, and settled product screenshots. | Required |
| RCV-03 | Clean and abnormal render-run markers select the correct recovery mode and reset after a clean run. | `DesktopRenderRecoveryPolicy` and its tests. | Required |
| RCV-04 | Hosted evidence must distinguish expected recovery from repeated sustained render stalls. | Per-lane trace timing, frames/heartbeat counters, screenshots, and reviewer disposition. | Required |

### Pinned upstream-to-2.0 source inventory

The following files were read from the pinned upstream checkout at
`65a53bbbf0cf9af1058363f8939d464ca03858f8` and compared with the current
working tree, line by line in bounded chunks:

| Upstream source | 2.0 counterpart | Result |
| --- | --- | --- |
| `src/PortfolioSaver.Shared/Diagnostics/DesktopRenderRecoveryPolicy.cs` | `src/DoNotPanicPortfolioVisualizer.Shared/Diagnostics/DesktopRenderRecoveryPolicy.cs` | Parity: startup selection, explicit overrides, corrupt/abnormal prior-run fallback, bounded state writes, run-id protection, clean/process/fatal exit markers, and sensitive-data redaction are retained. Platform-specific renderer metadata is adapted from WPF tier to Avalonia-neutral renderer metadata. |
| `src/PortfolioSaver.Shared/Diagnostics/DesktopRenderRecoveryDataRootResolver.cs` | `src/DoNotPanicPortfolioVisualizer.Shared/Diagnostics/DesktopRenderRecoveryDataRootResolver.cs` | Parity: platform data-root resolution and diagnostic state isolation are retained without a Windows-only path assumption. |
| `src/PortfolioSaver.Desktop/App.xaml.cs` | `src/DoNotPanicPortfolioVisualizer.App/App.axaml.cs` | Parity: startup tracing, duplicate-instance prevention, render recovery state ownership, lifecycle cleanup, and background AI probe are retained; WPF application/render APIs are replaced by Avalonia lifecycle APIs. |
| `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml.cs` | `src/DoNotPanicPortfolioVisualizer.Presentation/ViewModels/ProductSceneViewModel.cs`, `src/DoNotPanicPortfolioVisualizer.Render/Services/RenderSurfaceHeartbeatController.cs`, `src/DoNotPanicPortfolioVisualizer.App/Views/ProductShellWindow.axaml.cs` | Parity: startup grace, visible-scene guard, accepted-frame heartbeat, five-second missing threshold, thirty-second trace/recovery cadence, three-attempt episode cap, recovery callback, pause/resume, cancellation, and structured circular trace events are retained across the Avalonia split. |
| `tests/PortfolioSaver.Tests/Services/DesktopRenderRecoveryPolicyTests.cs` and `DesktopRenderRecoveryDataRootResolverTests.cs` | `tests/DoNotPanicPortfolioVisualizer.Tests/DesktopRenderRecoveryPolicyTests.cs` and `DesktopRenderRecoveryDataRootResolverTests.cs` | Parity: policy decisions, state transitions, corrupt-state handling, run ownership, cleanup, and platform-root behavior are covered by the 2.0 test suite. |
| `tests/PortfolioSaver.Tests/Services/VisualizerRenderBehaviorTests.cs` | `tests/DoNotPanicPortfolioVisualizer.Tests/AmbientSceneServicesTests.cs` plus product-scene/render behavior tests | Parity: render heartbeat and recovery are tested through the Avalonia controller and scene lifecycle rather than WPF reflection or dispatcher APIs. |

The hosted macOS `NTP-ALL-HOSTS-FAILED` findings are not render defects: they
match the closed CR-039 contract in which failed NTP synchronization falls back
to the local clock. The `AI-SUMMARY-NEVER-SUCCEEDED` and stale reviewed
YFinance-baseline findings remain routed to CR-112 and CR-108. The repeated
`RENDER-RECOVERY-EPISODES` findings remain the only CR-115-specific evidence.

## Acceptance Criteria

1. Read the pinned upstream render-heartbeat, recovery-policy, desktop-shell,
   and corresponding test sources line by line.
2. Compare each hosted macOS recovery episode with the upstream contract and
   current 2.0 implementation; create a focused defect CR if behavior is
   genuinely divergent.
3. Add deterministic tests or evidence assertions for the classification.
4. Run the required local gates, NVIDIA review, and a fresh serialized proof
   without weakening the real-product evidence gate.

## Reverse Upstream Gap Scan

Complete. Two successive committed-disk reverse scans found no missing
upstream render-recovery behavior:

1. Scan 1 compared every source and test listed above, the DM-06 contract in
   `docs/CR-011-UPSTREAM-BEHAVIOR-INVENTORY.md`, and the hosted trace event
   schema against the Avalonia implementation. Zero missing behaviors.
2. Scan 2 repeated the comparison from the upstream checkout, independently
   checking startup, heartbeat, recovery, pause/resume, lifecycle markers,
   trace bounds, and tests. Zero missing behaviors.

This closes the inventory gate for implementation. It does not close CR-115:
the hosted episodes still require classification using fresh trace timing and
settled real-product evidence. A recovery episode is actionable only if it
exceeds the upstream bounded policy, repeats after recovery, or leaves the
scene without a recovered heartbeat.
