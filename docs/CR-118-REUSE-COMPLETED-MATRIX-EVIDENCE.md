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

# CR-118: Reuse Completed Matrix Evidence for Instrumentation Corrections

**Status:** Implemented; closure evidence pending

**Priority:** Low infrastructure detour

**Harness-change authorization:** The operator explicitly authorized this
specific workflow correction in the request that introduced this CR. The
authorization covers the hosted workflow and its configuration gate only; it
does not unfreeze or authorize optimization of any local or hosted soak
harness.

## Objective

When a completed hosted matrix is known to have passed product execution and
only its post-matrix instrumentation, evidence extraction, or reviewer
packaging requires correction, reprocess the nominated completed run instead of
launching an identical matrix again.

## Enforced workflow contract

The publish workflow provides two explicit modes:

- `fresh-matrix` runs the normal serialized publish and real-product soak jobs.
- `reuse-completed-run` requires a numeric completed run ID and its 40-character
  commit SHA, skips publish/soak/local dispatch, downloads only that run's soak
  artifacts, and validates them against the nominated identities.

`build/Test-MatrixEvidenceReusePolicy.ps1` rejects missing or malformed reuse
identities and rejects prior-run identities in fresh mode. The workflow gate
requires this policy and the post-soak validator uses the selected run ID and
commit SHA. This prevents instrumentation-only corrections from silently
starting a new matrix.

## Operating rule

Use reuse mode only when the completed run is the authoritative run for the
unchanged product candidate and the correction is limited to post-run
instrumentation/evidence processing. Any product, dependency, runner, or soak
behavior change requires a fresh serialized matrix. A reused run never creates
new product execution evidence; it only reprocesses retained evidence.

## Validation

The policy self-test, workflow gate, PowerShell syntax gate, and license gate
must pass. Closure additionally requires one controlled reuse-mode workflow
execution against a completed run with all lane manifests and evidence
validated, without any publish or soak job admitted.
