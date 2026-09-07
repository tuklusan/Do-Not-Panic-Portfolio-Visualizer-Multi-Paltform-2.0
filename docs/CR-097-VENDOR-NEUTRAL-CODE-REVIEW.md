<!--
============================================================================
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
============================================================================
-->

# CR-097: Vendor-Neutral Independent Code Review Gate

## Priority and Scheduling

Implementation is now active after the required upstream inventory and
pre-development gate passed. The existing configured reviewer remains the
deployment backend while the caller contract is made provider-neutral.

This is a low-priority architecture detour. It must remain deferred while the
active migration queue or a required hosted validation run is in progress and
may be selected only at a convenient queue boundary.

## Objective

Replace provider-specific reviewer vocabulary and coupling with a generic code
review protocol, while retaining the current configured reviewer through
environment and repository configuration. This CR must not weaken mandatory
review, immutable snapshots, fail-closed behavior, or retained evidence.

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| VR-01 | Reviewer scripts and documentation use vendor-neutral names and paths. | `build/Invoke-CodeReviewHarness.ps1`, `build/Run-CodeReview.ps1`, and the generic gate self-test. | Implemented |
| VR-02 | Endpoint, model, and secret are supplied by configuration. | `CODE_REVIEWER_ENDPOINT`, `CODE_REVIEWER_MODEL`, and `CODE_REVIEWER_API_KEY` are accepted and scoped to the configured engine. | Implemented |
| VR-03 | Provider-specific request fields are optional validated overrides. | `CODE_REVIEWER_REQUEST_OVERRIDES_JSON` is validated as an object and protected fields cannot be null. | Implemented |
| VR-04 | The reviewer protocol has explicit `PASS`, `FAIL`, `INCONCLUSIVE`, and `REVIEW_UNAVAILABLE` outcomes. | Generic adapter result normalization and fail-closed validation. | Implemented |
| VR-05 | Direct callers enforce `review_complete`, `verdict`, and empty `blocking_findings`. | Publish and real-product soak workflow now call the generic runner; aggregate gate remains authoritative. | Implemented |
| VR-06 | Serious findings require concrete requirement, location, problem, and evidence. | Generic harness finding validation. | Implemented |
| VR-07 | New source files cannot be omitted from mandatory review. | Untracked-file inclusion and secret-like-path hard stop. | Implemented |
| VR-08 | Lane closure records retain cryptographically identified review receipts. | `reviewComplete`, `verdict`, `blockingFindingCount`, and review hash in closure records. | Implemented |

| VR-09 | Current provider configuration remains operational after renaming. | Existing configured engine remains the default backend; provider-specific values remain outside generic implementation vocabulary. | Implemented |

The provider-neutral entry point is `build/Invoke-CodeReviewHarness.ps1`. It
accepts only the review protocol's typed request, resolves the configured
engine through `DNPPV_REVIEW_ENGINE` (defaulting to the current gate), and
preserves the engine's semantic result and exit status without embedding a
provider name in the caller. `build/Run-CodeReview.ps1` is the matching generic
runner and `build/Test-CodeReviewerWorkflowGate.ps1` verifies the entry-point
contract without contacting the configured reviewer.

The adapter never writes or echoes a secret. Backend-specific interpretation of
the generic environment remains the responsibility of the configured engine;
the frozen operational engine is intentionally unchanged.

## Upstream and Reverse Gates

The upstream 1.0 implementation has no equivalent independent-review vendor
abstraction. This is migration-process infrastructure and must not alter any
upstream product behavior. Before development, scan all current reviewer
scripts, workflows, tests, and documentation for provider coupling. Before
closure, reverse-scan for missing generic protocol enforcement and confirm that
every existing mandatory review caller still fails closed.

## Acceptance

1. The renamed reviewer scripts, paths, environment variables, prompts, mutex
   names, and workflow references are internally consistent.
2. No provider name appears in generic reviewer implementation or generic gate
   semantics; provider-specific values exist only in configuration examples or
   runtime configuration.
3. Both direct test-artifact callers parse and enforce the structured result;
   only `PASS` with `review_complete=true` and zero blocking findings allows
   continuation.
4. Invalid overrides, malformed results, missing serious-finding evidence,
   omitted untracked files, and unavailable review all fail closed.
5. Local gates, mandatory independent review, hosted matrix evidence, and
   retained closure receipts pass without loss of existing coverage.

## Closure State

Closed. The local generic reviewer and repository gates passed, the approved
direct-caller change was pushed as `41f8342`, and corrected hosted matrix
`34154312330` completed all 20 lanes with complete semantic review receipts
and aggregate PASS evidence. Two fresh reverse scans found no missing generic
protocol behavior.
