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

# CR-111: Preserve Clean PASS Receipts with Null Blocking Findings

## Status

Open. Discovered during authoritative run `34014436744` while processing CR-109.

## Objective

Make the hosted lane evidence inspector treat a JSON `blockingFindings` value of
`null` as an empty finding set, matching the v2 semantic receipt contract. A
clean reviewer `PASS` must not be rejected because PowerShell wraps a null
property as a one-item array when counted.

## Functional Inventory

| ID | Requirement | Proof |
| --- | --- | --- |
| NULL-01 | A v2 `PASS` receipt with `blockingFindings: null` is accepted. | Focused validator test. |
| NULL-02 | A v2 `PASS` receipt with one or more blocking findings is rejected. | Focused negative test. |
| NULL-03 | Missing, malformed, incomplete, or non-PASS receipts remain rejected. | Existing and expanded validator tests. |
| NULL-04 | The hosted workflow and aggregate retain the unchanged fail-closed contract. | Workflow gate and one authoritative matrix. |
| NULL-05 | Upstream/current workflow behavior is rescanned forward and in reverse twice. | Source-cited inventory and two zero-gap audits. |

The pre-development source scan covers the complete receipt path line by line:
`.github/workflows/publish-six-rids.yml` (lane review, receipt construction,
and inspection), `build/Test-HostedSoakClosure.ps1` (aggregate receipt
validation and self-tests), `build/Test-WorkflowGateConfiguration.ps1` (workflow
contract gate), `docs/CR-105-DETERMINISTIC-HOSTED-SOAK-CLOSURE.md`, and the
corresponding upstream hosted-evidence workflow and receipt rules at the pinned
upstream commit recorded in the tracker. The scan found the null-array
normalization gap recorded here; no other behavior was left unmapped.

## Reverse Closure Audit

Two successive fresh scans from the on-disk implementation were completed
after the defect was isolated. Pass 1 traced every receipt read, null/array
normalization, PASS predicate, negative finding predicate, and self-test case
back to NULL-01 through NULL-04. Pass 2 repeated the scan from the workflow and
aggregate validators back to the inventory and found zero unmapped behaviors.
The remaining proof obligation is the post-change hosted matrix.

## Validation Plan

Read the current hosted workflow, lane inspector, receipt schema, self-tests,
and upstream equivalent line by line before editing. Add the smallest shared
normalization/helper needed, focused positive and negative tests, all repository
gates, NVIDIA review, and a fresh serialized 21-lane matrix. Inspect every
lane's screenshots, both circular traces, RSS/AI evidence, review receipt,
closure record, and aggregate output before closure.
