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

# CR-103: Immutable Committed-Candidate Review Receipt and Pre-Push Enforcement

**Priority:** Low

**Status:** Deferred / blocked

**Phase:** Phase 7 / workflow maintenance

**Depends on:** CR-097

## Constraint

Do not begin implementation until CR-097, the vendor-neutral independent code-review gate, is complete and verified. CR-097 defines the generic reviewer names, result schema, output locations, verdict semantics, serious-finding validation, and fail-closed caller contract required by this CR.

This is migration-process infrastructure only. It must not change DNPPV 1.0 behavior, reintroduce provider-specific reviewer vocabulary, weaken hosted validation, or retain unnecessary historical artifacts.

## Objective

Replace mutable-working-tree review authorization with a deterministic committed-candidate model. A CODE PASS must authorize exactly one immutable Git candidate and one reviewed remote-base-to-candidate range.

Create an ignored, atomic PASS receipt bound to the exact base SHA, candidate SHA and trees, deterministic Git-object snapshot, CR/task scope and hash, review packet hash, and structured reviewer-result hash. Only `review_complete=true`, exact `PASS`, and zero blocking findings may create a receipt. `FAIL`, `INCONCLUSIVE`, `REVIEW_UNAVAILABLE`, malformed, missing, stale, contradictory, or incomplete evidence must produce no authorization.

## Required implementation

1. Build one shared immutable snapshot-descriptor implementation from Git objects. It must use deterministic field ordering, LF UTF-8 serialization, no timestamps or machine paths, encoded unusual paths, and an ordinally sorted changed-file manifest. Hash the canonical descriptor with SHA-256 and hash the exact review material and result.
2. Make committed-candidate review fail closed unless the repository, commit, suitable branch, configured upstream, clean tree, absence of non-ignored untracked files, ancestor base, distinct head, and CR/task scope are valid. Revalidate HEAD, base, tree, clean status, descriptor, and snapshot after review and before atomic receipt creation.
3. Store receipts under ignored `build/code-review/receipts/<HEAD-SHA>.json` using schema `dnppv2-code-review-receipt/v1`; distinguish cryptographic binding from cryptographic authentication. Cleanup must not delete an active unpushed receipt or recreate one without review.
4. Add `Assert-CodeReviewReceipt.ps1` as an independently testable validator. For protected existing refs, require exact receipt-base equals Git pre-push remote-old SHA, exact head equality, matching trees and snapshot, valid scope/hash, ancestor and fast-forward checks, and all required receipt fields. Protected-ref configuration must initially include `refs/heads/main`, support extensible patterns, and fail closed for protected deletions and unapproved new protected refs.
5. Update `.githooks/pre-push.ps1` and its shell wrapper to consume every Git stdin update tuple, preserve arguments and exit status, validate every protected update in multi-ref pushes, reject the entire push if one protected update fails, preserve upstream-mutation/license/syntax/workflow gates, and verify repository-local `core.hooksPath` activation. Hook bypass is a documented workflow violation, not a hostile-user security boundary.
6. Extend the generic post-CR-097 reviewer runner and harness with separated responsibilities: the runner creates the immutable packet, validates the result, and creates the receipt; the harness performs independent review; the receipt validator checks Git objects and push tuples; the hook enforces authorization. Mutable diagnostic review may remain only if it can never create a receipt.
7. Update durable instructions and the generic review-gate standard with the canonical sequence, snapshot and receipt schemas, exact base/head and multi-ref rules, stale-receipt behavior, hook configuration, cleanup lifetime, failure states, bypass limitation, and vendor-neutral naming. Extend deterministic workflow gates to check the critical enforcement combination rather than a single token.

## Required tests and closure

Add focused validator and hook tests for exact valid receipts, missing/malformed/unsupported receipts, invalid hashes, every non-PASS reviewer state, wrong base/head/tree/scope/snapshot, non-fast-forward and stale local/remote candidates, dirty/untracked worktrees, reviewer races, multi-ref protected/unprotected combinations, stdin/argument/exit-code preservation, and inactive hook paths.

Add a real temporary-Git integration test with commits `A -> B -> C -> D` proving exact base/head acceptance, remote movement rejection, new-head rejection, and rejection of a skipped reviewed range even when ancestry holds. Do not rely only on mocked strings.

Before closure, run the reviewer/hook tests, real-Git tests, PowerShell syntax, license, workflow, upstream-mutation, applicable migration gates, focused/full Release tests, independent review of the exact committed candidate, receipt generation, normal protected push, hosted validation, evidence inspection, and two successive fresh reverse zero-gap scans. Closure requires CR-097 complete, all deterministic and temporary-Git tests passing, a fresh reviewer PASS receipt, ordinary protected push acceptance, stale/mismatch rejection, hosted success, and two zero-gap scans.

## Explicit non-goals

No server-side hostile-user enforcement, external signing authority, general branch-deletion authorization, implicit bootstrap base, replacement of GitHub branch protection, weakened matrix/evidence requirements, application behavior changes, provider-specific vocabulary, or historical traceback archive.

