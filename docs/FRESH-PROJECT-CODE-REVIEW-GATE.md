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

This is the review contract for code, documentation, and retained test
artifacts. `build/Run-CodeReview.ps1` is the stable public runner.
`build/Invoke-CodeReviewHarness.ps1` constructs and binds the packet, then
calls the fixed policy in `build/Invoke-ReviewGate.ps1`, which calls
`build/Invoke-NvidiaReviewHarness.ps1`. Production callers cannot select an
arbitrary engine, model, endpoint, client, or API key through test overrides.

## Configuration

The approved NVIDIA endpoint and primary/fallback models are fixed in the
adapter and policy. `NvidiaWorkflowCommon.ps1` resolves the credential from
`NVIDIA_API_KEY_CODING` or the ignored local test-secret overlay; it must
never appear in packet, result, or retained telemetry.
`Test-NvidiaWorkflowGate.ps1` delegates a bounded health probe to the same
adapter, without a separate HTTP or retry implementation.

## Minimal review-material contract

Every review dispatch MUST contain one minimal but complete requirement, only
the directly related code or document snippets, and a zero-context diff when
prior content exists. For documents, the requirement is the document purpose
and the material is limited to purpose-relevant snippets and their prior-section
diff. Generic reviewer instructions do not expand the material scope. Review
callers MUST present a committed candidate whose entire changed-path set is
directly related to that requirement. The CODE packet derives every changed
path from Git; callers cannot omit an unrelated changed file from the packet.
This contract is permanent and applies to every review dispatch.

## Result contract

The public runner emits exactly one JSON result on stdout and no progress
text. Infrastructure failure emits a typed JSON error on stderr with a
nonzero exit code. Semantic `INCONCLUSIVE` is a valid result and exits zero.
Failure classes distinguish local path/packet/persistence faults and busy
leases from provider authentication, request-contract, rate-limit, backend,
transport, protocol, deadline, and ambiguous-dispatch faults. Only verified
model/backend unavailability can select the fixed fallback; malformed model
content triggers a retained packet audit, not an availability diagnosis.
The result schema is `dnppv2-review-result/v3`, including candidate identity,
packet and snapshot SHA-256, model/stage counts, verdict, completion,
findings, and uncertainties. For example:

```json
{"schema":"dnppv2-review-result/v3","verdict":"PASS","review_complete":true,"blocking_findings":[],"uncertainties":[]}
```

`verdict` must be exactly `PASS`, `FAIL`, or `INCONCLUSIVE`.
`review_complete` must be boolean; `blocking_findings` and `uncertainties`
must be structured arrays. Missing, malformed, unsupported, incomplete, or
contradictory results fail closed. Provider availability failures are typed
infrastructure errors, not reviewer findings. Only `PASS` with
`review_complete=true` and zero blocking findings and uncertainties may permit a
caller to continue. Serious findings must include a concrete requirement,
location, problem, and evidence before they can be actionable.

## Caller and evidence rules

Every direct caller must check the exit code, parse only stdout as the
structured result, retain stderr separately, validate candidate identity,
and require a clean PASS. CODE packets use committed Git objects, only
directly changed files, and zero-context diffs; untracked source and secret
paths fail closed. Material packets bind exact retained snippets and purpose
to the head commit. Receipts bind CODE reviews to the exact base/new commit
pair and packet/result evidence. Hosted lane closure records retain the
review identity/hash required by their receipt contract.

Persistence is ordered: atomically prepare the packet and transaction, retain
the exact request before POST, retain response metadata and safe envelope/final
content before semantic parsing, then update transaction state and persist the
result. A protocol failure persists the packet audit before any authorized
same-model replay. Only a completed, clean candidate-bound PASS can create an
atomic ignored receipt; the hook independently rechecks its Git base/head,
snapshot, result, transaction, and retained evidence before a protected push.

An `OutputError`, schema failure, or malformed model response is a protocol
signal. The policy audits the exact outgoing packet, request, and retained
response before attribution; it never treats such a failure as a reviewer
finding or blindly resubmits. Any permitted protocol replay is bounded,
same-model, and evidence-linked.
Malformed adjudication is audited before an `INCONCLUSIVE` result. If the one
authorized same-model replay also has malformed final content, its audit is
retained separately from the first audit and the review ends `INCONCLUSIVE`
without a third dispatch.

The generic layer does not alter product behavior and does not unfreeze the
operational reviewer or soak harnesses. Changes to frozen harnesses require
the separate explicit operator-approval gate documented in
`docs/TEST_HARNESS_FREEZE.md`.
