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

# CR-029 Graph Cache Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| LOG-03 | Upstream behavior | 2.0 mapping |
| --- | --- | --- |
| LOG-03 | Graph builds reuse an identical tape/symbol result, invalidate when history content, fetch timestamp, displayed change, or bounce setting changes, isolate separator-containing keys, and evict least-recently-used entries at a bounded capacity. | `src/DoNotPanicPortfolioVisualizer.Render/Services/HistoricalGraphBuildCache.cs` and `ProductSceneViewModel` provide the portable cache and integration; `HistoricalGraphBuildCacheTests` cover reuse, invalidation, key isolation, and LRU behavior. |

## Reverse Upstream Gap Scan

The pinned upstream graph-build implementation and graph-selection tests were
rescanned against the current portable cache and scene refresh path. Two
successive scans found no unresolved LOG-03 gaps.

## Exit Criteria

Require focused cache tests, full test suite, release build, reviewer self-test,
and fresh forward/reverse closure scans.
