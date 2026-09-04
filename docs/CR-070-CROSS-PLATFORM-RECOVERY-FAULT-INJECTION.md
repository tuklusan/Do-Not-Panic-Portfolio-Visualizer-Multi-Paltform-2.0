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

# CR-070 Cross-Platform Recovery Fault Injection

## Functional Inventory

| CR-01 | Inject deterministic state-write failure on every supported platform | recovery policy and focused test | failure-path evidence |

The hosted smoke exposed that the recovery-policy test simulated an unwritable
state marker with Windows-style file-sharing behavior. Unix filesystems do not
enforce that sharing mode in the same way, so the test passed locally but failed
on Linux and macOS.

The test now injects a deterministic state-write failure through the existing
startup policy boundary. Production behavior remains unchanged for normal
callers; the fault path still allocates a fresh run ID, preserves the stale
running marker, emits a warning, and refuses to mark the failed registration as
a clean exit. This makes the contract portable across Windows, Linux, and
macOS without weakening the assertion.

**Status:** Open
