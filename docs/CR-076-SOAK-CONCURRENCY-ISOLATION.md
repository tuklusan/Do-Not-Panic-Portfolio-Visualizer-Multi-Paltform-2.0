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

# CR-076: Isolate Soak Workflow Concurrency

## Objective

| CR-01 | Prevent overlapping soak runs and isolate each run's artifacts | workflow concurrency and unique roots | cleanup evidence |

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| SOAK-CI-01 | A dispatched soak must not be canceled by the ordinary push validation workflow. | Event-qualified workflow concurrency group. | Implemented |
| SOAK-CI-02 | Duplicate runs of the same event/ref remain serialized according to the existing cancel policy. | `dnppv2-publish-${{ github.event_name }}-${{ github.ref }}`. | Implemented |
| SOAK-CI-03 | A current-SHA soak must retain its publish artifacts before real-product jobs start. | Publish-to-soak job dependency and matrix. | Pending fresh-cycle proof |

## Required Gates

Run `33689685166` completed successfully, but its replacement dispatch was
partly canceled because a push run shared the branch-only concurrency group.
The source and reverse workflow scans must be repeated after this change.

## Acceptance

- A push to `main` cannot cancel a dispatched soak on `main`.
- A fresh 10-minute dispatch reaches all 18 publish and real-product jobs.
- All 18 current-SHA artifacts pass screenshot, circular-trace, AI-evidence,
  cleanup, and NVIDIA NIM artifact-review gates.
