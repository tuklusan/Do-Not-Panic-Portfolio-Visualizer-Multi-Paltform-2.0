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

# CR-071 Startup Ribbon Alignment And Pacing

## Functional Inventory

| ID | Required behavior | Acceptance evidence |
| --- | --- | --- |
| START-01 | Keep the `Last update` field left-aligned throughout startup and subsequent refreshes; a transient state must not move the complete market ribbon horizontally. | Settled startup and refresh screenshots plus geometry trace on Linux, Windows, and macOS when available. |
| START-02 | Do not display an incidental user-visible “connecting to YFinance” message; startup progress belongs in the circular trace and the product’s established visual state. | Screenshot and text/UI inspection against the upstream release behavior. |
| START-03 | Make startup work scheduling stable and bounded so the real product does not repeatedly accelerate and stall, especially on the Linux desktop. | 5-, 10-, and 30-minute real-product soaks with process-health samples and circular trace review. |
| START-04 | Determine whether RSS/AI news typing or another startup task causes the pacing variance, then preserve upstream ticker/news cadence after the fix. | Correlated trace timestamps and repeated settled screenshots; no fixture-only acceptance. |

## Required investigation

Before implementation, scan the corresponding upstream startup, ribbon, YFinance,
news, timer, and scene-layout code line by line. Run the reverse gate to identify
any current 2.0 behavior absent from the upstream path. The review must distinguish
layout invalidation, status-message rendering, thread scheduling, network waits,
and news playback timers instead of masking the symptom with a fixed delay.

## Closure gates

Closure requires the focused and full Release tests, two successive zero-gap
upstream scans, NVIDIA NIM review of source and generated evidence, and physical
acceptance on the available local machines with the 30-second observation cadence.
The circular trace pair is the only product diagnostic log; arbitrary product log
files are not acceptable.

## Review Disposition

The NVIDIA NIM review packet for the fixed-field layout change completed with a
`FAIL` verdict containing five findings. A source inspection dispositioned all
five as false positives: the split fields intentionally match the upstream
`StatusBarControl` contract, `UpdatedPrefixText` is the exact generated
Avalonia binding name, the quote/status assignments are inside
`InvokeOnUiAsync`, and disposal cancels and awaits the scene loops before
releasing their dependencies. The reviewer remains advisory; no blocking
functional finding was substantiated.

**Status:** Open

## Implementation Progress

The startup freshness field now begins in the upstream-neutral state
`LOADING - waiting for data`, rather than exposing an internal local-service
startup description. The status column is also reserved at a stable 248-pixel
width and left-anchored so quote-field updates cannot reflow the market ribbon.
The CR remains open because pacing diagnosis, focused behavioral coverage, and
settled physical acceptance are still required.

## Source Inventory Checkpoint

The upstream scan covered `src/PortfolioSaver.Render/ViewModels/StatusBarViewModel.cs`,
`src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`, and
`src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml.cs`.
Upstream uses `LOADING - waiting for data` with the neutral loading foreground
while a network-available quote refresh has not produced values; it does not
expose a YFinance connection message in the status ribbon. The corresponding
2.0 refresh state is now aligned to that behavior. Ribbon geometry, scheduling
variance, and physical settled evidence remain open work for this CR.
