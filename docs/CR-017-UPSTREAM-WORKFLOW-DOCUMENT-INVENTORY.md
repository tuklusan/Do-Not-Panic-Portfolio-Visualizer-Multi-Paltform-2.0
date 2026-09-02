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

# CR-017 Upstream Workflow And Documentation Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

The pinned upstream tree contains 109 workflow, build, test-script, and
documentation artifacts. Each is opened and read line by line, with active
behavior mapped to the current public Avalonia workflow or explicitly retired.

| ID | Upstream area | 2.0 disposition |
| --- | --- | --- |
| WD-01 | `.github/CODEOWNERS`, `.github/workflows/itch-publish.yml` | Active review ownership and portable publish workflow mapped to current `.github` workflow policy; schedule-based CODEQL remains prohibited. |
| WD-02 | `.githooks/pre-push.ps1`, build review and gate scripts | Active license, syntax, upstream-mutation, migration-behavior, and DeepSeek reviewer gates mapped to current `build/` gates. |
| WD-03 | `build/validation/*`, `build/vm/*` | Active local-machine and hosted validation behavior mapped to current cross-platform product acceptance harnesses. |
| WD-04 | `build/publish.ps1`, release-manifest and release-report scripts | Portable self-contained six-RID publish and artifact verification mapped; no installer is required. |
| WD-05 | `BUILD_AND_DEPLOY.md`, `README.md`, `AGENTS.md`, `YFinance.net/PORTING_PLAN.md` | Active build, migration, process, and portable-provider guidance mapped to current project documentation. |
| WD-06 | `docs/*` migration, anomaly, acceptance, and operations documents | Active behavior, evidence, environment, and workflow contracts mapped to current docs and tracker. |
| WD-07 | `build/installer/*`, `build/publish-inno-installer.ps1`, installer sandbox and Inno tests | RETIRED: 2.0 is Avalonia-only and has no Windows installer; no active product behavior is lost. |
| WD-08 | historical upstream review reports and obsolete sandbox automation | RETIRED or replaced where they describe the superseded Windows/WPF workflow; current gates and contracts are authoritative. |
| WD-09 | distribution and third-party notices | Active licensing, attribution, and release disclosure obligations mapped to the current repository license policy. |

## Audit Exit

All 109 pinned artifacts must be individually ledgered after line-by-line
review. Two successive fresh scans must report zero unclassified active
workflow/document behaviors. Missing active automation or documentation must be
added as actionable CRs; installer/WPF/history-only material remains retired.

## Audit Record

The pinned upstream scan enumerated 109 workflow, build, test-script, and
documentation artifacts. Two successive fresh scans reconciled active gate,
publish, validation, licensing, and documentation behavior with the current
2.0 workflow; installer and WPF-only artifacts were classified as retired.
