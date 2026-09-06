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

Queued. Low priority. This is an approved one-off out-of-sequence CR to be
addressed at a convenient point; it must not interrupt or duplicate an active
serialized hosted matrix.

## Gap

CR-092 defines the combined hosted-plus-local acceptance contract and
`build/Invoke-LocalLabSoakCycle.ps1` implements the physical-machine runner,
but the current checked-in GitHub workflow set contains no dispatch bridge that
starts the four-machine companion cycle when a hosted matrix is launched.
Consequently, a hosted push currently proves only the GitHub lanes unless the
operator separately invokes the local coordinator.

## Functional Inventory

| ID | Requirement | Evidence |
| --- | --- | --- |
| COMP-01 | Each hosted matrix cycle records a uniquely identified local-companion cycle request with the same soak duration. | Dispatch manifest and shared cycle identifier. |
| COMP-02 | The local coordinator probes all four machines at cycle start and records available versus unavailable machines explicitly. | Availability and cycle manifests. |
| COMP-03 | Every available machine runs the real product with the same RSS, AI, screenshot, dual-trace, cleanup, and artifact-review contract as hosted lanes. | Per-machine result and reviewed evidence. |
| COMP-04 | The bridge cannot launch a second local or hosted cycle while one is queued or active. | Serialization and concurrency gate tests. |
| COMP-05 | Local-network unavailability remains an explicit non-product skip; an available-machine failure remains a failure. | Aggregated disposition and negative tests. |
| COMP-06 | The bridge never exposes local credentials or requires hosted runners to reach the private LAN. | Secret-scan and workflow topology review. |

## Required Work

Design and implement the smallest reliable bridge between the hosted workflow
and an operator-controlled local coordinator. Prefer a checked-in dispatch and
receipt protocol over pretending that a hosted GitHub runner can SSH into the
private lab. Keep `Invoke-LocalLabSoakCycle.ps1` locked unless a concrete
continuation defect requires a change. Reconcile the resulting combined
evidence with CR-092, CR-094, and CR-109 without weakening any existing gate.

## Closure Gates

Perform the upstream forward and reverse inventories, workflow/license/syntax
gates, NVIDIA review, focused serialization and secret-free dispatch tests, two
independent hosted-plus-available-local cycles, full evidence inspection, local
artifact cleanup, and commit/push. Do not close this CR from hosted-only
evidence.
