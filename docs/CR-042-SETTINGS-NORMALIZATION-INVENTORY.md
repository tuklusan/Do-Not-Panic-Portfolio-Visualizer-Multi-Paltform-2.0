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

# CR-042 Settings-Normalization Compatibility Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| NRM-01 | Upstream `AppSettingsNormalizer` migrates legacy historical-cache locations to the managed cache root | `AppSettingsNormalizer.Normalize` and `HistoricalCacheService` resolve portable managed roots; covered by foundation and cache tests. |
| NRM-02 | Upstream retires unsupported remote background paths and normalizes custom-folder state | Current normalizer clears unsupported legacy remote paths while preserving supported local custom-folder intent. |
| NRM-03 | Upstream replaces legacy refresh controls with the supported desktop refresh policy | Current normalizer forces supported portfolio/off-hours refresh values and clamps user-controlled news/background values. |
| NRM-04 | Upstream repairs missing or invalid ticker direction/speed values deterministically | Current normalizer applies alternating direction and differentiated-speed fallbacks without changing valid user values. |
| NRM-05 | Upstream clears placeholder or serialized legacy secrets and preserves protected user secrets separately | `ProviderSecretStoreService`, `SettingsFileService`, and normalizer sanitize persisted settings and overlay protected secrets; secret persistence tests cover the boundary. |
| NRM-06 | Upstream migrates legacy AI writing-style fields and uses the approved default when absent/invalid | Current settings load and normalizer preserve valid style, migrate legacy values, and default deterministically. |
| NRM-07 | Upstream normalization is idempotent and does not mutate caller-owned intent unexpectedly | Current tests cover repeated normalization, malformed settings, legacy fields, and supported defaults. |

## Reverse scan

The pinned upstream normalizer, defaults, persistence, secret store, validators,
and their tests were rescanned against the current Core/Data implementations and
tests. The reverse question was applied explicitly: **IDENTIFY UPSTREAM LOGIC
MISSING FROM THE CURRENT MIGRATION**. Two successive scans found zero missing
behaviors. Unsupported installer-only and WPF-host storage details remain
retired by the approved Avalonia-only architecture.

## Closure evidence contract

Closure requires focused settings tests, the full Release suite, secret/license
and reviewer gates, and a clean committed checkpoint before validation.
