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

# NVIDIA review gate (v2)

This is the current operational policy for the DNPPV-2.0 external reviewer.
`docs/FRESH-PROJECT-CODE-REVIEW-GATE.md` defines the public packet/result
contract. The fixed call graph is:

`Run-CodeReview.ps1` → `Invoke-CodeReviewHarness.ps1` →
`Invoke-ReviewGate.ps1` → `Invoke-NvidiaReviewHarness.ps1` →
`https://integrate.api.nvidia.com/v1/chat/completions`.

The generic runner and orchestrator contain no provider/model selection. The
policy selects primary `nvidia/nemotron-3-super-120b-a12b` and the sole
fallback `nvidia/nemotron-3.5-lightning-30b-a3b`. The fallback is permitted
only after a verified model/provider availability class, never to seek a
different verdict or to recover from local, packet, parser, schema, process,
or transport ambiguity. A clean review uses one semantic stage and one POST.
Serious findings may trigger one adjudication stage on the same selected
model. Reasoning is enabled for review stages; health uses a minimal prompt.

## Packets and evidence

CODE packets bind committed base/head trees, every directly changed path,
complete directly related snippets, and an accurate zero-context diff. The
caller supplies one minimal but complete requirement; optional unchanged
context is explicit and bounded. DOCUMENTATION and TEST_ARTIFACT packets bind
the exact retained purpose-relevant material to the current head commit.
Paths, bytes, SHA-256 values, scope, and requirement are canonicalized before
dispatch. A changed candidate, untracked source, secret-like path, missing
requirement, or packet mismatch fails before provider dispatch.

Each dispatch has an exclusive lease and a durable transaction. Request body,
HTTP status, safe response envelope, exact final content, and correlation
identity are retained without hidden reasoning or credentials. A process
restart resumes a confirmed 202 request through the provider status endpoint;
ambiguous POST dispatch is never retried as model unavailability. Explicit
HTTP responses have only the bounded status-specific retry policy in the
adapter. The entire operation obeys one deadline.

## Malformed responses and verdicts

An `OutputError`, schema error, or malformed final response triggers a
packet/request/response audit first. The audit verifies candidate identity,
packet hash/schema, outgoing request contract and size, secret scan, provider
envelope, terminal finish, and parser replay. It is retained beside the
transaction and is not a reviewer finding. Only an evidence-authorized,
bounded same-model protocol replay may follow; no blind resubmission or
fallback occurs.
Malformed adjudication is audited and ends `INCONCLUSIVE`. A second malformed
response from the one permitted same-model replay retains both audit records
and ends `INCONCLUSIVE`; no further automatic POST is allowed.

The policy returns only `PASS`, `FAIL`, or `INCONCLUSIVE` in a
`dnppv2-review-result/v3` object. A clean PASS requires complete review,
zero blocking findings, and zero uncertainties. The public runner writes
that single object to stdout. Infrastructure failure writes a typed JSON
error to stderr and exits nonzero. Callers must check both the process exit
and exact candidate/result identity. CODE receipts bind the approved result
to the exact base/new commit pair; the pre-push hook validates the remote old
SHA, receipt, license headers, syntax, workflow configuration, harness freeze,
and upstream push lock.

## Health and verification

`Test-NvidiaWorkflowGate.ps1` delegates to the orchestrator's `Health`
operation and the same NVIDIA adapter. It has no independent HTTP client or
retry ladder. The default probe targets the primary model; `-ModelRole
Fallback` targets the fallback. Each probe is bounded and must return one
valid terminal health result.

Deterministic fixtures and stub transports are required before any live
NVIDIA call. The certified review-gate acceptance suite, two consecutive
complete zero-defect on-disk audits with no edits between them, and all
freeze/license gates are required before activation or push. During the
authorized infrastructure repair, the defective gate does not review its own
change. Phase 1 forbids external AI/NVIDIA review calls; no live probe is
implicitly authorized by this document. After Phase 1, only separately
permitted bounded health probes or a small fixture review may be sent to
NVIDIA. Product matrix and physical-lab soaks are not substitutes for
review-gate acceptance.
