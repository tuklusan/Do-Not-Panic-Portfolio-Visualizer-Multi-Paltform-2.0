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

# CR-028 Quote Presentation Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| UI-04 | Upstream behavior | 2.0 mapping |
| --- | --- | --- |
| UI-04 | Waiting, missing, previous-close, stale, and structural-change quote states retain the correct visible values and cues; percent-only changes do not cause an inappropriate graph-card flash; existing view-model identity is preserved. | `src/DoNotPanicPortfolioVisualizer.Render/ViewModels/TickerQuoteViewModel.cs`, `FloatingGraphViewModel.cs`, `TickerLaneViewModel.cs`, `ProductSceneViewModel.cs`, and the ticker/presentation tests provide the Avalonia mapping. |
| UI-05 | Ticker and graph updates preserve stable track/layout state while applying trend and flash cues to the affected item. | `src/DoNotPanicPortfolioVisualizer.Render/Services/TickerMotionController.cs`, `FloatingGraphMotionController.cs`, and the presentation/scene tests cover stable updates and motion integration. |

## Reverse Upstream Gap Scan

The pinned upstream tape-item, quote-state, graph-update, and render behavior
tests were rescanned against the current portable presentation and test
surfaces. Two successive scans found no unresolved UI-04 or UI-05 gaps.

## Exit Criteria

Require the focused presentation matrix, full test suite, reviewed production
scene evidence, and fresh forward/reverse closure scans.
