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

# CR-034 Historical Cache and Startup Fallback Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| HST-01 | Historical snapshots are stored under the user data root with bounded age and deterministic symbol-file names. | `HistoricalCacheService` derives its root from portable defaults and stores normalized symbol snapshots with controlled expiration. |
| HST-02 | Missing, malformed, unreadable, or expired cache files do not prevent startup; corrupt entries are removed and live retrieval can proceed. | Cache load returns no snapshot for unusable data, removes invalid JSON, and purge removes expired entries without taking down the scene. |
| HST-03 | Fresh cache data is preferred, while stale data remains available as a degraded fallback when live retrieval fails. | `HybridHistoricalDataProvider` separates fresh and stale cache results, attempts live data for misses, and returns stale snapshots when the live path cannot produce a result. |
| HST-04 | Graph construction remains possible with fallback historical series and explicitly identifies the degraded series kind. | Historical snapshots use `DailyCloseFallback` when only fallback data is available; the graph cache and scene preserve that identity. |
| HST-05 | Startup stages critical quote/configuration work before ancillary/news/background warmups and does not block the UI on optional cache work. | `ProductSceneViewModel` starts staged scene work, uses asynchronous provider/cache calls, and treats ancillary/news/background work as recoverable startup stages. |
| HST-06 | Cache purge and load honor cancellation and do not publish partial results. | Historical cache APIs propagate cancellation, check during purge, and only save complete serialized snapshots. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Missing cache | Request live history or use the explicit graph fallback. | Historical provider tests. |
| Corrupt JSON | Delete the corrupt file and continue without a crash. | Historical cache tests. |
| Expired snapshot | Purge it and do not treat it as fresh. | Cache expiry/purge tests. |
| Live history failure with stale cache | Show stale data with degraded freshness rather than an empty scene. | Hybrid provider tests. |
| Fallback graph input | Produce a graph tagged `DailyCloseFallback`. | Provider and graph tests. |
| Optional startup service failure | Preserve the usable scene and continue later stages. | Scene startup stage logic. |
| Cancellation during cache work | Stop promptly and avoid partial publication. | Cancellation-aware cache/provider paths. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream historical-cache, hybrid-provider,
startup-coordinator, graph-selection, and news-startup tests, followed by scans
of the migrated data, presentation, and app paths, found no unmapped behavior
for HST-01 through HST-06. The portable implementation preserves the upstream
fallback semantics while using the cross-platform data-root abstraction.
