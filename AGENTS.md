## Process Management

Whenever you start a background process, local development server, or testing
instance, you must explicitly terminate it before reporting the task as
complete.

## Clean-Slate Baseline

This repository was intentionally reset on 2026-08-13.

Only the following retained assets remain in scope:

1. The DeepSeek review gate under `build/` and its supporting standard in
   `docs/FRESH-PROJECT-DEEPSEEK-REVIEW-GATE.md`.
2. The build/test machine access record in `docs/TEST_MACHINE_ACCESS.md`.

All prior product, migration, workflow, validation, and architecture artifacts
were deliberately removed.

## DeepSeek Review Gate

Before executing a newly composed multi-line PowerShell command, a command with
nested quoting/interpolation, or a Windows native-command bridge, parse it with
`build/Test-PowerShellSyntax.ps1 -CommandText`.

If the command intentionally hops through `cmd.exe`, include
`-AllowCmdShell` after inspecting the exact command text.

The preserved review-gate entry points are:

- `build/Run-DeepSeekCodeReview.ps1`
- `build/Invoke-DeepSeekReviewHarness.ps1`
- `build/Test-DeepSeekWorkflowGate.ps1`
- `build/DeepSeekWorkflowCommon.ps1`
- `docs/FRESH-PROJECT-DEEPSEEK-REVIEW-GATE.md`

## Test Machines

Current machine access details are recorded only in
`docs/TEST_MACHINE_ACCESS.md`.
