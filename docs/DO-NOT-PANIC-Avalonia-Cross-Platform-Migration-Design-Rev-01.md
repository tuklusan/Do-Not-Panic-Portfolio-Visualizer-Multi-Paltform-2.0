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

# DO NOT PANIC Avalonia Cross-Platform Migration Design Rev 01

Based on original work by Supratim Sanyal of SANYALnet Labs.

Current working date: 2026-09-01

## 1. Purpose

This document is the source-of-truth architecture baseline for migrating the
public upstream **DO NOT PANIC PORTFOLIO VISUALIZER 1.0** product into
**DO NOT PANIC PORTFOLIO VISUALIZER 2.0**, a multi-platform desktop product
implemented uniformly with **.NET 10** and **Avalonia**.

The migration target is a production application that preserves the upstream
product's user-visible functionality while removing Windows-only framework and
installer assumptions from the active 2.0 line.

## 2. Non-Negotiable Program Constraints

1. DNPPV-2.0 uses **Avalonia on every supported desktop platform**, including
   Windows.
2. DNPPV-2.0 does **not** use WPF.
3. DNPPV-2.0 does **not** ship a Windows installer as an active product lane.
   Platform publish bundles are the active delivery mechanism unless a later
   approved design revision says otherwise.
4. The migration goal is **100% preservation of upstream user-visible
   functionality** unless a later approved CR explicitly changes behavior.
5. The product remains a **desktop ambient market visualizer**, not a browser
   app, hosted web service, trading terminal, or portfolio-accounting system.
6. The locally managed loopback **YFinance.NET.Server** remains part of the
   design. DNPPV-2.0 uses the project-default local endpoint port **14871**.
7. Active DNPPV-2.0 data, cache, log, and secret storage must map to platform
   conventions through explicit abstractions rather than Windows-only path
   assumptions.
8. No migration phase closes on toy-app evidence. Closure requires evidence from
   the real DNPPV product surface.

## 3. Upstream Product Surface To Preserve

The migration program preserves these upstream 1.0 product capabilities:

1. A fullscreen-capable cinematic market-visualization window.
2. Four configurable portfolio ticker tapes with independent content and motion.
3. Floating graph cards for the largest movers with direction-sensitive motion
   and color behavior.
4. A macro-market ribbon and status panel showing broad market indicators.
5. A Global Markets lane showing exchange-local clocks, session state, and
   available weather.
6. A finance-news scroller with RSS mode by default and optional AI-summary
   mode when configured.
7. Rotating built-in and custom background imagery.
8. A settings/configuration workflow that validates symbols, feed settings, and
   optional AI connectivity before applying changes.
9. Fullscreen and window lifecycle controls, including keyboard and pointer
   toggles.
10. Logging, degraded behavior, and recovery behavior that keep the display
    alive when data sources are slow, unavailable, or partially failing.

## 4. Target Platform Matrix

The required validation and release matrix is:

- `windows-x64` via the physical Windows 10 reference machine
- `windows-x64` via the physical Windows 11 laptop
- `linux-x64` via the physical Lubuntu LXQt machine
- `windows-arm64` via GitHub-hosted `windows-11-arm`
- `linux-arm64` via GitHub-hosted `ubuntu-24.04-arm`
- `linux-x64` via GitHub-hosted `ubuntu-24.04`
- `osx-x64` via GitHub-hosted `macos-15-intel`
- `osx-arm64` via GitHub-hosted `macos-15`

Physical-machine proof is mandatory for the three SSH-accessed test machines.
GitHub-hosted runners provide the remaining multi-platform publish and smoke
coverage.

## 5. Architectural Direction

The intended 2.0 architecture is:

- portable `net10.0` class libraries for core models, services, state, data
  access, rendering logic, and settings logic;
- an Avalonia desktop shell for all product UI, including the main visualizer
  scene and configuration experience;
- explicit abstractions for platform-sensitive concerns such as local-data
  roots, secret storage, process launch, window fullscreen behavior, and test
  screenshot capture;
- a locally managed YFinance.NET server process with lifecycle isolation from
  any upstream 1.0 installation;
- validation harnesses that exercise the real product on physical machines and
  GitHub runners.

The active codebase should favor portable `net10.0` projects. Platform
differences should be isolated behind small interfaces and runtime services
rather than spread across feature logic.

## 6. Migration Phase Structure

## Phase 0 - Acceptance Baseline And Program Skeleton

