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

# CR-120: Minimal Review Material Contract

Current-state note (2026-09-14): The material rule remains in force, but the
implementation named below was retired during the review-gate v2 switch.
`build/Run-CodeReview.ps1` and `build/Invoke-CodeReviewHarness.ps1` now build
the committed, candidate-bound packet. See
`docs/FRESH-PROJECT-CODE-REVIEW-GATE.md` for the active contract. The original
closure evidence below is retained as historical record.

## Purpose

Make every project code-review dispatch permanently minimal and requirement-
scoped. A code review receives one minimal but complete requirement, only the
directly related code, and a zero-context diff when prior code exists. A
document review receives only the document purpose, purpose-relevant snippets,
and a zero-context diff when prior relevant sections exist.

## Enforcement

`build/Run-CodeReview.ps1` requires `-Requirement` or `-RequirementPath` and a
scope, rejects dirty or unrelated CODE candidates, and emits only the
candidate-bound material with zero-context diffs. The provider-neutral review
contract applies the same material rule to document and test-artifact reviews.

## Acceptance

- The packet builder rejects omitted requirements, omitted relevant paths, and
  unrelated changed files.
- The packet contains the requirement, directly related files, relevant
  snippets, and zero-context diffs only.
- Harness self-tests, syntax, license, review, receipt, and protected-push
  gates pass for the implementation.
- Hosted run `34329193881` passed all 43 jobs across 20 lanes with
  `HOSTED_SOAK_CLOSURE=Passed` and `REMOTE_REVIEW_CALLS=0`.

## Closure

CR-120 is closed. The permanent material rule remains enforced for every
project code-review gate.
