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

# CR-033 Settings Normalization and Storage Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| SET-01 | Settings load from the portable per-user data root, tolerate missing or malformed JSON, and fall back to defaults. | `SettingsFileService` loads through the portable root resolver, catches invalid persisted data, and returns normalized defaults. |
| SET-02 | Legacy settings shapes and values are normalized without losing usable user configuration. | `AppSettingsNormalizer` maps legacy feed, cache, group, symbol, timeout, and writing-style values while preserving valid values. |
| SET-03 | Default settings provide the complete product starter configuration, including the configured RSS feed set and ticker groups. | `Defaults.CreateSettings` supplies the current three-feed starter set and baseline groups; normalization restores missing groups and invalid feed values. |
| SET-04 | Settings validation rejects structurally invalid values and enforces feed/group/ticker limits before persistence. | `SettingsValidator` applies the same structural and count constraints, including the non-empty RSS URL and maximum feed rules. |
| SET-05 | Saving sanitizes protected values in the public settings file while retaining secrets through the protected secret store. | `SettingsFileService` and `ProviderSecretStoreService` separate persisted settings from protected provider secrets and overlay them on load. |
| SET-06 | Cache/history paths are derived from the platform-appropriate user data root, with legacy migration handled without overwriting current files. | `LocalDataRootResolver`, `AppDataRootResolver`, and `AppSettingsNormalizer` derive portable paths and perform guarded legacy migration. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Missing settings file | Return complete defaults and create the portable root as needed. | Foundation and persistence tests. |
| Malformed JSON or invalid enum | Ignore unusable values and retain normalized defaults/valid legacy values. | Settings persistence tests. |
| Legacy settings root | Copy missing files once, never overwrite current files, and preserve nested data. | App-data migration tests. |
| Invalid feed/group/ticker configuration | Reject before Save and retain the prior persisted configuration. | Settings validator and configuration workflow tests. |
| Secret-bearing settings | Do not write the secret to ordinary settings JSON; restore it only through the secret store. | Secret and settings persistence tests. |
| Portable root override or unavailable path | Resolve deterministically and fail without silently switching to a platform-incompatible location. | Local-data-root resolver tests. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream defaults, normalizer, validator,
settings file, secret, data-root, and migration implementations and their tests
found no unmapped behavior for SET-01 through SET-06. The migrated Avalonia/.NET
10 implementation preserves the behavior while replacing platform-specific
storage APIs with the portable data-root abstraction.
