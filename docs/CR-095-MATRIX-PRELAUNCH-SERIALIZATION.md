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

# CR-095: Serialize Hosted Matrix Launches

## Objective

Prevent a new hosted matrix from competing with an existing matrix whose
runner lanes are queued, running, or still publishing and reviewing evidence.

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| MG-01 | A complete publish/soak workflow run owns a single global concurrency group. | `dnppv2-complete-matrix` in `.github/workflows/publish-six-rids.yml`. | Implemented |
| MG-02 | A later push or manual dispatch waits instead of canceling the active run. | `cancel-in-progress: false`. | Implemented |
| MG-03 | The active run cannot finish until all lanes and aggregate evidence review finish. | Job dependencies and terminal lane evidence gates. | Implemented |
| MG-04 | The static workflow gate rejects removal or weakening of serialization. | `Test-WorkflowGateConfiguration.ps1`. | Implemented |

## Acceptance

The workflow gate passes with the global non-canceling concurrency group. A
second trigger is observed as pending until the first complete matrix reaches
terminal state; no overlapping matrix is admitted. The existing 21-lane
publish/soak and per-lane plus aggregate evidence requirements remain intact for
the current 20-lane matrix. The retired Ubuntu Slim lane is historical context
only and is not part of the current serialization count.
CR-095 cannot be closed, and its own matrix verification cannot be accepted,
until predecessor CR-094 is closed with its fresh 20-lane evidence and the
current run has reached terminal state.

## Upstream Behavior Inventory

The upstream workflow was read as the migration reference for trigger,
concurrency, matrix, artifact, and review behavior. This CR adds a migration
project safety gate around the complete run and does not alter product logic or
the upstream push lock.

## Reverse Upstream Gap Scan

The current workflow and gate script were rescanned after implementation. The
serialization requirement is explicit, and no upstream product behavior is
removed by this workflow-only change.

## Validation State

Static workflow, license, PowerShell syntax, migration, upstream-lock, and
full Release tests remain required before closure. Hosted verification must
wait for any active matrix to finish before another matrix
is launched.

## One-Time Slow-Lane Exception

On 2026-09-06, the operator authorized a one-time exception to the normal
90-minute observation threshold for the active serialized run `34040146263`.
The exception applies only to `real-product-soak (macos-14, osx-arm64)`,
job `101505317624`, which was still executing its reviewer-evidence step after
the threshold. It authorizes continued waiting beyond 90 minutes because the
review service may be slow; it does not authorize cancellation, a duplicate
matrix, acceptance of a non-terminal lane, or relaxation of any build, test,
manifest, screenshot, trace, cleanup, or reviewer-evidence requirement. The
exception was exercised by cancelling the run at operator request after the
reviewer wait became operationally excessive; the run therefore remains
incomplete and supplies no PASS evidence. The exception expires with that
terminal cancellation and must not be reused for a later run.
