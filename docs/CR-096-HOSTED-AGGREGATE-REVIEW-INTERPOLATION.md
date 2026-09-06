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

# CR-096: Fix Hosted Aggregate Review Interpolation

## Objective

Make the hosted post-soak aggregate reviewer parse and execute on every
supported runner shell, while preserving the 21-lane evidence requirements.

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| AR-01 | Aggregate review reports the failing artifact directory and safe reviewer message without PowerShell variable/drive-scope parsing errors. | Braced `${artifactDirectory}` interpolation in `.github/workflows/publish-six-rids.yml`. | Implemented |
| AR-02 | The workflow gate rejects a regression to unsafe interpolation. | Static assertion in `build/Test-WorkflowGateConfiguration.ps1`. | Implemented |
| AR-03 | Aggregate review consumes one manifest and inspected closure record for every hosted lane. | The 21-entry publish/soak matrix and evidence schema defined normatively by `docs/CR-094-21-PLUS-4-MATRIX-EVIDENCE.md`. | Pending hosted verification |

## Upstream Check

The upstream 1.0 repository has no equivalent hosted 2.0 aggregate evidence
review workflow. This migration-specific workflow behavior is therefore
recorded as new 2.0 infrastructure; no upstream product behavior is removed.

## Acceptance

1. [AR-01] The aggregate review PowerShell parses on Linux PowerShell.
2. [AR-02] The workflow configuration gate checks the braced interpolation.
3. The full local validation suite passes.
4. [AR-03] A fresh serialized 21-lane hosted run reaches terminal completion
   and its aggregate evidence review consumes all lane manifests and closure
   records, according to the lane and record contract in
   `docs/CR-094-21-PLUS-4-MATRIX-EVIDENCE.md`.

Acceptance status: criteria 1-3 are locally verified; criterion 4 remains
pending until the corrected checkpoint's hosted run reaches terminal state and
the aggregate review consumes all 21 lane records.

## Closure Evidence

Closure requires the local gate outputs, the 312-test Release result, the
mandatory NVIDIA review result, and the fresh hosted run ID plus its 21
manifest/closure-record inspection summary. Evidence is pending fresh hosted
validation after the corrected checkpoint is pushed. Hosted run `34061105976`
reached terminal failure and the aggregate reported `Expected 21 soak evidence
manifests, found 20`; the missing lane was `macos-26` (`osx-arm64`). The
interpolation path therefore failed closed as designed, but the hosted
verification criterion remains open.

Run `34063195136` reached the aggregate with all 21 soak manifests present and
again failed closed on review completeness: 20 semantic review results were
available and `ubuntu-slim|linux-x64` had none. The aggregate also retained
blocking findings from 12 lanes, including AI evidence gaps; interpolation is
working, but the CR remains open pending a complete authoritative review set.
