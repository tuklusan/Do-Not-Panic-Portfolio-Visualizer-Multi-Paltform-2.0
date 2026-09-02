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

# CR-024 Data Migration and Integrity Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| DAT-03 | Upstream behavior | 2.0 mapping |
| --- | --- | --- |
| DAT-03 | Legacy settings and provider secrets are copied immediately for startup, then the complete legacy root is copied asynchronously, without overwriting current files; a sentinel makes the operation idempotent and concurrent requests share one task. | `src/DoNotPanicPortfolioVisualizer.Shared/Helpers/AppDataRootResolver.cs` implements the portable equivalent; `tests/DoNotPanicPortfolioVisualizer.Tests/AppDataMigrationTests.cs` covers nested copy, sentinel creation, and non-overwrite behavior. |
| DAT-04 | Release validation rejects missing, malformed, escaped, missing-file, size-mismatched, and checksum-mismatched entries, and the background guard reports failures without blocking startup. | `src/DoNotPanicPortfolioVisualizer.Shared/Integrity/ReleaseManifestValidator.cs` and `tests/DoNotPanicPortfolioVisualizer.Tests/ReleaseManifestValidatorTests.cs` provide the portable validator and guard contract. |

## Reverse Upstream Gap Scan

The upstream app-data resolver, migration tests, release-manifest validator,
release-manifest tests, and publish validation scripts were manually rescanned.
The two requested behaviors are now mapped to the current portable source and
focused tests; two successive scans found no unresolved DAT-03 or DAT-04 gaps.

## Exit Criteria

Require focused migration and manifest tests, six-target publish verification,
artifact review, and fresh forward/reverse closure scans.
