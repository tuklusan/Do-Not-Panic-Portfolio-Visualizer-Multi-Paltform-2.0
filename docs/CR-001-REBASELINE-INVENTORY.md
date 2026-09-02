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

# CR-001 Upstream Parity Re-baseline

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

This is a fresh parity pass after CR-020. Historical closure evidence is not
accepted as proof for this pass. The upstream working copy on disk is the
authority and must be read manually, line by line, with the current 2.0
working copy inspected for the corresponding behavior and evidence.

## Functional Inventory

| BAS-01 | Upstream artifact scope | 2.0 disposition |
| --- | --- | --- |
| BAS-01 | All upstream source, test, workflow, build, documentation, configuration, and distribution artifacts in the disk scope below | Every behavior is mapped to 2.0, explicitly retired as installer/history-only, or routed to CR-021 through CR-054. The reverse scan found no unmapped behavior. |
| BAS-02 | Product shell and startup | Avalonia shell, startup lifecycle, persisted window state, native fullscreen, monitor work-area bounds, menu interaction, and Escape recovery are mapped to CR-020 and CR-045. |
| BAS-03 | Configuration and settings | Settings load/save, defaults, validation, cancellation, secret handling, legacy normalization, scene-state transfer, and responsive dialog layout are mapped to CR-030, CR-033, CR-040, and CR-042. |
| BAS-04 | Market data and orchestration | Quote polling, YFinance transport, symbol classification, exchange catalogs, benchmark mapping, NTP/network availability, cancellation, retries, and degraded fallback are mapped to CR-021, CR-022, CR-031, CR-037, CR-038, CR-039, CR-043, and CR-048. |
| BAS-05 | Cinematic scene composition | Ticker lanes, ticker motion, graph cards, critter motion, market ribbon, clock/macro overlays, background identity, layout nudges, freeze recovery, and multi-monitor behavior are mapped to CR-020, CR-044, CR-045, CR-049, and CR-052. |
| BAS-06 | News and external services | RSS configuration, feed validation, persistent news cache, playback sequencing, AI summarization, shared HTTP ownership, timeout behavior, and degraded news presentation are mapped to CR-025, CR-046, CR-047, and CR-048. |
| BAS-07 | Weather, clocks, and persistence | Weather snapshots, per-city freshness, timezone resolution, user-data paths, cache integrity, cleanup, and cross-platform persistence are mapped to CR-024, CR-032, CR-034, CR-051, and CR-053. |
| BAS-08 | Verification and delivery | Upstream tests, migration tests, reviewer gates, PowerShell syntax gates, license gates, physical harnesses, screenshot/trace artifacts, CI matrices, cleanup, and publish acceptance are mapped to CR-023, CR-036, CR-041, and CR-054. |

## Disk Scope

The fresh disk inventory recorded 465 upstream files after excluding only
`.git`, `bin`, and `obj` directories:

| Area | Files | Required treatment |
| --- | ---: | --- |
| `src` | 192 | Scan every source file and map every behavior, rule, lifecycle path, and platform assumption. |
| `tests` | 62 | Scan every test and map its asserted behavior and missing 2.0 depth. |
| `.github` | 1 | Scan workflow behavior, triggers, runner matrix, permissions, and artifact handling. |
| `docs` | 34 | Scan requirements, operating procedures, acceptance rules, and user-visible contracts. |
| `build` | 66 | Scan harnesses, gates, packaging, cleanup, and evidence behavior; disposition installer-only items explicitly. |
| Other tracked artifacts | 110 | Scan root configuration, licenses, release/distribution material, YFinance subtree, and media assets; record parity or approved non-product disposition. |

Installer/Inno-only artifacts are not product requirements for 2.0, but they
must still be scanned and explicitly marked as non-applicable rather than
silently omitted. No historical traceback is retained as a product artifact.

## Forward Scan Contract

For every scanned upstream behavior, record the exact upstream file and symbol,
the current 2.0 counterpart, the focused test, the physical acceptance
evidence, and any intentional Avalonia/.NET 10 platform adaptation. Existing
actionable findings are routed through CR-021 onward in
`docs/UPSTREAM-BEHAVIOR-GAP-ITEMS.md`; this re-baseline must confirm that list
is complete rather than assuming it is complete.

## Reverse Scan Contract

Starting from every current 2.0 source, test, workflow, documentation, and
harness artifact, ask explicitly: **IDENTIFY UPSTREAM LOGIC MISSING FROM THE
CURRENT MIGRATION**. Record all missing behavior, including timing, geometry,
multi-monitor behavior, freeze-nudge recovery, degraded paths, persistence,
cleanup, and test-depth differences. A missing map is a hard stop.

## Exit Criteria

CR-001 cannot proceed to implementation or closure until the forward and
reverse scans each have two successive zero-gap results, with complete source
lists and no unresolved dispositions recorded in `docs/AUDIT_STATE.json`.
