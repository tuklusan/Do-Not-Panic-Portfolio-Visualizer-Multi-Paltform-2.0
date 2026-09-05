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

# CR-101: Clean Downloaded Hosted Evidence Artifacts

## Objective

Ensure project cleanup removes downloaded hosted-run evidence directories so
disposable artifacts cannot contaminate license, syntax, or source validation.

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| CL-01 | Cleanup removes project-owned `build/hosted-run-*` directories. | `Cleanup-LocalProjectArtifacts.ps1`. | Implemented |
| CL-02 | Cleanup remains scoped to disposable project outputs. | Existing generated-root and `bin`/`obj` allowlist. | Implemented |
| CL-03 | Cleanup behavior is verified after hosted artifact retrieval. | Cleanup test and post-download validation. | Open |

## Upstream and Reverse Gates

This is migration workflow infrastructure with no upstream product equivalent.
Before closure, rescan the complete cleanup script and all callers to prove the
allowlist remains narrow and all downloaded hosted evidence is disposable.

## Evidence

Run `33982819747` downloaded 8,871 files under `build/hosted-run-33982819747`.
The existing cleanup script left that root behind, and the resulting generated
JSON caused the license-header scan to fail. The script now includes the
`hosted-run-*` generated-root pattern.

## Acceptance

After a representative hosted artifact download, cleanup removes the hosted
run root, leaves tracked source/documentation intact, and all repository gates
pass on the cleaned worktree.
