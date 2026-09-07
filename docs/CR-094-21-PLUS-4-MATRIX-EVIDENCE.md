<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
-->

# CR-094 Hosted Matrix And Per-Lane Evidence

## Functional Inventory

| ID | Required behavior | Acceptance evidence |
| --- | --- | --- |
| MATRIX-01 | Publish the real Avalonia product on every configured hosted runner label, including `macos-latest` and `xcode-27`, with the correct self-contained RID. | A 20-entry publish matrix and one successful publish job per runner/RID pair. |
| MATRIX-02 | Run the real product soak on the same 20 runner/RID pairs; queued or slow lanes are waited on rather than duplicated or treated as failures. | A terminal workflow run with all 20 soak jobs accounted for. |
| MATRIX-03 | Require each lane to complete build and test before publication and soak execution. | Per-lane workflow step results and terminal job manifest. |
| MATRIX-04 | Require each lane to emit and validate `soak-result.json`, `news-evidence.json`, both bounded circular traces, and settled PNG evidence where the runner supports capture. | Per-lane validation output and file/hash inventory. |
| MATRIX-05 | Require NVIDIA review output to be non-empty and retain a redacted lane closure record containing hashes and inspection status before the lane can succeed. | `build/nvidia-review/.../lane-closure-record.json` uploaded with the lane artifact. |
| MATRIX-06 | Keep the post-soak aggregate review count synchronized with the 20 hosted lanes and inspect every retained lane record. | Push and dispatch aggregate review rejects anything other than 20 complete evidence manifests and 20 complete closure records. |
| MATRIX-07 | Keep the four local companion machines under the existing availability, storage, cleanup, and dual-trace contract. | Existing local-cycle evidence and current availability manifest. |

## Implementation

The publish and `real-product-soak` jobs share one 20-entry matrix. The two
additional macOS labels are `macos-latest` and `xcode-27` (`osx-arm64` for both).
`xcode-27` is retained as a required hosted label by project direction and must
not be silently substituted or removed. The former `ubuntu-slim` lane is retired
by CR-104 and is retained below only as historical diagnostic context.
After the per-lane NVIDIA review, the workflow runs an unconditional
success-path inspection step. That step validates the product result, cleanup,
screenshots, RSS/AI evidence, both circular traces, and non-empty reviewer
output, then writes the redacted `lane-closure-record.json` with SHA-256 hashes.
The lane cannot be successful if this record is not produced.

The aggregate `post-soak-review` job likewise requires 20 manifests and runs
for both push-triggered and explicit 10-minute dispatch soaks. Push-triggered
and dispatch lanes therefore receive both per-lane and aggregate inspection.

The complete matrix, authoritative in both jobs' `matrix.include` blocks, is:

| Runner | RID |
| --- | --- |
| `windows-latest` | `win-x64` |
| `windows-2025` | `win-x64` |
| `windows-2025-vs2026` | `win-x64` |
| `windows-2022` | `win-x64` |
| `windows-11-arm` | `win-arm64` |
| `windows-11-vs2026-arm` | `win-arm64` |
| `ubuntu-latest` | `linux-x64` |
| `ubuntu-24.04` | `linux-x64` |
| `ubuntu-22.04` | `linux-x64` |
| `ubuntu-26.04` | `linux-x64` |
| `ubuntu-24.04-arm` | `linux-arm64` |
| `ubuntu-22.04-arm` | `linux-arm64` |
| `ubuntu-26.04-arm` | `linux-arm64` |
| `macos-15-intel` | `osx-x64` |
| `macos-26-intel` | `osx-x64` |
| `macos-15` | `osx-arm64` |
| `macos-14` | `osx-arm64` |
| `macos-26` | `osx-arm64` |
| `macos-latest` | `osx-arm64` |
| `xcode-27` | `osx-arm64` |

## Closure Gates

Closure requires the pre-development and closure upstream gates, focused and
full Release tests, license and PowerShell syntax gates, workflow configuration
validation reporting 20 entries, NVIDIA source/evidence review, and a fresh
terminal matrix with every 20 hosted lanes accounted for. Local companion
machines remain dynamically probed and are recorded as unavailable when absent.

**Status:** Open

## Upstream Behavior Inventory

The matrix and evidence contract was checked against the pinned upstream
revision `65a53bbbf0cf9af1058363f8939d464ca03858f8`. The relevant source files
are recorded in `docs/AUDIT_STATE.json` under `upstream_behavior_inventory`;
the expansion changes validation coverage and does not remove product behavior.

## Reverse Upstream Gap Scan

Two successive reverse scans compared publication, real-product soak, review,
screenshot, trace, and cleanup behavior. No missing upstream behavior was
identified; the new runner lanes and closure record are validation coverage.

## Closure Audit

Closure requires a fresh pinned-upstream scan, two successive zero-gap results,
all 20 hosted lanes reaching terminal state, and inspection of every lane's
result manifest, screenshots, both circular traces, RSS/AI evidence, reviewer
output, and redacted closure record. The results must be correlated to this CR.

## Current Validation

The static workflow gates pass with 21 identical publish and soak entries. In
hosted run `34061105976`, 20 soak manifests were retrieved and inspected; every
retrieved product result passed, cleaned up its process, retained four circular
trace files, and recorded settled screenshot evidence. The `macos-26`
`osx-arm64` lane produced no soak manifest, and the aggregate therefore failed
closed with `Expected 21 soak evidence manifests, found 20`. This CR remains
open until every lane has a complete, reviewed evidence record.

Hosted run `34063195136` supplied all 21 soak manifests. Every product soak
reported `Passed`, `processCleanedUp=true`, four circular trace files, and
settled screenshot evidence. The aggregate still failed closed: only 20
semantic review results were present because `ubuntu-slim` had none, and 12
lane reviews contained blocking findings. The run is diagnostic evidence, not
closure proof.
