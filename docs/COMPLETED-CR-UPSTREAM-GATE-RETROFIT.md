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

# Upstream Behavior Gate Coverage

This document is the current coverage index for the mandatory upstream
behavior gates. It is not a historical log. The authoritative per-request
status and evidence live in `docs/AUDIT_STATE.json`; the source-derived
acceptance contracts live in the other documents named below.

## Current Baseline

The upstream-gate retrofit baseline is complete at pushed commit `ced5c21`.
The migration itself remains active: the authoritative tracker currently has
22 open CRs covering validation, visual parity, provider reliability, and
soak evidence. The active product is a .NET 10 Avalonia desktop application
on Windows, Linux, and macOS. WPF and a Windows installer are not part of the
active architecture. The six release RIDs are `win-x64`, `win-arm64`,
`linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`.

## Coverage

| Scope | Current authoritative evidence |
| --- | --- |
| Acceptance baseline and production scene | `docs/UPSTREAM_ACCEPTANCE_BASELINE.md` |
| Numeric cinematic geometry, motion, playback, and lifecycle | `docs/UPSTREAM_CINEMATIC_DISPLAY_CONTRACT.md` |
| Portable path, process, and loopback rules | `docs/PORTABLE_RUNTIME_CONTRACT.md` |
| Physical test machines and hosted runners | `docs/TEST_MACHINE_ACCESS.md` and `.github/workflows/publish-six-rids.yml` |
| CR pre-development and closure enforcement | `docs/MIGRATION_BEHAVIOR_GATES.md` and `build/Test-MigrationBehaviorGate.ps1` |
| Per-CR upstream inventories and closure audits | `docs/CR-*-UPSTREAM-BEHAVIOR-INVENTORY.md` and `docs/AUDIT_STATE.json` |

## Gate Invariant

Every product change request must have a source-cited upstream inventory with
zero known gaps before implementation. Closure requires a fresh independent
upstream rescan, at least two successive zero-gap scans, zero unresolved gaps,
and evidence appropriate to the request. A test toy, static fixture screen, or
unreviewed artifact cannot satisfy real-product acceptance.

## Current Result

The gate-retrofit work recorded by CR-013 is closed. CR-011 records real
degraded-mode acceptance on all four local machines, and CR-012 records the
six-RID hosted publish and local physical acceptance. The remaining open CRs
are listed in `docs/AUDIT_STATE.json`; new work must be introduced as a CR and
must not silently alter the gate baseline.
