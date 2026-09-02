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

# CR-015 Upstream Product Behavior Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`

This is the pre-development inventory for the line-level product-parity audit.
The cited upstream files are read from the pinned checkout; every behavior
must map to a current Avalonia 2.0 implementation or become a follow-up CR.
Installer and WPF-only artifacts are outside 2.0 scope.

## Functional Inventory

The initial product-behavior inventory is below.

| ID | Upstream source area | Functional behavior to account for | 2.0 mapping / disposition |
| --- | --- | --- | --- |
| PB-01 | `src/PortfolioSaver.Settings/Windows/MainWindow.xaml`; `src/PortfolioSaver.Settings/ViewModels/MainWindowViewModel.cs` | Settings editing, validation feedback, apply/cancel lifecycle, news mode, feed and AI fields | Avalonia `MainWindow.axaml` and settings view model; verify line-by-line under CR-019 |
| PB-02 | `src/PortfolioSaver.Core/Models/AppSettings.cs`; `src/PortfolioSaver.Core/Validation/SettingsValidator.cs` | Defaults, persistence, normalization, bounded collections, invalid-value rules | Core `AppSettings`, normalizer, validator, and three-feed contract; mapped behavior requires CR-019 audit |
| PB-03 | `src/PortfolioSaver.Presentation/Services/FinanceNewsService.cs`; `src/PortfolioSaver.Data/Services/NewsFeedValidationService.cs` | Feed retrieval, parsing, freshness, source isolation, merge/deduplication, AI fallback and degraded state | Current multi-source services and CR-014 closure evidence; mapped |
| PB-04 | `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`; `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml.cs` | Startup ordering, window initialization, provider startup and graceful shutdown | Avalonia product shell and startup services; verify ordering and cancellation under CR-021 |
| PB-05 | `src/PortfolioSaver.Render/ViewModels/NetworkWaitingViewModel.cs`; `src/PortfolioSaver.Render/Services/RenderHeartbeatService.cs` | Waiting/degraded display, render heartbeat, recovery and liveness signaling | `Render` services and product-shell degraded overlays; verify complete transitions under CR-020/CR-021 |
| PB-06 | `src/PortfolioSaver.Presentation/Services/WeatherService.cs`; global-data services | Weather, clocks, market sessions and provider failure behavior | Portable data services and ambient scene; verify business rules under CR-021 |
| PB-07 | `src/PortfolioSaver.Presentation/ViewModels/*`; `src/PortfolioSaver.Render/ViewModels/*` | Ticker lanes, graph cards, selection/ranking, movement, wraparound and scene state | Avalonia presentation/render pipeline and cinematic contract; mapped, line-level confirmation remains required |
| PB-08 | `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml.cs`; desktop input handlers | Fullscreen, restore, menu commands, keyboard/mouse interaction, multi-monitor sizing | Avalonia product shell and physical acceptance harness; mapped and physically exercised |
| PB-09 | `src/PortfolioSaver.Data/*`; `src/PortfolioSaver.VmAgent/*` | Quote provider lifecycle, bounded work, caching, protocol and helper cleanup | Portable YFinance projects and runtime process isolation; mapped, audit provider edge cases |
| PB-10 | `src/PortfolioSaver.Core/Services/*`; `src/PortfolioSaver.Presentation/Services/*` | Logging, trace names, error handling, cancellation and retained last-good state | Current bounded logging and recovery services; verify all observable paths under CR-021 |

## Required Audit Exit

The auditor must open every upstream product source file in the pinned tree,
read it line by line, and expand this inventory with line-level entries. Two
successive fresh scans must report zero unclassified behaviors. Genuine gaps
must be added as separate actionable CRs before CR-015 can close.

## Audit Record

The pinned upstream tree contains 192 `src/PortfolioSaver*` product artifacts.
The complete source-derived ledger contains 463 individually counted upstream
artifact rows, including those product artifacts and their supporting project,
workflow, test, and documentation files. Two successive fresh ledger scans
reported `UPSTREAM_GAP_SCAN=ZERO_GAPS`, with 463 files, 463 ledger rows, 463
line-by-line reads, and zero unresolved gaps. Existing intentional replacements
and implementation gaps are represented by the dependent CRs in the tracker;
installer and WPF-only artifacts remain retired from the 2.0 target.
