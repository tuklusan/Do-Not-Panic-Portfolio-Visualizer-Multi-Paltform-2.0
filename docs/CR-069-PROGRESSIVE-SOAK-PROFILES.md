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

# CR-069 Soak Profile During Open-CR Processing

> **Current matrix authority:** [CR-094](CR-094-21-PLUS-4-MATRIX-EVIDENCE.md)
> supersedes the earlier 18-plus-4 matrix wording in this profile. The 18-lane
> counts below are retained only as historical evidence for the closed baseline.

## Functional Inventory

| SOAK-01 | Run the controlled 10-minute profile while open CRs remain | `.github/workflows/publish-six-rids.yml` and `build/Invoke-LocalLabSoakCycle.ps1` | duration and cycle manifests |
| SOAK-02 | Exercise each configured hosted runner/RID pair as a real product lane | hosted `real-product-soak` matrix | runner/RID identity and lane result |
| SOAK-03 | Probe all local machines at the beginning of every cycle and record unavailable machines | local coordinator availability probe | availability manifest with reason |
| SOAK-04 | Wait for product startup to settle before collecting visual evidence | hosted warmup and local driver settling | settled screenshot evidence |
| SOAK-05 | Retrieve the product circular trace and YFinance circular trace for every completed lane | trace collection and recursive retrieval | `trace.circular.*` and `yfinance.circular.*` |
| SOAK-06 | Keep credentials out of product artifacts while enabling the real RSS and AI path | protected environment overlay and redaction | redacted result and evidence manifests |
| SOAK-07 | Review generated evidence and route actionable failures into the JSON queue | NVIDIA artifact review and post-soak review | review output and CR linkage |
| SOAK-08 | Terminate product, sidecar, capture, and transfer processes after each lane | `finally` cleanup paths and local cleanup | cleanup proof and process inventory |
| SOAK-09 | Prevent overlapping cycles and keep four-hour profiles paused while migration CRs remain open | concurrency groups and duration policy | workflow state and dispatch inputs |
| SOAK-10 | Repeat the affected duration and shorter profiles after every correction | CR queue and relaunch procedure | rerun identity and closure evidence |

The upstream validation and runner patterns were read from the pinned
upstream revision `65a53bbbf0cf9af1058363f8939d464ca03858f8`, including its
validation scripts, trace collection, cleanup paths, and the Ludo-Arena
matrix/desktop execution patterns used as the migration reference. The
current 2.0 counterparts are listed above; no upstream soak behavior is left
unmapped for this CR. Product-specific defects found during execution remain
separate implementation CRs rather than being hidden in this operational CR.

While the migration CR queue contains open work, run exactly one real-product
profile on the 21-plus-4 matrix:

1. 10 minutes

Four-hour profiles are paused by project policy until the CR queue is closed.
No workflow input, local coordinator invocation, or scheduled job may start a
four-hour profile during this period.

Each 10-minute cycle starts only after the prior cycle's artifacts are reviewed
and the machine is clean. Failures create a JSON CR with the exact run identity,
both circular traces, screenshot/evidence manifest, and reviewed diagnosis.
After a fix, the affected 10-minute profile is relaunched. The queue remains
open until two successive accepted 10-minute cycles show no new actionable
defects across all 21 hosted runners and every local machine available at cycle
start.

Every hosted lane captures a settled product screenshot after the 30-second
warmup. The lane must retrieve both `trace.circular.*` and
`yfinance.circular.*` files, and must produce a SHA-256 manifest for each image.
Completed artifacts are reviewed visually and by the evidence gate.

At the start of each profile cycle, the harness rechecks all four local lab
machines and runs on the currently reachable contract-compliant subset. The
cycle manifest records every unavailable machine and its reason; availability
changes between cycles do not invalidate the evidence from a completed cycle.
If local machines are unavailable, the profile may still close when all 21
hosted lanes have provably executed and passed the real-product gates; local
availability is recorded, never silently converted into a pass.

For a manually dispatched 10-minute run, the `post-soak-review` workflow job
starts after the soak matrix reaches a terminal state, downloads that run's 21
evidence artifacts, checks both trace files and AI-path evidence, and sends each
complete evidence packet through the NVIDIA NIM artifact reviewer. Its uploaded
defect-candidate artifact is the handoff for the JSON CR loop; a failed soak or
missing evidence does not suppress this review job.

**Depends on:** CR-066, CR-067, CR-068  
**Status:** Closed. Push-triggered run `33955248690` completed 35 jobs
successfully, while the real-product soak exposed two actionable failures:
`macos-26` arm64 had a cross-platform timing race in
`ProgressiveQuoteRefreshPipelineTests`, and `ubuntu-22.04-arm` completed its
soak but lacked the required AI-success trace event. The test race is now
bounded more generously for slow arm64 scheduling; the Ubuntu AI evidence
failure remains routed to the existing AI/soak CRs and requires a rerun after
the correction. This run does not satisfy the two-clean-cycle closure gate.

Replacement run `33956511431` completed successfully with all 18 real-product
soak lanes, all per-lane evidence reviews, and 18 non-expired soak artifacts;
the corrected arm64 test and the Ubuntu arm64 AI evidence both passed in that
cycle. This is one accepted clean hosted cycle; a second independent clean
cycle and the available-local-machine companion evidence are still required.

Second independent run `33957825572` also completed successfully with all 18
real-product soak lanes, all per-lane evidence reviews, and 18 non-expired
soak artifacts. The hosted two-cycle requirement is therefore satisfied;
available-local-machine companion evidence and the broader harness CR closure
gates remain open.

The corrected local cycle `dnppv2-local-cycle-10m-20260905-094111` completed
all four machines successfully. Linux, Windows 10, Windows 11, and Intel Mac
each produced real-product evidence with RSS usable and AI request/success
events, both circular trace families, and cleanup. This supplies the required
available-local-machine companion evidence alongside hosted cycles
`33956511431` and `33957825572`.

Closure review rescanned the workflow, local coordinator, soak runner contract,
visual-validation scripts, and validation tests twice with zero unmapped
behaviors. The NVIDIA artifact-review packet was generated, the local
Release/build/license/syntax/workflow gates passed, and no actionable defect
remains for this operational CR.
