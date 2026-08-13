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

# Upstream Workflow Inventory And Acceptance Baseline

Based on original work by Supratim Sanyal of SANYALnet Labs.

Current working date: 2026-08-13

## 1. Purpose

This document captures the authoritative upstream product surface that
DNPPV-2.0 must preserve during the Avalonia migration. It is the acceptance
baseline for the real application and explicitly forbids substituting toy-app
evidence for production-surface evidence.

## 2. Reviewed Upstream Evidence

The baseline in this document was derived from the public upstream repository:

- repository: `tuklusan/DO-NOT-PANIC-PORTFOLIO-VISUALIZER`
- product README
- `docs/screenshot.png`
- `docs/screenshot-release-1.0.png`
- `docs/RELEASE_1_0_BASELINE.md`
- `docs/MANUAL_UI_QA_SUITE.md`
- `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml`
- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`
- `src/PortfolioSaver.Settings/Windows/MainWindow.xaml`
- supporting project and service structure under `src/`

## 3. The Real Production Screen

The real upstream screen is not a sample panel or toy quote window. It is a
single cinematic scene with these first-viewport characteristics:

1. a full-window background city or exchange photograph dimmed behind overlays;
2. a centered branded title capsule at the top;
3. a top-left market-status block with New York status, last-updated symbol, and
   data-freshness text;
4. a top-center macro ribbon with compact market indicator cards;
5. a top-right UTC date/time block;
6. four wide horizontal ticker tapes across the middle;
7. multiple floating graph cards scattered across the scene;
8. a Global Markets strip near the bottom;
9. a red-labeled Finance News strip at the bottom;
10. footer watermark text and version text.

Migration acceptance must use this scene composition as the visual reference
point. A window with a few controls or a generic dashboard does not satisfy the
baseline.

## 4. User-Visible Workflow Inventory

## 4.1 Application launch and shell lifecycle

The user launches a desktop visualizer window that opens maximized and can be
used either windowed or fullscreen. The shell includes a menu bar with File,
View, Options, and Help entries.

Required preserved behaviors:

- startup produces the real visualizer scene, not a placeholder screen;
- the scene keeps running while quotes, backgrounds, and news fill in
  progressively;
- an Options path exists for opening configuration;
- a Help path exists for About;
- application exit is available from the menu.

Primary upstream evidence:

- `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml`
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`

## 4.2 Fullscreen and window controls

The upstream app supports normal windowed/maximized use and a fullscreen mode
optimized for ambient display.

Required preserved behaviors:

- `F11` toggles fullscreen;
- `Esc` leaves fullscreen;
- double-click toggles fullscreen;
- fullscreen removes the normal menu area while preserving the scene;
- normal maximized mode remains usable.

Primary upstream evidence:

- upstream README section "Window and Fullscreen Controls"
- `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml`

## 4.3 Status bar and macro-market ribbon

The top overlay is not decorative. It communicates scene health and broad
market context.

Required preserved behaviors:

- top-left status shows New York market/session information;
- a latest-updated symbol field exists;
- a data-freshness field distinguishes loading, live, stale, and offline cases;
- the top-right clock is pinned to UTC;
- the macro ribbon contains compact cards for indicators such as VIX, NASDAQ,
  Treasury yields, gold, crude oil, DXY, and Bitcoin.

Primary upstream evidence:

- upstream README "Macro-market ribbon"
- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`

## 4.4 Four configurable portfolio ticker tapes

Ticker tapes are a defining visual and behavioral feature.

Required preserved behaviors:

- up to four independently configured tapes are rendered;
- each tape has a user-controlled name, enabled state, direction, speed, and
  ticker list;
- quote updates visibly reflect positive, negative, and unchanged movement;
- the scene can duplicate/expand tape items to maintain visual density;
- invalid or missing data remains readable rather than collapsing the lane.

Primary upstream evidence:

- upstream README "Four customizable portfolio ticker tapes"
- `docs/MANUAL_UI_QA_SUITE.md`
- `src/PortfolioSaver.Settings/Windows/MainWindow.xaml`
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`

## 4.5 Floating graph cards

The graph cards are the other major identity feature of the product.

Required preserved behaviors:

- up to 16 graph cards can appear for selected movers;
- cards use compact floating overlays rather than large dashboard panels;
- cards reflect intraday or fallback recent-day history;
- cards visually distinguish upward and downward movement;
- motion and placement feel ambient rather than frantic.

Primary upstream evidence:

- upstream README "Floating graph cards for the biggest movers"
- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`

## 4.6 Global Markets strip

The product includes a dedicated bottom-lane global market summary.

Required preserved behaviors:

- major financial centers appear with local exchange time;
- local index name/value/direction are shown;
- session state is shown;
- weather is shown when available;
- the strip updates independently from the main quote flow.

Primary upstream evidence:

- upstream README "World markets and local conditions"
- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`

## 4.7 Finance News strip

The product includes a bottom news scroller rather than a static label list.

Required preserved behaviors:

- RSS-backed news works with no AI account by default;
- headlines advance in a ticker/scroller presentation;
- optional AI summarization exists when configured;
- writing-style options include Douglas Adams and William Shakespeare;
- the product can fall back to RSS when AI access is unavailable.

