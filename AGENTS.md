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

## Process Management

Whenever you start a background process, local development server, or testing
instance, you must explicitly terminate it before reporting the task as
complete.

## Clean-Slate Baseline

This repository was intentionally reset on 2026-08-13 and then republished as
the fresh DNPPV-2.0 migration workspace.

The inherited product implementation, obsolete packaging lanes, and old
traceback artifacts were removed. The active project documentation, CR
inventories, architecture contracts, gate scripts, and test-machine record
remain intentionally in scope:

1. The NVIDIA NIM review gate under `build/` and its supporting standard in
   `docs/FRESH-PROJECT-NVIDIA-REVIEW-GATE.md`.
2. The migration architecture, acceptance, gap-ledger, and CR inventory
   documents under `docs/`.
3. The build/test machine access record in `docs/TEST_MACHINE_ACCESS.md`.
4. The migration issue tracker in `docs/AUDIT_STATE.json`.

All prior product, migration, workflow, validation, and architecture artifacts
were deliberately removed.

## Migration Orientation

This repository is the under-development migration line for the public upstream
DNPPV 1.0 release. Work here must not mutate the upstream repository.

## Migration Behavior Gates

Before product-code work begins on any CR, complete a source-cited inventory of
all related upstream behavior and pass
`build/Test-MigrationBehaviorGate.ps1 -CrId CR-NNN -Stage PreDevelopment`.
Before closure, independently rescan the upstream implementation and pass the
same gate with `-Stage Closure`. Missing or unmapped behavior hard-stops work or
closure as applicable. Follow `docs/MIGRATION_BEHAVIOR_GATES.md`.

## NVIDIA NIM Review Gate

Before executing a newly composed multi-line PowerShell command, a command with
nested quoting/interpolation, or a Windows native-command bridge, parse it with
`build/Test-PowerShellSyntax.ps1 -CommandText`.

If the command intentionally hops through `cmd.exe`, include
`-AllowCmdShell` after inspecting the exact command text.

For every nontrivial generated PowerShell command, the default execution path
is the checked wrapper:

`build/Invoke-CheckedPowerShell.ps1 -CommandText`

That wrapper must remain the standard path for multi-line commands, nested
quoting/interpolation, native-command bridges, and other nontrivial generated
PowerShell invocations so validation and execution happen in one step.

The preserved review-gate entry points are:

- `build/Run-NvidiaCodeReview.ps1`
- `build/Invoke-NvidiaReviewHarness.ps1`
- `build/Test-NvidiaWorkflowGate.ps1`
- `build/Test-WorkflowGateConfiguration.ps1`
- `build/NvidiaWorkflowCommon.ps1`
- `docs/FRESH-PROJECT-NVIDIA-REVIEW-GATE.md`
- `build/Invoke-CheckedPowerShell.ps1`

## Generated Artifact Cleanup

Use `build/Cleanup-LocalProjectArtifacts.ps1` for disposable local outputs.
It removes only the project-owned `dnppv2-*` temp directories, known generated
roots, and `bin`/`obj` directories below this repository. The corresponding
GitHub workflow, `.github/workflows/cleanup-generated-artifacts.yml`, is
`workflow_dispatch` only and has read-only repository permissions; it must not
be converted into an automatic cleanup trigger.

## Test Machines

Current machine access details are recorded only in
`docs/TEST_MACHINE_ACCESS.md`.

## License Header Gate

Every tracked project artifact in this repository must carry the required
license notice in a form appropriate to the file type. Run
`build/Test-LicenseHeaders.ps1` before committing or pushing changes that add
or modify project files.

## Upstream Push Lock

No pushes to the upstream 1.0 repository are permitted from this workspace.
The local pre-push hook and `build/Assert-NoUpstreamMutation.ps1` enforce that
lock.
