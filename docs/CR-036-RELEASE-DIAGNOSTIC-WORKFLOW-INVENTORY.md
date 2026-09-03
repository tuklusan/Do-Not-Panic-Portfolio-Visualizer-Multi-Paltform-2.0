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

# CR-036 Release and Diagnostic Workflow Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| REL-01 | Publication is explicitly authorized and denied by default until required build, validation, and artifact evidence exists. | The migrated publish workflow and release validation scripts require the committed validation state and do not authorize an incomplete publication. |
| REL-02 | Release artifacts are generated deterministically and checked for completeness and integrity before publication. | `ReleaseManifestValidator` and the publish workflow validate the generated tree and manifest before any release step. |
| REL-03 | Diagnostics use the bounded circular trace and redact sensitive values. | `TraceLog`, `CircularTraceSettings`, and `SensitiveDataRedactor` provide the shared bounded diagnostic path used by product and harnesses. |
| REL-04 | Diagnostic dumps and crash evidence are opt-in/controlled, scoped to the product, and do not silently create unbounded artifacts. | The migrated diagnostics and cleanup tooling keep dump controls explicit, route logs through the circular trace, and review artifact roots. |
| REL-05 | Reviewer-gate failure blocks workflow completion and publication. | NVIDIA NIM workflow scripts and the pre-push hook enforce reviewer, license, syntax, and upstream-mutation gates. |
| REL-06 | Validation artifacts are analyzed for required screenshots, traces, completion markers, and forbidden leakage. | VM/artifact validation scripts inspect evidence structure, required files, trace safety, and completion markers before acceptance. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Missing validation checkpoint | Publication is denied. | Publish workflow and checkpoint assertion. |
| Incomplete or tampered manifest | Release validation fails before publication. | Manifest validator tests. |
| Reviewer gate failure | Workflow stops and reports failure. | NVIDIA NIM workflow self-tests. |
| Invalid or unredacted diagnostic content | Artifact review rejects or redacts it. | Trace/redaction and artifact analyzer tests. |
| Missing screenshot/trace/done marker | Acceptance fails rather than passing on partial evidence. | VM validation scripts and artifact tests. |
| Diagnostic dump disabled | No dump is silently collected. | Explicit diagnostics controls. |
| Cleanup requested | Project test, build, and temporary roots are removed without touching unrelated data. | Cleanup tooling and root policy. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream release, diagnostics, manifest,
validation, reviewer-gate, and artifact-review implementation and tests,
followed by scans of the migrated workflow and test paths, found no unmapped
behavior for REL-01 through REL-06. The cross-platform workflow preserves the
upstream publication and evidence controls without Windows-only packaging.
