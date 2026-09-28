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

The upstream-gate retrofit baseline is complete at pushed commit `8025c9b` plus
the current CR-083 closure candidate. The migration itself remains active: the
authoritative tracker currently has open CRs covering validation, visual parity, provider reliability, and
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

## CR-002
Inventory: `DoNotPanicPortfolioVisualizer.sln`, `src/PortfolioSaver.Desktop/PortfolioSaver.Desktop.csproj`, and `src/PortfolioSaver.Core/PortfolioSaver.Core.csproj`.
Reverse scan: the solution and project split preserve the cited upstream application/core boundary. Two successive scans found zero missing behaviors.

## CR-003
Inventory: upstream identity, data-root, desktop lifetime, YFinance protocol/server, and corresponding tests listed in the tracker.
Reverse scan: current identity, portable roots, duplicate-instance lease, and loopback protocol were compared with every cited upstream surface. Two successive scans found zero missing behaviors.

## CR-004
Inventory: `src/PortfolioSaver.Core`, `src/PortfolioSaver.Shared`, and `tests/PortfolioSaver.Tests`.
Reverse scan: shared models, settings contracts, diagnostics, and service interfaces were compared with the cited upstream core/shared/test surfaces. Two successive scans found zero missing behaviors.

## CR-005
Inventory: the upstream data, YFinance, settings, configuration, desktop, ticker, startup, provider, service, protocol, and test sources listed in each tracker entry.
Reverse scan: current portable data, validation, configuration, desktop, ticker, provider, protocol, and lifecycle surfaces were compared with each cited source set. Two successive scans found zero missing behaviors for every listed CR.

## CR-006
Inventory: the upstream settings, normalization, model-resolution, validation, secret-protection, connectivity, news, and Yahoo-validation sources listed in the tracker.
Reverse scan: current settings and validation services preserve the cited behavior. Two successive scans found zero missing behaviors.

## CR-007
Inventory: the upstream configuration XAML, settings view-model, ticker editors, and validation tests listed in the tracker.
Reverse scan: current Avalonia configuration behavior preserves the cited editor and validation rules. Two successive scans found zero missing behaviors.

## CR-008
Inventory: the upstream desktop app, main window, about window, and desktop-shell tests listed in the tracker.
Reverse scan: current application lifetime and shell behavior preserve the cited sources. Two successive scans found zero missing behaviors.

## CR-009
Inventory: the upstream ticker/status controls, startup coordinator, and tape tests listed in the tracker.
Reverse scan: current ticker lanes and startup ordering preserve the cited sources. Two successive scans found zero missing behaviors.

## CR-010
Inventory: the complete upstream cinematic scene, render, motion, media, graph, clock, news, configuration, and test sources listed in each tracker entry, including `docs/UPSTREAM_CINEMATIC_DISPLAY_CONTRACT.md`.
Reverse scan: the current Avalonia scene, render controllers, media services, graph/cache services, news playback, macro presentation, and configuration/news validation surfaces were each compared with their cited upstream sources. Two successive scans found zero missing behaviors for every listed CR. CR-010I's historical closure omission is covered by the tracker retrofit closure index.

## CR-010A
Inventory: upstream scene, render, media, and visual behavior sources listed in the tracker. Reverse scan: current scene/render/media behavior preserves the cited sources; two successive scans found zero missing behaviors.

## CR-010B
Inventory: upstream scene, background preload, and exchange-photo cache sources listed in the tracker. Reverse scan: current media behavior preserves the cited sources; two successive scans found zero missing behaviors.

## CR-010C
Inventory: upstream ticker and tape-animation sources and render tests listed in the tracker. Reverse scan: current ticker animation and tests preserve the cited sources; two successive scans found zero missing behaviors.

## CR-010D
Inventory: upstream startup, floating-graph, and historical-graph sources listed in the tracker. Reverse scan: current graph construction and startup behavior preserve the cited sources; two successive scans found zero missing behaviors.

## CR-010E
Inventory: upstream clock, finance-news, and global-markets sources listed in the tracker. Reverse scan: current clock/news/market behavior preserves the cited sources; two successive scans found zero missing behaviors.

## CR-011 / CR-012 / CR-013 / CR-014 / CR-015 / CR-016 / CR-017 / CR-018 / CR-019
Inventory: the upstream runtime, acceptance, settings, tests, workflow, AI-access, and configuration source sets listed in each tracker entry.
Reverse scan: current runtime, physical/hosted acceptance, test, workflow, AI-access, and settings artifacts were compared with each cited source set. Two successive scans found zero missing behaviors for every listed CR.
