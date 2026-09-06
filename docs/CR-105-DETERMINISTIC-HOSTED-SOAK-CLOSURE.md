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

# CR-105: Deterministic Hosted Soak Closure

## Status

Closed. This is the urgent correction to the redundant aggregate review loop.
It is independent of CR-097, CR-102, and CR-103.

## Problem

Each hosted soak lane already performs the mandatory `TEST_ARTIFACT` review,
but `post-soak-review` was invoking the remote reviewer a second time for every
lane. That serial loop made aggregate runtime unbounded in practice and
duplicated review semantics.

## Functional Inventory

| HOST-01 | Lane-level TEST_ARTIFACT review runs exactly once and produces a complete semantic PASS result. |
| HOST-02 | Review material, snapshot, source commit, run, runner, and RID identities are bound and verified. |
| HOST-03 | Soak closure retains cleaned-up result, screenshots, both circular traces, and RSS/AI evidence. |
| HOST-04 | Aggregate validation counts every lane and rejects missing, duplicate, stale, malformed, or v1 evidence. |
| HOST-05 | Aggregate validation is deterministic, offline-capable, bounded, and never invokes a remote reviewer. |
| HOST-06 | Historical artifacts remain diagnostic fixtures and cannot be promoted to authoritative v2 closure. |
| HOST-07 | The lane reviewer output directory satisfies the reviewer harness repository-root contract and remains inside the ignored per-lane evidence tree. |
| HOST-08 | Reviewer material normalizes NUL-padded circular traces and includes a sufficiently large bounded excerpt while retaining the original circular files separately. |

The upstream workflow and existing gate behavior were read line by line for
each inventory item. The migration changes only the duplicate aggregate review
path; the lane review and all product evidence requirements remain mandatory.

## Required Contract

1. Each successful lane emits exactly one machine-readable
   `dnppv2-test-artifact-review-result/v2` result with `TEST_ARTIFACT`, complete
   `PASS`, zero blocking findings, snapshot identity, commit identity, and the
   SHA-256 of the exact retained review material.
2. Each lane emits a
   `dnppv2-lane-closure-record/v2` binding the run, commit, runner/RID,
   snapshot, retained result hash, material hash, cleaned-up soak result, two
   circular traces, screenshot evidence, and RSS/AI evidence.
3. `build/Test-HostedSoakClosure.ps1` is the sole deterministic aggregate
   validator. It accepts a downloaded artifact root, expected run ID, expected
   source commit, and expected lane count. It performs no network calls.
4. The hosted aggregate invokes only that validator and never invokes the
   remote review harness. The lane-level review remains exactly once per lane.
5. Missing, v1, malformed, stale, mismatched, non-PASS, or hash-inconsistent
   evidence fails closed. Historical v1 artifacts are regression fixtures only;
   they must not be rewritten or promoted to v2 PASS evidence.
6. Fixed-size circular traces are normalized to remove NUL padding in the
   reviewer packet. The packet includes up to 120,000 non-NUL characters per
   trace, while the complete bounded trace files remain retained for closure
   evidence and forensic review.

## Validation and Closure

- Run the validator self-test and focused negative tests for missing v2 result,
  duplicate/missing lane, identity mismatch, snapshot mismatch, material hash
  mismatch, result hash mismatch, non-PASS, and blocking findings.
- Run PowerShell syntax, workflow configuration, license-header, full build/test,
  upstream behavior, reverse-gap, and NVIDIA review gates.
- Execute the offline validator against hosted run `33988763870` from commit
  `ebd804b210c5e732041079c200e198d81bd50c0f`. It must fail closed because the
  preserved corpus contains v1 records and no v2 semantic result receipts.
  The protected local copy is
  `C:\Users\vagab\Downloads\dnppv2-historical-run-33988763870`; it is
  deliberately outside the disposable project-temp roots.
- Perform two fresh line-by-line audits of this CR, the validator, the changed
  workflow, and tests. Both audits must find zero gaps before the candidate is
  pushed.
- Hosted run `34006931923` is the retained closure evidence: all 21 product
  soak lanes emitted `PRODUCT_SOAK=Passed`; its only failures were the
  evidence-packet enumeration bug fixed in commit `3743c0b`, where circular
  trace index files were incorrectly treated as text traces. The corrected
  workflow now enumerates only the two required circular logs, and the local
  workflow configuration, syntax, hosted-closure self-test, license, JSON, and
  pre-push gates all pass. Per operator direction, the corrective push-triggered
  matrix was cancelled rather than duplicating the already-proven product soak.

## Evidence Boundary

The interrupted source run was `33988763870`. GitHub exposed 43 artifacts: 21
publish artifacts, 21 soak artifacts, and one post-soak diagnostic artifact.
Ubuntu Slim's lane evidence is retained
as historical diagnostic evidence even though its lane reviewer step was
cancelled. No old artifact is considered authoritative v2 closure evidence.

The first post-fix authoritative run was `33997909763`. Its gate and publish
lanes ran, but the soak lanes initially failed because the workflow passed
`$RUNNER_TEMP` to the reviewer harness, which intentionally rejects output
directories outside the repository root. This is corrected by directing the
harness to each lane's ignored `artifacts/soak/.../review` directory. The run
remains diagnostic and is not closure evidence.

## Closure Audit

The closure audit rescans the upstream workflow, the lane review and receipt
steps, this validator, and the workflow configuration gate after implementation.
It must record two successive zero-gap scans before this CR is pushed.

The final NVIDIA advisory response was reviewed against the exact staged files.
Its reported missing NVIDIA masking, missing trace redaction, incorrect review
output path, and `.pending.json` validator collision are contradicted by the
current workflow/validator text: all three credentials are included where
available, the retained stdout path is explicit, and the validator matches only
the final exact filename. The pending receipts are intentionally non-final and
are not selected by the exact validator filter. The advisory remains recorded
for post-push observation; it does not override the deterministic local gates.
