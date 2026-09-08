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

# Test Harness Freeze

The test harnesses are tracked in `docs/TEST_HARNESS_MANIFEST.json` and are
frozen at the validated baseline. Harness optimization, tuning, or behavioral
changes are outside the migration objective and are prohibited by default.

Any intentional harness change requires explicit operator approval for the
same validation and push operation. Set `DNPPV_HARNESS_CHANGE_APPROVED` to
`Y` or `1`, and record the approval and reason in the applicable CR and commit
message. Without that authorization, `build/Test-HarnessFreeze.ps1` and the
pre-push hook hard-stop the operation.

The freeze covers hosted workflows, local-machine launch/soak/validation
scripts, review/evidence harnesses, cleanup harnesses, and the PowerShell
command-validation wrapper listed in the manifest. Disposable outputs are
not frozen; they remain subject to the normal cleanup workflow.

The current product/test correction that established this baseline is retained
as-is. Future harness edits require the approval gate above.

## Evidence-integrity invariant

The manifest is the mechanical protected-set authority. A frozen harness is an
evidence instrument: it must not be weakened to manufacture PASS, erase a
required failure, or turn a harness defect into `UnavailableAtCycleStart`.
When a frozen harness fails after a target has been admitted, the result is a
harness failure and the valid raw product evidence remains available for
reprocessing. The harness must be repaired, deterministically tested, and
refrozen before the affected validation lane resumes.
