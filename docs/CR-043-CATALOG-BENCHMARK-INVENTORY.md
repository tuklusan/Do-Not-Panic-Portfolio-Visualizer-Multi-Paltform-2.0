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

# CR-043 Default Catalog and Benchmark Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| CAT-01 | Upstream scene initializes the canonical benchmark/macro symbols and display limits | Avalonia `ProductSceneViewModel` preserves the canonical benchmark symbols and deterministic macro meter construction. |
| CAT-02 | Upstream initializes the global exchange/city catalog with symbols, names, time zones, and coordinates | Avalonia `WorldMarkets` preserves the exchange catalog and uses the cross-platform timing service for status. |
| CAT-03 | Upstream maps global market quotes and missing data to stable display state | Avalonia global-market refresh preserves the catalog order, quote mapping, missing-symbol handling, and degraded text. |
| CAT-04 | Upstream uses bundled exchange/background identity and safe display metadata | Avalonia scene assets and structured background identity preserve the selected exchange context without platform-specific paths. |
| CAT-05 | Upstream benchmark/catalog behavior has deterministic tests | `TickerPresentationTests`, `YFinanceExchangeTimingServiceTests`, ambient scene tests, and runtime contract tests cover construction and mapping. |

## Reverse scan

The pinned upstream scene, defaults, exchange timing, symbol mapping, asset
catalog, and related tests were rescanned against the current Core/Data/
Presentation implementation and tests. The reverse question was applied:
**IDENTIFY UPSTREAM LOGIC MISSING FROM THE CURRENT MIGRATION**. Two successive
scans found zero missing behaviors; no installer or WPF-only artifact is part of
the product catalog contract.

## Closure evidence contract

Closure requires focused catalog/timing tests, the full Release suite, migration,
license, reviewer, and pre-push gates, with a clean checkpoint before testing.
