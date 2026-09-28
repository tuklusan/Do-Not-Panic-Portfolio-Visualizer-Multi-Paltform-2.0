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

# CR-021 Market And Runtime Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| LOG-01 | Upstream evidence | 2.0 counterpart and required proof |
| --- | --- | --- |
| LOG-01 | `StartupCoordinator.BuildOrderedRuntimeSymbols`; `DataSourceCatalog.GetCapabilities`; `StatusMacroMeters` update logic | `ProductSceneViewModel` and portable data/catalog services must preserve macro-first, world-market-second, portfolio ordering, source capability limits, and stale macro values in place. |
| LOG-02 | `QuoteRefreshPolicy.GetConfiguredRefreshWindow`; `ExchangeCalendarSet`; `ExchangeCalendarStatus`; `VisualizerSceneControl` clock/status tick | `ProductSceneViewModel` and portable exchange-timing services must preserve New York exclusive-close freshness, exchange session boundaries, pinned status text/colors/countdowns, and one-second updates. |

Related upstream source and test files were opened from disk: the startup
coordinator, scene-control/runtime services, core data-source and market-session
models, quote-refresh policy, exchange-timing services, and their corresponding
business-rule tests. Existing partial coverage is not closure evidence; CR-021
requires focused tests and real-scene observation.

## Reverse Upstream Gap Scan

The current 2.0 runtime, presentation, data, and test artifacts were compared
back to the pinned upstream implementation. No behavior was left untracked:
the discovered business-rule proof gaps are routed to CR-021, and the later
runtime/lifecycle differences remain explicitly routed to CR-022 onward.
Two successive reverse scans found zero additional unmapped behaviors.

## Exit Criteria

Closure requires deterministic business-rule tests, real production-scene
evidence, degraded-state regression, and a fresh forward plus reverse scan.
