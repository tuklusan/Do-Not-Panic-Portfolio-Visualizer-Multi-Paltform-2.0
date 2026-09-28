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

# CR-092: Enforce local companion soaks for hosted matrices

## Current execution state

Local-machine soak execution is enabled again after the hosted-only checkpoint.
`build/Invoke-LocalLabSoakCycle.ps1` and the four-machine inventory remain
unchanged. Each cycle must probe all four machines and use every available
contract-compliant machine; unavailable machines remain explicitly recorded.

## Objective

Whenever a hosted GitHub runner matrix is dispatched, discover the four local
lab machines at the start of that cycle and run the same-duration real-product
soak on every machine that is available. Every local lane must first terminate
residual project-owned product processes and prove cleanup before launch.

## Functional Inventory

| ID | Required behavior | Evidence |
| --- | --- | --- |
| LS-01 | Probe all four lab machines before each matrix cycle and record available/unavailable state. | Availability manifest with timestamp and machine identity. |
| LS-02 | Terminate residual project-owned product and YFinance processes before launch; failure hard-stops that local lane. | Cleanup result and zero-residual verification. |
| LS-03 | Run the real product, not a fixture, for the exact hosted soak duration with identical RSS, AI, trace, screenshot, settling, and cleanup goals. | Shared soak-result schema and two circular traces. |
| LS-04 | Unavailable local machines are explicit skips; all 18 hosted lanes remain mandatory. | Aggregated result distinguishes skipped local lanes from failures. |
| LS-05 | Local and hosted evidence is reviewed together and defects become JSON CRs before the cycle can count. | Combined artifact-review manifest and queue update. |

## Required Gates

The workflow must use a shared profile and result schema for hosted and local
lanes. The preflight must not silently convert an available-machine failure
into a skip. A guaranteed finalizer must terminate residual processes and clean
disposable artifacts. Run migration, license, syntax, NVIDIA review, build,
artifact, and commit/push gates before closure.

## Acceptance

- Every hosted matrix dispatch creates one availability manifest and one local
  result for each machine available at preflight.
- Local lanes use the exact hosted duration and real-product acceptance rules.
- Two independent complete cycles pass all 18 hosted lanes and all available
  local lanes with no new defects.

## Current Validation State

The latest full four-machine execution reached the real product successfully on
Windows 10, Windows 11, and Intel macOS. Those lanes produced product-scene
screenshots, RSS/AI evidence, circular traces, and passed machine manifests.
The Linux coordinator stopped before writing a result manifest, so that cycle
is incomplete and does not qualify the four-machine acceptance or the harness
maintenance lock. CR-092 remains open until a fresh reviewed cycle proves all
four available machines launch, settle, validate, and clean up alongside the
hosted matrix.

## Harness maintenance lock

After two independent, reviewed 10-minute cycles record all four local machines
as available and passed, `build/Invoke-LocalLabSoakCycle.ps1` is a proven
working harness and is now locked against opportunistic optimization or cleanup
refactoring. Changes are allowed only for a concrete continuation defect or an
explicit project requirement, and each exception must include focused syntax,
test, and acceptance evidence in the same checkpoint. The lock is active after
cycles `dnppv2-local-cycle-10m-pushed-20260905-062216` and
`dnppv2-local-cycle-10m-fourth-20260905-074500` passed all four local lanes.

This lock applies to the local-lab harness only; it does not suppress product
defect work, evidence analysis, or required fixes for a demonstrable harness
failure.

The coordinator launches Windows child PowerShell processes with hidden windows
so local validation does not disturb the operator's desktop. Unix lanes remain
headless over SSH and do not require a visible terminal.

## Latest cycle evidence

Cycle `dnppv2-local-cycle-10m-pushed-20260905-062216` completed on the pushed
cleanup-hardening revision with all four machines available and passed:

- Linux/LXQt, Windows 10, Windows 11, and Intel macOS each launched the real
  product for the full 10-minute duration.
- Every lane produced product screenshots, fresh RSS evidence, successful AI
  evidence, and both circular traces.
- Windows cleanup used the native PowerShell finalizer and completed without
  residual product/coordinator processes; Mac cleanup completed within its
  slow-lane budget.

This is the first clean local companion cycle for the current harness revision.
One additional independent complete cycle is required before CR-092 closure.

The next independent cycle, `dnppv2-local-cycle-10m-second-20260905-064042`,
completed its available-machine lanes but did not qualify as a clean cycle:
Windows 10, Windows 11, and Intel macOS passed the full real-product
acceptance contract, while Linux failed preflight because the deployed
executable was missing at the expected remote path. A focused one-minute Linux
retry subsequently launched the real product and produced product screenshots,
fresh RSS/AI evidence, and both circular traces. This confirms a transient or
race-sensitive Linux deployment defect, which remains actionable under CR-080;
the retry is diagnostic evidence and does not substitute for the required
independent four-machine ten-minute cycle.

The following fresh cycle, `dnppv2-local-cycle-10m-third-20260905-071500`,
reproduced the same Linux preflight failure while the three other available
machines again passed their complete ten-minute real-product lanes. The
repeatable Linux deployment miss is now confirmed as CR-080 work, and CR-092
cannot count either of these cycles as its second clean cycle until that lane
is repaired and the complete local acceptance contract passes again.

Following the staged Linux deployment fix, cycle
`dnppv2-local-cycle-10m-fourth-20260905-074500` passed all four available local
machines for the complete ten-minute real-product contract. Every lane
produced product screenshots, fresh RSS/AI evidence, and both circular traces;
residual product and harness processes were cleaned up. Together with cycle
`dnppv2-local-cycle-10m-pushed-20260905-062216`, this supplies the two required
independent clean local companion cycles. CR-092 is closed after the hosted
matrix evidence, upstream closure audit, and repository gates passed.

Cycle `dnppv2-local-companion-cycle-r9` completed its coordinator run after the
startup-settling bound was increased. Linux and Windows 10 completed their
10-minute real-product lanes successfully. Windows 11 reached the driver but
failed the startup-trace acceptance on that attempt; Intel macOS completed its
slow lane but failed the required RSS/AI evidence gate. The cycle therefore
does not activate the maintenance lock and remains actionable under the related
startup and provider-reliability CRs.
