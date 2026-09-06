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

# CR-113: Retry Empty NIM Reviewer Output

## Status

Open. Discovered while inspecting hosted run `34032694317`.

## Objective

Ensure the NVIDIA review harness treats absent, empty, or incomplete model
content as a transient reviewer transport/output condition and consumes the
existing bounded exponential retry budget before the lane is quarantined and
fails closed. A missing final reviewer result must never be converted into a
PASS receipt.

## Functional Inventory

| ID | Requirement | Proof |
| --- | --- | --- |
| NIM-01 | Absent, empty, and truncated model content use the existing bounded retry path. | Harness self-test and source inspection. |
| NIM-02 | Retry delays remain bounded and exponential; no unbounded polling or duplicate product calls are introduced. | Retry implementation and self-test. |
| NIM-03 | Exhausted reviewer retries still fail closed, quarantine unsafe output, and emit no semantic PASS receipt. | Hosted workflow inspection and negative test. |
| NIM-04 | Valid PASS/FAIL reviewer JSON behavior remains unchanged. | Harness self-test and existing review-gate tests. |
| NIM-05 | The change does not alter the product RSS/AI cadence; reviewer retries are outside the product process. | Source-cited upstream/reverse scan. |

## Evidence

The affected implementation is `build/Invoke-NvidiaReviewHarness.ps1`, in
`Test-IsRetryableHarnessFailureMessage` and `Invoke-NvidiaJsonRequest`.
The hosted caller is `.github/workflows/publish-six-rids.yml`, which invokes
the harness once per lane and fails closed when the semantic v2 receipt is
missing or incomplete. Run `34032694317` showed several lanes with missing or
empty reviewer output and no semantic PASS receipt; those lanes correctly
failed closure. The defect is that the harness did not retry the specific
empty-content message even though the model is known to return it transiently.

## Closure Gates

Run the CR pre-development and closure migration gates against the pinned
upstream commit, the harness self-test, PowerShell syntax and license gates,
the full Release build/test suite, the mandatory NVIDIA source/evidence review,
and one fresh serialized hosted matrix. Inspect every lane's reviewer output,
semantic receipt, screenshots, both circular traces, RSS/AI evidence, and
closure record. Close only when exhausted empty-output retries remain fail
closed and a complete matrix has authoritative evidence.

## NVIDIA Review Disposition

The mandatory NVIDIA reviews were run against the project-only candidate after
temporarily excluding the separately staged third-party reference corpus from
the packet-size-limited code snapshot. The reviews identified and led to the
following corrections: explicit retry-policy constants, bounded retry-index
handling, anchored-but-context-tolerant transient-output matching, property
missing self-tests, production-path sanitization checks, explicit 404/429
contract coverage, and retry telemetry.

The review request to remove HTTP 404 from the transient set is rejected as a
project-contract false positive: this migration explicitly requires 404 to be
handled like 429 for the NIM route. The request for a complete unchanged helper
definition in a diff is a scope false positive because
`build/NvidiaWorkflowCommon.ps1` is the loaded, separately gated common module;
its redaction implementation is now exercised by the harness self-test. A
full live HTTP mock is deferred as non-blocking reviewer advice; the production
decision helper, single-source boundary, sanitization, 404/429 contract, retry
telemetry, and fail-closed behavior are covered locally. The reviewer request
to make the policy externally configurable is also non-blocking: the project
requires one deterministic bounded policy for all reviewer environments.
