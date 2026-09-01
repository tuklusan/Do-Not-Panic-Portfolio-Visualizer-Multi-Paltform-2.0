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

# Upstream 1.0 to DNPPV-2.0 Gap Ledger

Generated artifact register for upstream commit $upstreamCommit. The register covers every tracked upstream artifact; each row is a review unit, not a claim that equal filenames imply equal behavior.

## Scan Protocol

Each upstream file is opened from the pinned tree, read line-by-line, and assigned one disposition: `MAPPED` (behavior mapped to 2.0), `REPLACED` (intentional architecture/platform replacement), `RETIRED` (historical or prohibited artifact), or `GAP` (missing 2.0 behavior/artifact). Functional gaps are recorded as CRs in `docs/AUDIT_STATE.json`.

| Upstream artifact | Lines | Disposition | 2.0 mapping / gap |
| --- | ---: | --- | --- |
| $safeFile | 1 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 3 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 148 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 1 | MAPPED | Current build/workflow counterpart to verify: .github/CODEOWNERS or the current build/.github gate family. |
| $safeFile | 276 | MAPPED | Current build/workflow counterpart to verify: .github/workflows/itch-publish.yml or the current build/.github gate family. |
| $safeFile | 61 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 98 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 241 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 63 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 57 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 229 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 150 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 26 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 242 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 201 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 34 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 273 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 31 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 29 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 820 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 23 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 366 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 31 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 26 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 29 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 22 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 27 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 19 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 22 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 24 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 26 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 37 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 18 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 25 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 35 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 105 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 22 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 25 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 26 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 30 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 148 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 40 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 25 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 106 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 261 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 722 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 107 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 31 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 59 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 87 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 51 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 19 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 107 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 94 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 67 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 25 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 35 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 330 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 36 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 176 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 24 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 24 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 281 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 199 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 156 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 83 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 357 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 14 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 19 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 22 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 29 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 19 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 26 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 48 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 54 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 57 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 46 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 213 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 252 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 26 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 32 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 8 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 50 | MAPPED | Current build/workflow counterpart to verify: build/DeepSeekWorkflowCommon.ps1 or the current build/.github gate family. |
| $safeFile | 556 | MAPPED | Current build/workflow counterpart to verify: build/Run-DeepSeekCodeReview.ps1 or the current build/.github gate family. |
| $safeFile | 114 | MAPPED | Current build/workflow counterpart to verify: build/Test-DeepSeekWorkflowGate.ps1 or the current build/.github gate family. |
| $safeFile | 63 | MAPPED | Current build/workflow counterpart to verify: build/YFinanceServer.targets or the current build/.github gate family. |
| $safeFile | 194 | MAPPED | Current build/workflow counterpart to verify: build/build-safe-temp.ps1 or the current build/.github gate family. |
| $safeFile | 97 | MAPPED | Current build/workflow counterpart to verify: build/diagnostics/Set-DesktopWerLocalDumps.ps1 or the current build/.github gate family. |
| $safeFile | 69 | MAPPED | Current build/workflow counterpart to verify: build/generate-release-manifest.ps1 or the current build/.github gate family. |
| $safeFile | 232 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 340 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 213 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 359 | MAPPED | Current build/workflow counterpart to verify: build/publish-inno-installer.ps1 or the current build/.github gate family. |
| $safeFile | 235 | MAPPED | Current build/workflow counterpart to verify: build/publish-safe-temp.ps1 or the current build/.github gate family. |
| $safeFile | 33 | MAPPED | Current build/workflow counterpart to verify: build/publish.ps1 or the current build/.github gate family. |
| $safeFile | 538 | MAPPED | Current build/workflow counterpart to verify: build/release/Publish-VirusTotalReleaseReport.ps1 or the current build/.github gate family. |
| $safeFile | 45 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 29 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 29 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 29 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 29 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 22 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 78 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 95 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 2 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 312 | MAPPED | Current build/workflow counterpart to verify: build/validation/Add-AuditChangeRequest.ps1 or the current build/.github gate family. |
| $safeFile | 284 | MAPPED | Current build/workflow counterpart to verify: build/validation/Analyze-InstalledReleaseMonitor.ps1 or the current build/.github gate family. |
| $safeFile | 108 | MAPPED | Current build/workflow counterpart to verify: build/validation/Analyze-InstalledSoakTrace.ps1 or the current build/.github gate family. |
| $safeFile | 798 | MAPPED | Current build/workflow counterpart to verify: build/validation/Analyze-VisualValidationArtifacts.ps1 or the current build/.github gate family. |
| $safeFile | 461 | MAPPED | Current build/workflow counterpart to verify: build/validation/Collect-InstalledReleaseMonitor.ps1 or the current build/.github gate family. |
| $safeFile | 202 | MAPPED | Current build/workflow counterpart to verify: build/validation/Invoke-AutonomousVisualValidation.ps1 or the current build/.github gate family. |
| $safeFile | 255 | MAPPED | Current build/workflow counterpart to verify: build/validation/Invoke-DeepSeekArtifactReview.ps1 or the current build/.github gate family. |
| $safeFile | 180 | MAPPED | Current build/workflow counterpart to verify: build/validation/Run-InstalledSoakOnce.local.ps1 or the current build/.github gate family. |
| $safeFile | 756 | MAPPED | Current build/workflow counterpart to verify: build/validation/Test-ValidationScripts.ps1 or the current build/.github gate family. |
| $safeFile | 6 | MAPPED | Current build/workflow counterpart to verify: build/validation/allowed-fault-injection-trace-patterns.txt or the current build/.github gate family. |
| $safeFile | 6 | MAPPED | Current build/workflow counterpart to verify: build/validation/allowed-trace-patterns.txt or the current build/.github gate family. |
| $safeFile | 66 | MAPPED | Current build/workflow counterpart to verify: build/vm-enum-windows.ps1 or the current build/.github gate family. |
| $safeFile | 413 | MAPPED | Current build/workflow counterpart to verify: build/vm-settings.example.json or the current build/.github gate family. |
| $safeFile | 50 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/fix-scoop-and-install-sysinternals.ps1 or the current build/.github gate family. |
| $safeFile | 56 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/install-choco.ps1 or the current build/.github gate family. |
| $safeFile | 39 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/install-scoop.ps1 or the current build/.github gate family. |
| $safeFile | 26 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/install-sysinternals-direct.ps1 or the current build/.github gate family. |
| $safeFile | 121 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/install-vm-qa-tools-resume.ps1 or the current build/.github gate family. |
| $safeFile | 128 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/install-vm-qa-tools.ps1 or the current build/.github gate family. |
| $safeFile | 22 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/repair-python-ui-packages.ps1 or the current build/.github gate family. |
| $safeFile | 36 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/scan-existing-tools.ps1 or the current build/.github gate family. |
| $safeFile | 29 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/verify-package-managers.ps1 or the current build/.github gate family. |
| $safeFile | 126 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/verify-vm-tools.ps1 or the current build/.github gate family. |
| $safeFile | 123 | MAPPED | Current build/workflow counterpart to verify: build/vm-tools/vm-tool-inventory.ps1 or the current build/.github gate family. |
| $safeFile | 75 | MAPPED | Current build/workflow counterpart to verify: build/vm/Guest-ApplyTestSecrets.ps1 or the current build/.github gate family. |
| $safeFile | 109 | MAPPED | Current build/workflow counterpart to verify: build/vm/Guest-BootstrapVmRemoteTools.ps1 or the current build/.github gate family. |
| $safeFile | 29 | MAPPED | Current build/workflow counterpart to verify: build/vm/Guest-ClearDesktopAutomationCredentials.ps1 or the current build/.github gate family. |
| $safeFile | 76 | MAPPED | Current build/workflow counterpart to verify: build/vm/Guest-ConfigureDesktopAutomation.ps1 or the current build/.github gate family. |
| $safeFile | 4152 | MAPPED | Current build/workflow counterpart to verify: build/vm/Guest-UxDeepExercise.ps1 or the current build/.github gate family. |
| $safeFile | 376 | MAPPED | Current build/workflow counterpart to verify: build/vm/Invoke-VmBuildTest.ps1 or the current build/.github gate family. |
| $safeFile | 286 | MAPPED | Current build/workflow counterpart to verify: build/vm/PostProcess-ReferenceSpotChecks.ps1 or the current build/.github gate family. |
| $safeFile | 64 | MAPPED | Current build/workflow counterpart to verify: build/vm/Pull-VmResults.ps1 or the current build/.github gate family. |
| $safeFile | 181 | MAPPED | Current build/workflow counterpart to verify: build/vm/Push-VmWorkspace.ps1 or the current build/.github gate family. |
| $safeFile | 470 | MAPPED | Current build/workflow counterpart to verify: build/vm/Run-VmUxValidation.ps1 or the current build/.github gate family. |
| $safeFile | 400 | MAPPED | Current build/workflow counterpart to verify: build/vm/VM_OPERATIONS_RUNBOOK.md or the current build/.github gate family. |
| $safeFile | 102 | MAPPED | Current build/workflow counterpart to verify: build/vm/VmPackageInstallCommon.ps1 or the current build/.github gate family. |
| $safeFile | 469 | MAPPED | Current build/workflow counterpart to verify: build/vm/VmSshCommon.ps1 or the current build/.github gate family. |
| $safeFile | 125 | MAPPED | Current build/workflow counterpart to verify: build/vm/VmTraceQuoteEvidence.ps1 or the current build/.github gate family. |
| $safeFile | 146 | MAPPED | Current build/workflow counterpart to verify: build/vm/VmWindowInput.ps1 or the current build/.github gate family. |
| $safeFile | 341 | MAPPED | Current build/workflow counterpart to verify: build/vm/vm-settings-full.json or the current build/.github gate family. |
| $safeFile | 10 | MAPPED | Current build/workflow counterpart to verify: build/vm/vm-settings.example.json or the current build/.github gate family. |
| $safeFile | 63 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 215 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 612 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 12282 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 108 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 334 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 249 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 376 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 212 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 122 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 60 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 1269 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 146 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 127 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 162 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 146 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 551 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 111 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 82 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 550 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 376 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 136 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 4898 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 8 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 56 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 8 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 529 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 8333 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 5 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 1 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 1 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 90 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 3 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 11330 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 10272 | MAPPED | Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR. |
| $safeFile | 6 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 73 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 64 | RETIRED | Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product. |
| $safeFile | 299 | MAPPED | Root/build metadata counterpart reviewed against the clean-slate 2.0 repository. |
| $safeFile | 22 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 84 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 33 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 91 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 177 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 19 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 23 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 29 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 121 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 25 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 23 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 31 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 29 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 28 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 28 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 24 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 26 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 294 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 60 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 46 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 144 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 342 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 99 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 75 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 23 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 24 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 25 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 33 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 17 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 312 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 156 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 269 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 114 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 121 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 33 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 31 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 35 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 77 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 152 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 51 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 103 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 58 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 41 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 125 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 96 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 231 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 295 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 66 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 22 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 304 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 9 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 3873 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 82261 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 12238 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 47 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 23 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 111 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 36 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 69 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 1231 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 26 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 20 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 27 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 37 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 34 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 494 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 27 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 232 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 5365 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 31 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 1657 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 30 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 144 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 227 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 102 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 112 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 51 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 1572 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 34 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 70 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 183 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 7 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 13 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 5 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 5 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 6 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 4 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 9 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 4 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 6 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 4 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 4 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 5 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 8 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 13 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 4 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 9 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 8 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 5 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 151 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 24 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 158 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 134 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 100 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 597 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 71 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 969 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 183 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 24 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 62 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 522 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 31 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 127 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 64 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 151 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 140 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 33 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 23 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 251 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 36 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 274 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 69 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 115 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 69 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 32 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 38 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 42 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 58 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 85 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 118 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 45 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 34 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 12 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 10 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 41 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 252 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 137 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 51 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 36 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 23 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 106 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 176 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 378 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 1541 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 22 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 151 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 135 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 607 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 204 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 55 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 33 | REPLACED | WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF. |
| $safeFile | 22 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 10774 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 1233 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 14932 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 74 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 45 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 230 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 364 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 47 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 405 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 202 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 48 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 27 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 37 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 24 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 43 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 29 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 216 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 100 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 28 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 21 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 16 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 184 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 48 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 236 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 31 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 525 | MAPPED | Portable 2.0 counterpart expected at $normalized; line-level behavior reviewed under the product-parity CR. |
| $safeFile | 43 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 16 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 102 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 141 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 271 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 409 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 115 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 253 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 118 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 327 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 437 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 117 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 143 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 721 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 562 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 72 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 21 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 373 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 2155 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 129 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 228 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 225 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 139 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 61 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 115 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 85 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 1777 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 29 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 1687 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 115 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 91 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 92 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 75 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 77 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 393 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 157 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 176 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 126 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 122 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 183 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 201 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 137 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 392 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 409 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 68 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 219 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 37 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 47 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 86 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 51 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 483 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 138 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 4640 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 1306 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 313 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 419 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 1267 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 156 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 711 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 257 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 75 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |
| $safeFile | 95 | MAPPED | Current test counterpart to verify: $normalized; missing cases are tracked by the test-parity CR. |

## Initial Gap Register

| Gap family | Missing or changed upstream behavior/artifact | CR |
| --- | --- | --- |
| Product parity | Every user-visible workflow in the upstream product source must be rechecked against the real Avalonia scene, settings, providers, motion, news, and degraded behavior. | CR-015 |
| Test parity | Upstream test cases and test-only workflows require one-by-one mapping to current tests or a documented rationale. | CR-016 |
| Automation parity | Upstream CI/release/test scripts require current workflow counterparts; prohibited installer and WPF lanes remain intentional replacements. | CR-017 |

## Completion Rule

This ledger is not complete until CR-015 through CR-017 attach line-level findings, every `GAP` is either closed or has an implementation CR, and two successive scans of the pinned upstream tree report zero unclassified artifacts.
