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

# CR-013 Upstream Behavior Inventory

## Scope

CR-013 reconciles the project documents and tracker after the Phase 3 visual
port, Phase 4 degraded-mode work, and Phase 5 publish/acceptance work. It does
not introduce a new product behavior. The authoritative documents must agree
on the Avalonia/.NET 10 architecture, six-target matrix, three physical
machines, current CR status, and remaining work.

Upstream baseline: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream/source contract behavior | DNPPV-2.0 reconciliation and evidence |
| --- | --- | --- |
| DR-01 | `README.md`, `docs/RELEASE_1_0_BASELINE.md`, and `docs/MANUAL_UI_QA_SUITE.md` define the production visualizer and its user-visible acceptance surface. | Acceptance documents identify the real production scene and do not treat a toy or fixture screen as product evidence. |
| DR-02 | `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml` and related upstream shell sources define fullscreen, menu, lifecycle, and scene ownership. | Architecture and acceptance documents describe the Avalonia-only shell and the three-machine physical evidence consistently. |
| DR-03 | Upstream feature/service sources define ticker, graph, market, weather, news, background, degraded, and recovery behavior. | The cinematic and acceptance contracts, CR inventories, and closed tracker entries use consistent feature names and closure state. |
| DR-04 | The upstream release is a desktop product whose migration must retain functionality while changing the UI framework and delivery model. | Every authoritative document states .NET 10 + Avalonia on all targets, with no WPF or Windows installer lane. |
| DR-05 | Platform execution and release behavior must cover the supported architecture matrix. | The design, test-machine record, workflow, and CR-012 evidence agree on six published RIDs and three local physical machines. |

## Exit Gate

Before documentation or tracker changes, run:

```powershell
./build/Test-MigrationBehaviorGate.ps1 -CrId CR-013 -Stage PreDevelopment
```

Closure requires two successive fresh upstream scans, a line-by-line
documentation scan, JSON/schema validation, license and syntax gates, and a
committed pushed tracker state with no contradictory remaining gaps.
