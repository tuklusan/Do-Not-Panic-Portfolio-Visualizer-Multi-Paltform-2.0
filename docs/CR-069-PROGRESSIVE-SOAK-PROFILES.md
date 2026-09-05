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

## Functional Inventory

| CR-01 | Run the controlled 10-minute profile while open CRs remain | soak workflow and local coordinator | traces and result manifests |

While the migration CR queue contains open work, run exactly one real-product
profile on the 18-plus-4 matrix:

1. 10 minutes

Four-hour profiles are paused by project policy until the CR queue is closed.
No workflow input, local coordinator invocation, or scheduled job may start a
four-hour profile during this period.

Each 10-minute cycle starts only after the prior cycle's artifacts are reviewed
and the machine is clean. Failures create a JSON CR with the exact run identity,
both circular traces, screenshot/evidence manifest, and reviewed diagnosis.
After a fix, the affected 10-minute profile is relaunched. The queue remains
open until two successive accepted 10-minute cycles show no new actionable
defects across all 18 hosted runners and every local machine available at cycle
start.

Every hosted lane captures a settled product screenshot after the 30-second
warmup. The lane must retrieve both `trace.circular.*` and
`yfinance.circular.*` files, and must produce a SHA-256 manifest for each image.
Completed artifacts are reviewed visually and by the evidence gate.

At the start of each profile cycle, the harness rechecks all four local lab
machines and runs on the currently reachable contract-compliant subset. The
cycle manifest records every unavailable machine and its reason; availability
changes between cycles do not invalidate the evidence from a completed cycle.
If local machines are unavailable, the profile may still close when all 18
hosted lanes have provably executed and passed the real-product gates; local
availability is recorded, never silently converted into a pass.

For a manually dispatched 10-minute run, the `post-soak-review` workflow job
starts after the soak matrix reaches a terminal state, downloads that run's 18
evidence artifacts, checks both trace files and AI-path evidence, and sends each
complete evidence packet through the NVIDIA NIM artifact reviewer. Its uploaded
defect-candidate artifact is the handoff for the JSON CR loop; a failed soak or
missing evidence does not suppress this review job.

**Depends on:** CR-066, CR-067, CR-068  
**Status:** Open. Push-triggered run `33955248690` completed 35 jobs
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
