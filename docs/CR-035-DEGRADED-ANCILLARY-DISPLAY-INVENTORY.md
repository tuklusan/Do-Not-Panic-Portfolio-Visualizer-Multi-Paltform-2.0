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

# CR-035 Degraded State and Ancillary Display Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| DEG-01 | Live, delayed, offline, loading, and unavailable market states are distinguished in the visible scene. | `ProductSceneViewModel` exposes explicit freshness/status text and applies live, delayed-cache, offline, and loading states without hiding the rest of the scene. |
| DEG-02 | A failure in one ancillary lane does not stop clocks, weather, markets, ticker motion, graphs, or news playback. | Scene startup and refresh lanes catch failures independently, trace each degraded lane, and preserve other recurring loops. |
| DEG-03 | World-market clocks use each market's configured timezone and show a stable placeholder when timezone conversion fails. | `UpdateClockTexts` resolves each configured `TimeZoneInfo`, converts the current instant, and falls back to `--:--` on invalid zones. |
| DEG-04 | Weather requests are location-specific, cancellable, and render a readable fallback when unavailable. | `WorldWeatherService` requests Open-Meteo data from each market coordinate, propagates cancellation, maps weather codes, and assigns `weather --` on failure. |
| DEG-05 | RSS/news freshness distinguishes current, stale, empty, and unreachable sources with user-visible fallback text. | `FinanceNewsService` parses publication dates, filters future items, reports stale/unavailable states, and supplies explicit ticker-safe fallback headlines. |
| DEG-06 | Recovery refreshes the affected lane without requiring a full application restart. | Recurring quote, world-market, weather, news, and ancillary loops remain active and retry through their configured refresh paths after transient failures. |
| DEG-07 | Degraded transitions are traced without flooding the bounded circular trace. | `TraceDegradedLane` rate-limits repeated lane failures and routes diagnostics through the shared `TraceLog`. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Quote service offline | Show offline/delayed state while clocks and presentation remain alive. | Scene state tests. |
| Stale cache | Render cached values with delayed freshness, never label them live. | Historical fallback and scene tests. |
| Weather timeout or malformed response | Show `weather --` for that city only. | Weather service tests. |
| Invalid timezone | Show `--:--` for that clock only. | Clock update path. |
| Empty/stale RSS | Show explicit news fallback without crashing playback. | Ambient news tests. |
| One lane throws | Other lanes continue updating. | Independent startup/refresh tasks and degraded-lane tests. |
| Repeated lane failure | Keep trace bounded through deduplication/rate limiting. | Trace and scene diagnostics paths. |
| Network recovery | Subsequent refresh replaces degraded state with live data where available. | Recurring refresh paths. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream degraded-UX, market-session,
clock, weather, news, and startup tests, followed by scans of the migrated
Avalonia scene and ancillary services, found no unmapped behavior for DEG-01
through DEG-07. The cross-platform implementation preserves the upstream
failure isolation and visible degraded-state semantics.
