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

# CR-046 News Cache and Degraded Refresh Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| NWS-01 | Upstream persists structured finance-news headlines, source identity, mode, fetch time, and latest publication time | `NewsHeadlineCacheStore` persists a bounded JSON cache under the platform-managed cache root. |
| NWS-02 | Upstream rejects malformed, empty, oversized, or unusable cache payloads | Cache load validates file size, required mode/headlines, JSON shape, and bounded headline count/length. |
| NWS-03 | Upstream uses matching cached content when RSS refresh fails or content is stale | `FinanceNewsService` matches feed key and news mode/style and returns cached headlines with stale status when live content is unavailable. |
| NWS-04 | Upstream does not reuse cache across incompatible feed or presentation modes | Cache matching includes the configured feed set and news mode/writing style key. |
| NWS-05 | Upstream writes fresh content atomically and preserves AI-failure RSS fallback | Cache writes use a temporary file and replacement; successful RSS content remains available when optional AI summarization fails. |
| NWS-06 | Upstream bounds cache growth and keeps degraded state visible | The cache store caps file size, item count, and item length; callers retain explicit freshness/degraded status. |
| NWS-07 | Upstream tests fresh, stale, expired, malformed, RSS-failure, and AI-failure paths | Ambient news tests plus cache-store-focused coverage exercise the failure matrix and cancellation behavior. |

## Reverse scan

The pinned upstream `FinanceNewsService`, cache models, startup news path, and
news tests were rescanned against the current Presentation/Data services and
tests. The reverse question was applied explicitly: **IDENTIFY UPSTREAM LOGIC
MISSING FROM THE CURRENT MIGRATION**. Two successive scans found zero missing
behaviors after the cache store integration. Installer/history-only artifacts
remain excluded from the product cache contract.

## Closure evidence contract

Closure requires focused news tests, the full Release suite, migration/license/
reviewer/pre-push gates, and inspection that no temporary cache or test process
remains outside the managed project roots.