Primary upstream evidence:

- upstream README "Financial news scroller"
- `src/PortfolioSaver.Settings/Windows/MainWindow.xaml`
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`

## 4.8 Background rotation and imagery

The visual background is part of the product identity.

Required preserved behaviors:

- built-in curated backgrounds exist;
- managed/downloaded exchange or city photography can be used;
- a user can select a custom image directory instead;
- background change interval is configurable;
- the background remains full-scene and dimmed under overlays.

Primary upstream evidence:

- upstream README "Rotating financial and city backgrounds"
- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`
- `src/PortfolioSaver.Settings/Windows/MainWindow.xaml`

## 4.9 Configuration workflow

The upstream configuration surface is a real workflow, not just a settings file.

Required preserved behaviors:

- a General tab exists for background and tape configuration;
- an Advanced tab exists for news and AI-provider options;
- `Validate` is the deliberate trigger for validation rather than background
  auto-validation;
- successful validation transitions the dialog into save/close controls;
- `Cancel` abandons pending changes;
- network lockout is explicit when connectivity is required;
- a validation progress experience exists during active validation.

Primary upstream evidence:

- upstream README "Configuration"
- `docs/MANUAL_UI_QA_SUITE.md`
- `src/PortfolioSaver.Settings/Windows/MainWindow.xaml`

## 4.10 Runtime data behavior

The data path is part of the product contract.

Required preserved behaviors:

- market quotes and chart history are retrieved through the local
  `YFinance.NET` service;
- quote updates are paced one symbol at a time rather than as giant scene-wide
  refresh bursts;
- the UI progressively fills instead of blocking on the full data set;
- historical and quote caches exist;
- delayed-data messaging remains visible to the user.

Primary upstream evidence:

- upstream README "Market Data, Network Use, and Local Storage"
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`
- upstream `YFinance.net/` and `src/PortfolioSaver.Data/`

## 4.11 Degraded and offline behavior

The product is expected to remain graceful when data is incomplete.

Required preserved behaviors:

- the scene can show loading and offline states without collapsing;
- stale-data labeling exists;
- network-wait overlays are explicit when applicable;
- local cached values can still be shown in degraded operation;
- logs/traces support diagnosis of runtime problems.

Primary upstream evidence:

- upstream README degraded-state statements
- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`

## 4.12 Local storage and secrets

The product persists settings and related artifacts locally.

Required preserved behaviors:

- settings, caches, traces, and managed backgrounds persist locally;
- secrets are handled through protected local storage rather than environment
  variables for normal end-user operation;
- storage layout is product-owned and not confused with unrelated applications.

Primary upstream evidence:

- upstream README "Market Data, Network Use, and Local Storage"
- source structure under `src/PortfolioSaver.Data/` and `src/PortfolioSaver.Shared/`

## 5. Upstream Source Component Map

The upstream source is already partitioned in a way that is useful for the
migration:

- `PortfolioSaver.Core`: shared models, enums, constants, normalization,
  validation
- `PortfolioSaver.Shared`: app identity, paths, diagnostics, licensing,
  filesystem helpers
- `PortfolioSaver.Data`: quote/history/providers/settings protection
- `PortfolioSaver.Media`: background and image services
- `PortfolioSaver.Render`: ticker, graph, status, and global-market controls
- `PortfolioSaver.Presentation`: real scene composition and startup/runtime
  orchestration
- `PortfolioSaver.Desktop`: desktop shell and window lifecycle
- `PortfolioSaver.Settings`: configuration UI and validation workflow
- `YFinance.net`: local market-data service lineage

This split should guide the new Avalonia solution. The migration does not need
to preserve WPF projects, but it should preserve this functional separation as
much as practical.

## 6. Approved DNPPV-2.0 Deltas From Upstream

The migration target preserves upstream functionality while applying these
already-approved 2.0 changes:

1. Avalonia replaces WPF everywhere, including Windows.
2. The active 2.0 product does not carry a Windows-installer lane.
3. The local `YFinance.NET` loopback port is `14871`.
4. The DNPPV-2.0 default RSS feed is
   `https://www.france24.com/en/business/rss`.
5. Local data, cache, log, and secret locations become platform-appropriate
   abstractions rather than Windows-only path assumptions.

These are implementation and packaging changes, not permission to reduce the
user-visible feature set.

## 7. Acceptance Rules For Migration CRs

Every product-port CR must satisfy these rules:

1. acceptance evidence must show the real DNPPV screen or workflow being ported;
2. scaffold windows, test toys, and synthetic demo shells are never sufficient
   closure evidence;
3. visual acceptance must compare against the upstream production screen and
   workflow expectations in this document;
4. physical-machine validation is mandatory on:
   - the Windows 10 reference desktop
   - the Windows 11 laptop
   - the Lubuntu LXQt machine
5. GitHub-hosted runners extend coverage to:
   - Windows arm64
   - Linux x64
   - Linux arm64
   - macOS x64
   - macOS arm64

## 8. Immediate Migration Implication

The next active CR must create the fresh `.NET 10 + Avalonia` solution skeleton
in a way that clearly maps to the upstream product split above, because all
later work is porting the real application into that structure.
