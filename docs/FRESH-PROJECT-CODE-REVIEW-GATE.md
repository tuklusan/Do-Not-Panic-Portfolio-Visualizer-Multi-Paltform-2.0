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

# Fresh Project Code Review Gate

This is the provider-neutral contract for independent review of code,
documentation, and retained test artifacts. `build/Run-CodeReview.ps1` is the
generic runner and `build/Invoke-CodeReviewHarness.ps1` is the generic adapter.
The configured engine is selected by `DNPPV_REVIEW_ENGINE`; the default engine
is the currently approved repository reviewer.

## Configuration

The configured engine receives the following neutral process configuration:

- `CODE_REVIEWER_ENDPOINT`
- `CODE_REVIEWER_MODEL`
- `CODE_REVIEWER_API_KEY`
- `CODE_REVIEWER_REQUEST_OVERRIDES_JSON` (optional JSON object)

Override JSON must be an object. Protected request fields (`model`, `messages`,
`response_format`, and `stream`) remain engine-controlled and cannot be null.
The adapter never logs, writes, or echoes the secret.

## Minimal review-material contract

Every review dispatch MUST contain one minimal but complete requirement, only
the directly related code or document snippets, and a zero-context diff when
prior content exists. For documents, the requirement is the document purpose
and the material is limited to purpose-relevant snippets and their prior-section
diff. Generic reviewer instructions do not expand the material scope. Review
callers MUST reject changed files that are not explicitly listed as directly
related. This contract is permanent and applies to every configured reviewer.

## Result contract

The engine must return one JSON result with:

```json
{"verdict":"PASS","review_complete":true,"blocking_findings":[]}
```

`verdict` must be exactly `PASS`, `FAIL`, `INCONCLUSIVE`, or
`REVIEW_UNAVAILABLE`. `review_complete` must be boolean and
`blocking_findings` must be a structured array. Missing, malformed,
unsupported, incomplete, unavailable, or contradictory results fail closed.
Only `PASS` with `review_complete=true` and zero blocking findings may permit a
caller to continue. Serious findings must include a concrete requirement,
location, problem, and evidence before they can be actionable.

## Caller and evidence rules

Every direct caller must consume the structured result rather than treating
non-empty output as success. Untracked source files, secret-like paths, and
all changed review material must be included. Hosted lane closure records must
retain the review completion state, verdict, blocking-finding count, and the
review identity/hash required by the applicable receipt contract.

The generic layer does not alter product behavior and does not unfreeze the
operational reviewer or soak harnesses. Changes to frozen harnesses require
the separate explicit operator-approval gate documented in
`docs/TEST_HARNESS_FREEZE.md`.
