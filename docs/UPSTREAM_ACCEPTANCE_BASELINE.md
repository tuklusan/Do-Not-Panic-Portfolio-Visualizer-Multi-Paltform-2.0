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

Current working date: 2026-09-01

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

Exact geometry, timing, motion, impulse, collision, background, and playback
requirements are defined in `docs/UPSTREAM_CINEMATIC_DISPLAY_CONTRACT.md` from
the upstream source. This baseline and that numeric contract are jointly
mandatory; screenshots alone cannot establish dynamic parity.

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

- a product-specific single-instance guard blocks a duplicate launch and shows
  the user that the visualizer is already active;
- startup produces the real visualizer scene, not a placeholder screen;
- the scene keeps running while quotes, backgrounds, and news fill in
  progressively;
- an Options path exists for opening configuration;
- a Help path exists for About, including product/publisher/author/license
  metadata, transparent-corner brand artwork, suitable platform icon sizes, and
  the complete bundled/project license text;
- application exit is available from the menu;
- startup and shutdown own the local YFinance process and release the
  single-instance guard without affecting an upstream 1.0 process;
- non-Debug packaged startup validates the release manifest asynchronously,
  bounds reported integrity errors, and visibly reports a missing, malformed,
  escaped, size-mismatched, or checksum-mismatched release file.

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
- the macro ribbon contains eight compact gauges for VIX, NASDAQ, UST10Y,
  UST3M, gold, Brent crude, DXY, and Bitcoin, including normalized arcs,
  needles, stale state, and inverse risk colors for VIX and DXY;
- stable dash placeholders and public automation identifiers keep loading,
  live, stale, offline, and missing states accessible and testable.

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
- invalid or missing data remains readable rather than collapsing the lane;
- waiting and missing glyphs remain distinct, track position survives quote
  updates, and empty/unmeasured/data-transition/unload states start or stop
  animation without stale subscriptions;
- cached fixed-width measurements define wrap distance, and runtime quote
  dispatch remains one interleaved symbol per second with separate freshness
  and closed-world-market slowdown policy.

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
- motion and placement feel ambient rather than frantic;
- stale cards retain last-known values; first-live and stale-to-live transitions
  flash, while percent-only or fetch-token-only changes do not create a price
  impulse;
- graph history/build caches and in-flight progressive warmup survive refreshes;
- fallback order includes current-session points seeded from previous close,
  the latest five distinct exchange-local daily closes, and quote-memory
  fallback; labels use exchange-local intraday/day values and the keyed graph
  cache is case-insensitive, collision-safe, change-sensitive, and bounded LRU.

Primary upstream evidence:

- upstream README "Floating graph cards for the biggest movers"
- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`

## 4.6 Global Markets strip

The product includes a dedicated bottom-lane global market summary.

Required preserved behaviors:

- major financial centers appear with local exchange time;
- one local-desk summary plus all 18 upstream exchange cards and their bundled
  flags are represented;
- local index name/value/direction are shown;
- session state uses exchange timing/calendar data, including holidays and
  pre/post-market windows where available;
- weather is shown when available;
- weather fetches cities with bounded parallelism, uses cached/offline fallback,
  omits uncached failed cities, trims unrequested stale entries, and releases
  concurrency gates on cancellation;
- NTP-adjusted UTC is used while a recent synchronization remains valid;
- the strip updates independently from the main quote flow;
- clocks remain live at one-second cadence while ancillary market redraws are
  throttled, and pinned New York uses calendar truth when quote session state
  lags.

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
- summarized mode gathers its upstream multi-feed context, treats headlines as
  untrusted input, produces the structured style-specific item format and
  closing quotation, retries within a bounded budget, and caches by mode/style;
- summarized-news refresh has a 30-minute minimum even when legacy settings
  request a shorter cadence;
- invalid credentials use plain RSS fallback, while transient/empty/malformed
  AI results use the upstream structured local fallback and remain retryable on
  the next refresh;
- feed fetches run in parallel with partial-feed tolerance, and playback
  preserves debounce, unload cancellation, equivalent-refresh index,
  pre-layout/viewport recovery, explicit line breaks, prior-line carry, and
  cached width measurement behavior;
- startup preserves the actual headline count without synthetic duplication,
  shows the waiting message for an empty set, and emits a style closing quote
  only once per playback sequence.

Primary upstream evidence:

- upstream README "Financial news scroller"
- `src/PortfolioSaver.Settings/Windows/MainWindow.xaml`
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`

## 4.8 Background rotation and imagery

The visual background is part of the product identity.

Required preserved behaviors:

- built-in curated backgrounds exist;
- managed/downloaded exchange or city photography can be used;
- the managed cache warms asynchronously from the upstream manifest, validates
  completed images, removes stale partial downloads, and records full/footer
  attribution;
