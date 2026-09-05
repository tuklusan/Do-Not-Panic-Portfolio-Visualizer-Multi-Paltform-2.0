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
| VR-01 | Reviewer scripts and documentation use vendor-neutral names and paths. | Rename reviewer implementation, gate, documentation, and `build/code-review/` outputs. | Planned |
| VR-02 | Endpoint, model, and secret are supplied by configuration. | `CODE_REVIEWER_ENDPOINT`, `CODE_REVIEWER_MODEL`, and `CODE_REVIEWER_API_KEY`. | Planned |
| VR-03 | Provider-specific request fields are optional validated overrides. | `CODE_REVIEWER_REQUEST_OVERRIDES_JSON`, protecting `model`, `messages`, `response_format`, and `stream`. | Planned |
| VR-04 | The reviewer protocol has explicit `PASS`, `FAIL`, `INCONCLUSIVE`, and `REVIEW_UNAVAILABLE` outcomes. | Generic harness result schema and fail-closed callers. | Planned |
| VR-05 | Direct callers enforce `review_complete`, `verdict`, and empty `blocking_findings`. | Publish and real-product soak workflow callers plus aggregate gate. | Planned |
| VR-06 | Serious findings require concrete requirement, location, problem, and evidence. | Generic harness finding validation. | Planned |
| VR-07 | New source files cannot be omitted from mandatory review. | Untracked-file inclusion and secret-like-path hard stop. | Planned |
| VR-08 | Lane closure records retain cryptographically identified review receipts. | `reviewComplete`, `verdict`, `blockingFindingCount`, and review hash in closure records. | Planned |
| VR-09 | Current provider configuration remains operational after renaming. | NVIDIA NIM values remain repository configuration only, never generic implementation vocabulary. | Planned |

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

Deferred. No implementation is claimed by this CR until a queue boundary is
reached and the full forward/reverse review and hosted evidence cycle is run.
