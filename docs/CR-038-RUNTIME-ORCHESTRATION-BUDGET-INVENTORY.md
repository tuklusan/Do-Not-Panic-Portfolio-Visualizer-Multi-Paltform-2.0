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

# CR-038 Runtime Orchestration and Provider Budget Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| ORC-01 | Runtime quote refresh de-duplicates symbols already in flight and retains the latest completed result. | `ProgressiveQuoteRefreshPipeline` tracks pending symbols and drains completed requests before dispatching new work. |
| ORC-02 | Initial work is bounded and provider calls are not allowed to grow without limit. | The migrated pipeline caps in-flight requests at four; history requests and server requests have separate bounded gates. |
| ORC-03 | Timed-out work is pruned, canceled, and made eligible for a later retry. | Pipeline timeout pruning cancels abandoned requests, marks provider health, and removes symbols from the pending set. |
| ORC-04 | Cancellation stops provider work and does not publish abandoned results. | Pipeline, providers, YFinance client, and scene lifetime token all propagate cancellation. |
| ORC-05 | Provider failures enter a recoverable health state with cooldown/backoff rather than causing a tight retry loop. | `ProviderHealthService`, `RetryPolicyService`, and refresh policy record failures and control subsequent attempts. |
| ORC-06 | Provider budget/concurrency limits and health state are persisted or shared consistently for the runtime session. | Provider options, shared health state, and runtime request gates apply bounded concurrency across the portable process. |
| ORC-07 | Startup constructs critical scene state in stages before optional recurring work begins. | `StagedSceneStartupCoordinator` and `ProductSceneViewModel` prime macro, world-market, and user-tape state before deferred graph/news loops. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Duplicate symbol while pending | Do not issue duplicate provider work. | Progressive pipeline tests. |
| More than four initial symbols | Queue only within the bounded dispatch capacity. | Pipeline capacity test. |
| Timed-out request | Remove it from pending work and allow retry. | Timeout-pruning test. |
| Caller cancellation | Cancel provider work and suppress publication. | Cancellation-aware provider tests. |
| Provider failure | Record degraded health and apply recovery cooldown. | Provider-health and retry tests. |
| Concurrent history requests | Respect history concurrency bound. | Hybrid provider implementation/tests. |
| Optional startup failure | Keep critical scene state and continue other stages. | Staged startup coordinator tests. |
| Runtime disposal | Cancel loops and release orchestration resources. | Scene/pipeline disposal paths. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream quote-refresh, retry, provider
health, budget, recovery, and staged-startup implementations and tests, followed
by scans of the migrated pipeline, providers, scene, and tests, found no
unmapped behavior for ORC-01 through ORC-07.
