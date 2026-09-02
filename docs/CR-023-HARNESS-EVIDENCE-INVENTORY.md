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

# CR-023 Harness and Evidence Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| TST-01 | Upstream behavior | 2.0 mapping |
| --- | --- | --- |
| TST-01 | Validation runs the real application, captures visible GUI state, records bounded circular trace output, reviews artifacts, and terminates launched processes. | `build/vm/Invoke-ConfigWindowValidation.ps1`, `build/vm/Invoke-ProductSceneValidation.ps1`, `build/Assert-ValidationCheckpoint.ps1`, and `build/Cleanup-LocalProjectArtifacts.ps1`; product traces use the bounded circular trace pair. |
| TST-02 | Workflow execution is gated by clean committed checkpoints, syntax/license/migration/reviewer checks, platform-specific storage rules, and explicit physical-acceptance evidence. | `build/Test-DeepSeekWorkflowGate.ps1`, `build/Test-MigrationBehaviorGate.ps1`, `build/Test-LicenseHeaders.ps1`, `build/Test-PowerShellSyntax.ps1`, `build/Invoke-CheckedPowerShell.ps1`, the VM scripts, and the documented test-machine contracts. |

## Reverse Upstream Gap Scan

The pinned upstream validation, sandbox, VM, workflow, trace, screenshot, and
test artifacts were rescanned against the current build and test surfaces.
Two successive scans found no missing TEST-01 or TEST-02 behavior. Differences
that are product-specific or platform-specific remain routed to the numbered
product CRs; no harness gap is silently waived.

## Exit Criteria

Require the gate self-tests, a clean committed checkpoint, reviewer result,
physical artifact review, and process/storage cleanup inspection before closure.
