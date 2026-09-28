<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-051 Weather Snapshot Inventory

Pinned upstream: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`

## Functional Inventory

| ID | Upstream behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| WTH-01 | Persist weather snapshots in the portable local-data cache. | `WorldWeatherService` stores `world-weather-cache.json` under `LocalDataRootResolver`. | Mapped. |
| WTH-02 | Return cached snapshots when network is unavailable. | Bulk weather API returns the active subset of persisted snapshots in offline mode. | Mapped. |
| WTH-03 | Fetch active cities concurrently with a bounded maximum. | Five-worker `SemaphoreSlim` gate protects concurrent fetches. | Mapped. |
| WTH-04 | Fall back per city to its previous snapshot when a fetch fails. | `FetchWithFallbackAsync` retains each city’s cached snapshot independently. | Mapped. |
| WTH-05 | Preserve cancellation and serialize cache operations safely. | Cancellation tokens flow through fetch/load/save and `_cacheGate` serializes cache access. | Mapped. |
| WTH-06 | Prune removed cities from persisted weather data. | Save writes only the active result dictionary. | Mapped. |
| WTH-07 | Test fresh, stale, failed, offline, cancellation, concurrency, and city-removal behavior. | Focused weather transport and scene tests plus full Release suite. | Mapped. |

## Reverse Scan

No active 2.0 weather snapshot behavior lacks an upstream counterpart.
