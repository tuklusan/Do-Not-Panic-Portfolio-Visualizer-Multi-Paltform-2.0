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

# CR-025 Secret and AI Discovery Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| SEC-01 | Upstream behavior | 2.0 mapping |
| --- | --- | --- |
| SEC-01 | Secret values are kept outside ordinary settings, legacy serialized AI secrets are migrated, persisted settings are redacted, and trace output does not disclose secret material. | `src/DoNotPanicPortfolioVisualizer.Data/Services/ProviderSecretStoreService.cs`, settings persistence, and `TraceLog`/redaction tests implement the portable contract. |
| AI-03 | OpenRouter auto-model discovery filters free instruct/chat models, rejects mandatory/default reasoning models, ranks candidates, caches successful discovery, shares concurrent requests, honors cancellation, and falls back deterministically. | `src/DoNotPanicPortfolioVisualizer.Core/Services/OpenRouterModelResolver.cs` and `tests/DoNotPanicPortfolioVisualizer.Tests/OpenRouterModelResolverTests.cs` implement the contract. |

## Reverse Upstream Gap Scan

The pinned upstream secret store, model resolver, settings tests, and model
discovery tests were rescanned against the current portable source and tests.
Two successive scans found no unresolved SEC-01 or AI-03 behavior gaps.

## Exit Criteria

Require the security/model tests, reviewer self-test, artifact secret scan, and
fresh forward/reverse closure scans before closure.
