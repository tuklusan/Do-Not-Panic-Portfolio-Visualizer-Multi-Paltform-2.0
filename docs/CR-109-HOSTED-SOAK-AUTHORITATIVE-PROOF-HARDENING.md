<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.
Based on original work by Supratim Sanyal of SANYALnet Labs.
DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
-->

# CR-109: Hosted Soak Authoritative-Proof Hardening

## Status

Open. High-priority follow-up to closed CR-105; implementation is deliberately
deferred until the queue reaches this dependency.

## Dependency and Scope

Depends on CR-105. This CR does not redesign CR-105's one-reviewer-per-lane,
v2 receipt, deterministic aggregate, fail-closed, or two-audit architecture.
It closes the remaining trust-boundary gaps around that architecture.

## Functional Inventory

| ID | Functional behavior or proof obligation |
| --- | --- |
| SNAP-01 | Reconstruct the expected snapshot as the canonical expected commit SHA plus the actual SHA-256 of `evidence-review-input.txt`; never trust agreement between retained fields alone. |
| SNAP-02 | Enforce that equation independently in lane finalization and in `Test-HostedSoakClosure.ps1`, including nested review snapshot data. |
| SNAP-03 | Reject wrong-commit, wrong-material, missing, stale, cross-lane, and mutually-consistent-but-fabricated snapshot receipts. |
| SEC-01 | Detect known secrets, bearer tokens, and credential-shaped values across the entire reviewer subtree, including future text-bearing files. |
| SEC-02 | Move contaminated files or the whole reviewer subtree outside the upload root before failure propagation; retain only sanitized metadata. |
| SEC-03 | Prove quarantined bytes cannot be uploaded by an unconditional artifact-upload step and never appear in receipts or diagnostics. |
| PRIV-01 | Keep the deterministic `post-soak-review` job free of all live provider credentials, masks, and authenticated network operations. |
| PRIV-02 | Preserve credentials only in the lane product/reviewer steps that genuinely require them, with immediate masking and fail-closed scanning. |
| PROOF-01 | Run one complete serialized final hosted matrix against the exact candidate, with every expected publish and real-product-soak lane complete. |
| PROOF-02 | Retain and inspect every lane's screenshots, both circular traces, RSS/AI evidence, v2 review result, closure receipt, and aggregate result. |
| PROOF-03 | Record the exact candidate commit, run ID, lane count, aggregate result, snapshot result, security result, credential-free aggregate result, and zero aggregate reviewer-call count. |

The upstream behavior and current CR-105 workflow/validator are the sources of
truth. Installer-only, WPF-only, and historical artifacts remain out of scope.

## Required Implementation

1. Recompute and canonicalize the manifest hash, construct
   `<ExpectedCommitSha>:<actualManifestSha256>`, and require exact equality at
   both lane and aggregate closure gates.
2. Add the five snapshot-binding negative cases: wrong pair, wrong material
   hash, wrong commit, result/closure disagreement, and matching-but-wrong
   retained values.
3. Quarantine contaminated reviewer files/subtrees under `RUNNER_TEMP`, verify
   they no longer exist below the upload root, and write only a sanitized
   attributable failure record with `status != complete`.
4. Add actual-file quarantine tests using synthetic secrets, bearer syntax, and
   nested telemetry; scan the complete post-failure upload root.
5. Remove provider credentials and aggregate-only masking from `post-soak-review`
   and add workflow-gate regression checks that distinguish lane credentials
   from aggregate configuration.
6. Keep exactly one lane-level `TEST_ARTIFACT` invocation and no aggregate
   remote reviewer invocation. Bound the deterministic aggregate timeout to a
  documented defensive limit (60 minutes for 21-lane artifact download and
  hashing) and remove the
   blind artifact-publication sleep unless a bounded race workaround is proven.
7. Expand `Test-HostedSoakClosure.ps1 -SelfTest` for v1 rejection, malformed and
   non-PASS receipts, all snapshot cases, hash/identity failures, duplicates,
   missing lanes, and blocking findings.

## Acceptance Criteria

- All snapshot, quarantine, aggregate-privilege, reviewer-count, timeout, and
  v2-schema workflow gates pass.
- Synthetic secrets are detected, quarantined outside upload paths, absent from
  every retained artifact, and absent from aggregate output.
- Existing license, PowerShell syntax, upstream-mutation, migration behavior,
  reverse-gap, build/test, harness self-tests, and NVIDIA review gates pass.
- Two consecutive line-by-line closure audits from the committed disk copy find
  zero gaps.
- One complete serialized hosted matrix runs on the final candidate and exits
  successfully. Every expected lane produces authoritative v2 PASS evidence;
  the deterministic aggregate validates all lanes and makes zero remote review
  calls.
- The closure record names the exact implementation commit and authoritative
  run ID. Earlier diagnostic runs are never presented as final proof.

Latest proof attempt `34058732179` produced `Passed` product soak results and
`processCleanedUp=true` for all 21 lanes, but the aggregate remained a
non-closure result because several lane reviewer receipts were unavailable or
blocking and the AI-evidence findings were retained. The complete lane
evidence was inspected and downloaded artifacts were removed after inspection.

Hosted run `34061105976` likewise reached terminal failure. Twenty soak
manifests were inspected and each reported successful product execution,
cleanup, four circular trace files, and screenshot evidence; the `macos-26`
lane did not produce a soak manifest. The post-soak aggregate consequently
failed closed with `Expected 21 soak evidence manifests, found 20`, so no
authoritative closure receipt is claimed.

## Validation Plan

Perform the pre-development upstream forward inventory before editing. After
implementation, run focused negative tests, all repository gates, the NVIDIA
review gate, and the complete serialized hosted matrix. Inspect every lane's
retained evidence and perform the independent reverse scan twice. Any gap or
workflow change resets the two-pass closure sequence.