- a user can select a custom image directory instead;
- custom image discovery includes subdirectories, while the managed-cache path
  remains visible and read-only in configuration;
- background change interval is configurable;
- the default interval is five minutes;
- large images decode to a bounded width, and catalog changes retain a valid
  current frame while selecting a different next image when possible;
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
- General exposes bounded portfolio/off-hours refresh and background-interval
  sliders plus the YFinance.NET-only runtime summary; provider secret fields
  remain on Advanced only;
- each tape exposes name, enabled state, direction, speed, add/remove ticker,
  and each ticker exposes symbol, display name, enabled state, validation
  badge/message, and removal;
- editor/model conversion enforces the upstream maximum ticker count per tape;
- ticker display names are provider-filled and read-only; unused benchmark,
  asset-class, exchange, currency, and provider-id editors are not exposed;
- key sections have nonempty help tooltips, both tabs remain scrollable and
  responsive at supported sizes, and the validated footer keeps primary
  `OK`/`Cancel` actions visible;
- configuration text remains visually intact on every target; upstream's
  WPF-specific software-rendering workaround is migrated as that behavioral
  outcome, not as a Windows-only rendering implementation;
- news mode controls enable only their applicable RSS or AI-style fields;
- `Validate` is the deliberate trigger for validation rather than background
  auto-validation;
- successful validation transitions the dialog into save/close controls;
- `Cancel` abandons pending changes;
- network lockout is explicit when connectivity is required;
- a validation progress experience exists during active validation.
- validation performs a fresh connectivity probe, validates RSS or optional AI
  access before symbols, handles rate-limited symbols as deferred, and
  preserves trusted symbol profiles;
- AI validation uses the explicitly configured key (never an environment-key
  fallback), validates compatible chat completions, treats rate limiting as a
  deferred/transient result, reports malformed endpoints and timeouts, and
  avoids repeating a still-valid probe for unchanged saved AI settings;
- successful validation changes the primary actions to `OK` and `Cancel`, and
  any persisted edit invalidates the validated snapshot;
- invalid RSS resets to the project default only in RSS mode, summarized mode
  does not require a valid RSS URL, an offline check does not overwrite the
  configured RSS URL, and invalid symbols are visibly disabled;
- apply saves settings, publishes validated quote seeds, and requests close;
  cancellation publishes neither candidate settings nor quote seeds and does
  not surface cancellation as an unexpected error;
- the scene pauses throughout the configuration session and resumes after the
  single owned configuration window closes;
- normalization deep-clones the settings graph, caps four tapes and eight
  symbols per tape, restores approved defaults for an empty portfolio, preserves
  explicit direction/speed choices, clears placeholder secrets, defaults RSS
  and AI fields, and canonicalizes a chat-completions URL to its provider base.

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
- the configuration runtime summary states the one-second pacing and ten-minute
  cache/metadata freshness ceiling;
- the UI progressively fills instead of blocking on the full data set;
- historical and quote caches exist;
- cache defaults remain ten minutes, expired memory entries are removed with
  bounded LRU eviction, and failed live history may fall back to stale cache;
- provider parsing distinguishes null/empty, malformed, and explicit upstream
  errors; quote change/percentage values are normalized from previous close;
- partial quote responses preserve every resolved requested symbol, distinguish
  partial compatibility exceptions from total failure, deduplicate input, and
  propagate intentional cancellation;
- authentication/crumb failures alone refresh the Yahoo session, rate limits
  honor Retry-After or exponential backoff, and transient server errors retry
  only within a bounded budget;
- shared client access, in-flight timeout/stale completion, recovery reset,
  shutdown, and cancellation are serialized without blocking the UI thread;
- the YFinance lane writes its own bounded redacted circular trace and performs
  a nonfatal, cancellable, disableable upstream-sync metadata check;
- initial runtime order is macro symbols, world-market symbols, then configured
  tape symbols, and validated quote seeds are consumed once;
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
- the network-wait overlay preserves upstream branding and bounce motion;
- local cached values can still be shown in degraded operation;
- logs/traces support diagnosis of runtime problems.
- structured trace fields redact secrets, writes are asynchronous and bounded,
  and circular/capped logs recover from corrupt cursors or write failures;
- burst trace writes retain order, avoid per-line disk sync, restart their
  worker after loop failure, and resolve network metadata lazily rather than in
  static initialization;
- recovery-state storage prefers the product data root, falls back through
  writable platform locations, and still returns an absolute last-resort path;
- an abnormal prior render run can select a software-rendering recovery mode,
  while a clean run returns to the normal hardware path;
- bounded displayed-tape samples and lane traces support soak comparison.

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

The completed baseline uses the `.NET 10 + Avalonia` solution skeleton
in a way that clearly maps to the upstream product split above, because all
later work is porting the real application into that structure.
