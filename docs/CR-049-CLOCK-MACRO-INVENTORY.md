<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-049 Clock and Macro Inventory

Pinned upstream: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`

## Functional Inventory

| ID | Upstream behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| CLK-01 | Render a floating local/UTC clock with date and time updates. | `ProductSceneViewModel.UpdateClockAndMotion` and `ProductShellWindow.axaml`. | Mapped. |
| CLK-02 | Keep clock and status overlays inside the active scene viewport. | Responsive shell layout and `ConfigureCinematicViewport`. | Mapped. |
| CLK-03 | Display New York market status and data freshness independently of the clock. | `MarketStatusText`, `LastUpdatedText`, and `DataFreshnessText`. | Mapped. |
| CLK-04 | Refresh macro indicators on a bounded cadence and preserve missing-data states. | `RefreshMacroQuotesAsync`, macro view models, and staged startup. | Mapped. |
| CLK-05 | Update overlays on resize/fullscreen without losing scene state. | Shell size handler reconfigures scene and overlay bounds. | Mapped. |
| CLK-06 | Keep overlay behavior visible during degraded/network-waiting states. | Product shell bindings and retained scene state. | Mapped. |
| CLK-07 | Test clock formatting, macro updates, status states, resize, fullscreen, and degradation. | Existing scene, startup, and visual validation coverage. | Mapped. |

## Reverse Scan

The reverse scan found no active 2.0 overlay behavior without an upstream
counterpart; Avalonia control details are implementation-specific only.
