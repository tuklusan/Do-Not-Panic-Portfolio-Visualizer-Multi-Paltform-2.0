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

# CR-102: Complete Vendor-Neutral Code-Review Gate Cleanup

**Priority:** Low

**Status:** Deferred

**Phase:** Phase 7 / workflow maintenance

## Purpose

Refactor the external code-review gate so that its mechanism is vendor-neutral
while retaining the currently configured reviewer as deployment configuration.
The active migration queue remains authoritative; this detour is scheduled only
between higher-priority product and validation CRs.

## Scope

- Rename reviewer scripts, shared helpers, documentation, and output roots to
  use `CodeReview` or `CodeReviewer` terminology.
- Replace provider-specific environment variables with
  `CODE_REVIEWER_API_KEY`, `CODE_REVIEWER_ENDPOINT`,
  `CODE_REVIEWER_MODEL`, and optional
  `CODE_REVIEWER_REQUEST_OVERRIDES_JSON` configuration.
- Keep provider-specific request fields in validated configuration overrides,
  with protected fields such as `model`, `messages`, `response_format`, and
  `stream` rejected when overridden.
- Remove provider names from generic reviewer identifiers, prompts, mutexes,
  paths, and gate semantics. Provider-specific deployment examples may remain
  only where explicitly identified as configuration examples.
- Preserve fail-closed review enforcement: only a complete `PASS` with zero
  blocking findings permits continuation; `FAIL`, `INCONCLUSIVE`, unavailable,
  malformed, empty, or incomplete responses block.
- Make every direct artifact-review caller enforce `review_complete`, `verdict`,
  and `blocking_findings`, and retain those fields in each closure receipt.
- Preserve automatic inclusion of untracked files and fail closed on
  secret-like untracked paths.

## Required upstream and reverse gates

Before implementation, inventory the current reviewer scripts, workflows, tests,
and gate documentation line by line. At closure, rescan the same upstream and
current artifacts independently and record every provider-neutral behavior,
including retry, timeout, redaction, secret handling, response parsing,
serious-finding validation, snapshot binding, and receipt enforcement.

## Acceptance criteria

1. The reviewer implementation and generic documentation contain no provider
   identifiers; deployment configuration remains explicit and auditable.
2. A configured reviewer can be changed by environment/configuration changes
   without source renames or provider-specific code edits.
3. Provider override JSON is validated, protected fields cannot be replaced, and
   malformed configuration fails closed.
4. PASS/FAIL/INCONCLUSIVE/unavailable, empty-content, malformed-response,
   retry, timeout, secret-redaction, untracked-file, and serious-finding cases
   are covered by focused tests.
5. All direct workflow callers and aggregate gates inspect the complete review
   receipt rather than merely checking that a log is non-empty.
6. Existing product CR processing, build/test, hosted matrix, local-lab,
   license, syntax, cleanup, and upstream behavior gates remain unchanged in
   strength.

## Validation and closure

- Run the pre-development and closure migration behavior gates.
- Run the focused reviewer tests and the full Release build/test suite.
- Run the repository license, PowerShell syntax, workflow, cleanup, and
  upstream-mutation gates.
- Execute the mandatory external review against the final candidate snapshot.
- Run one serialized hosted validation cycle and inspect all required receipts.
- Commit and push only after all gates pass; update `docs/AUDIT_STATE.json` with
  the exact evidence and close this CR only after the independent closure scan.

## Dependencies and scheduling

This CR supersedes the broad deferred intent in CR-097 only when execution is
started. It must not interrupt an active product-port, soak, or evidence-repair
CR. It may be started after the current queue reaches a quiet checkpoint and
the reviewer service is available for the required final gate.