Establish the migration design, upstream feature inventory, work queue,
solution skeleton, CI matrix, and platform contracts needed for the real port.

Exit criteria:

- migration design is committed and current;
- upstream user-visible workflows are inventoried and traceable;
- the JSON issue tracker contains the sequenced CR program;
- the new solution skeleton builds with .NET 10 and Avalonia on the active
  local machine;
- platform path/process/port rules are captured before feature code begins.

## Phase 1 - Portable Foundations

Recreate the shared domain, settings models, app identity, diagnostics, local
data abstractions, and YFinance server lifecycle in portable form.

Exit criteria:

- core models and service contracts exist in portable projects;
- local data roots are abstracted for Windows, Linux, and macOS;
- YFinance loopback management is portable and isolated for DNPPV-2.0;
- baseline unit tests cover the portable foundation.

## Phase 2 - Data And Configuration Port

Port quote/history/news/provider services and the configuration workflow needed
to drive the product faithfully.

Exit criteria:

- quote, history, provider-health, and news services run in the new solution;
- settings persistence and validation work in portable form;
- the Avalonia settings UI can edit and validate product configuration.

## Phase 3 - Main Visual Shell Port

Port the real DNPPV visual scene, not a toy surrogate.

Exit criteria:

- the Avalonia main window reproduces the upstream composition model;
- ticker tapes, status surfaces, graph cards, news, backgrounds, and global
  markets render from live runtime state;
- cinematic geometry, render-timed tape motion, full-scene graph physics,
  quote-driven rise/drop impulses, background cinema, and news playback satisfy
  `docs/UPSTREAM_CINEMATIC_DISPLAY_CONTRACT.md`; static presence or a fixed
  dashboard substitute is not phase-completion evidence;
- fullscreen/window controls behave correctly on supported platforms.

## Phase 4 - Runtime Fidelity And Degraded Behavior

Match upstream runtime behavior, motion, partial-failure handling, and logging.

Exit criteria:

- quote refresh orchestration and animation behavior are stable;
- degraded mode remains informative and non-destructive;
- traces/logs and screenshot capture support acceptance review.

## Phase 5 - Cross-Platform Publish And Acceptance

Publish and validate the actual product on the full platform matrix.

Exit criteria:

- the product publishes for every required RID;
- physical-machine validations pass on Windows 10, Windows 11, and Lubuntu;
- GitHub-runner publish/smoke coverage passes for macOS x64/arm64, Linux x64,
  Linux arm64, and Windows arm64;
- evidence is sufficient to claim real product parity rather than scaffold
  progress.

## Phase 6 - Release Readiness

Close remaining parity gaps, validate documentation, and prepare the project for
continued public development and later release packaging decisions.

Exit criteria:

- no open parity blockers remain for the migrated product surface;
- documentation matches the implemented architecture and workflow;
- the issue tracker reflects a reconciled state for the completed migration
  baseline.

## 7. CR Execution Rule

Every change request follows this lifecycle:

1. define the CR in `docs/AUDIT_STATE.json`;
2. deeply scan the related upstream implementation, record a source-cited
   inventory of every functional behavior, and pass the mandatory
   `PreDevelopment` migration behavior gate;
3. implement only that scoped increment;
4. run the required review and validation workflow for the increment;
5. independently rescan upstream after implementation, reconcile every behavior
   and newly discovered detail, and obtain two successive zero-gap scans;
6. pass the mandatory `Closure` migration behavior gate;
7. commit and push the reviewed result;
8. update the CR record with evidence and closure status;
9. only then begin the next CR.

The executable requirements and tracker fields for both hard gates are defined
in `docs/MIGRATION_BEHAVIOR_GATES.md`. A missing behavior restarts the complete
scan; it cannot be dismissed merely because an existing 2.0 implementation
already appears similar.

## 8. What We Will Not Do

The migration program does not:

- preserve WPF as a Windows-side host;
- preserve an Inno-style or other Windows installer as the active 2.0 delivery
  path;
- count toy applications as product-port evidence;
- claim parity from screenshots that do not show the real DNPPV experience;
- mutate the upstream 1.0 repository while using it as the migration reference.

## 9. Current Boundary

The Phase 0 through Phase 6 migration baseline is complete and recorded in
`docs/AUDIT_STATE.json`. Phase 7 begins the post-baseline configurable-news and
AI-acceptance increment. Any further implementation must begin with a newly
scoped CR, a fresh upstream inventory, and the same pre-development and closure
gates.
