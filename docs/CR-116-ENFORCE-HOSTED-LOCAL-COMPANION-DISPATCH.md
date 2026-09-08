<!--
============================================================================
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
============================================================================
-->

# CR-116: Enforce Hosted-Matrix and Local-Companion Dispatch

## Status

In progress. The workflow now emits a credential-free, same-duration local
companion request for every hosted cycle, and the checked-in receipt validator
binds a completed local cycle to that request by run-derived cycle identity,
duration, machine coverage, and terminal lane status. Physical execution of
two combined cycles remains an acceptance requirement; no new soak is launched
solely to validate the validator or post-run instrumentation.

Two attempts against the same self-contained hosted candidate are recorded as
diagnostic only. Linux, Windows 10, and Intel macOS produced successful
real-product evidence in the first attempt; Windows 11 launched and completed
its soak but failed strict AI evidence after the configured external OpenRouter
endpoint returned HTTP 429. The second attempt reproduced the Windows 11
external-endpoint response, while its Linux child exceeded
the bounded completion window without a terminal result. A fresh availability
probe recorded all four machines reachable. These results do not satisfy the
combined closure requirement.

A fresh four-machine 10-minute cycle on 2026-09-07 reached all four boxes and
completed collection. Linux and both Windows lanes recorded fresh RSS playback
but the required AI success event was absent after OpenRouter returned HTTP
429; macOS recorded RSS playback but its strict news evidence gate reported a
missing RSS/AI trace pair. The coordinator failed closed and disposable
artifacts were cleaned after inspection. These findings remain routed to the
existing AI/news CRs and are not local-machine unavailability.
Under the updated CR-117 disposition contract, evidence-matched external RSS
outage and model HTTP 4xx conditions are advisory; they do not mask missing
traces, missing requests, cleanup defects, or unknown runtime failures.

## Gap

CR-092 defines the combined hosted-plus-local acceptance contract and
`build/Invoke-LocalLabSoakCycle.ps1` implements the physical-machine runner.
The hosted workflow now emits `dnppv2-local-companion-dispatch-<run>.json` as a
credential-free handoff. Its `companionCycleId` is the deterministic cycle
identity expected by `build/Test-LocalCompanionReceipt.ps1`. A private-lab
operator consumes that request with the frozen local coordinator and validates
the resulting cycle manifest; hosted runners never reach the private LAN.

## Functional Inventory

| ID | Requirement | Evidence |
| --- | --- | --- |
| COMP-01 | Each hosted matrix cycle records a uniquely identified local-companion cycle request with the same soak duration. | Dispatch manifest and shared cycle identifier. |
| COMP-02 | The local coordinator probes all four machines at cycle start and records available versus unavailable machines explicitly. | Availability and cycle manifests. |
| COMP-03 | Every available machine runs the real product with the same RSS, AI, screenshot, dual-trace, cleanup, and artifact-review contract as hosted lanes. | Per-machine result and reviewed evidence. |
| COMP-04 | The bridge cannot launch a second local or hosted cycle while one is queued or active. | Serialization and concurrency gate tests. |
| COMP-05 | Local-network unavailability remains an explicit non-product skip; an available-machine failure remains a failure. | Aggregated disposition and negative tests. |
| COMP-06 | The bridge never exposes local credentials or requires hosted runners to reach the private LAN. | Secret-scan and workflow topology review. |
| COMP-07 | A local result cannot be accepted for the wrong hosted request, duration, machine set, or non-terminal lane state. | `build/Test-LocalCompanionReceipt.ps1 -SelfTest` and receipt validation. |

## Required Work

The dispatch and receipt bridge is implemented by
`build/New-LocalCompanionDispatch.ps1` and
`build/Test-LocalCompanionReceipt.ps1`. The operator-controlled local
coordinator must consume the request using the prescribed cycle identity and
retain the validated receipt with the combined evidence. Keep
`Invoke-LocalLabSoakCycle.ps1` locked unless a concrete continuation defect
requires a change. A startup/scene timeout is a concrete continuation defect:
the coordinator must bind every platform cycle root before launch and remove
it in its `finally` cleanup path, so a product that is not visible within the
180-second validation timeout aborts cleanly and cannot poison the next cycle
with a stale-root hard stop. Reconcile combined evidence with CR-092, CR-094,
and CR-109 without weakening any existing gate.
On Linux, every `xdotool search --pid` window-discovery probe is independently
bounded to five seconds with a two-second kill grace period, and the discovery
loop uses a wall-clock deadline rather than a probe-count budget. A hung X11
probe therefore cannot defeat the 180-second scene deadline; the normal
trap/finally cleanup path remains authoritative for the owned product, helper,
and cycle root.

## Closure Gates

Perform the upstream forward and reverse inventories, workflow/license/syntax
gates, NVIDIA review, focused serialization and secret-free dispatch tests, two
independent hosted-plus-available-local cycles, full evidence inspection, local
artifact cleanup, and commit/push. Do not close this CR from hosted-only
evidence.
