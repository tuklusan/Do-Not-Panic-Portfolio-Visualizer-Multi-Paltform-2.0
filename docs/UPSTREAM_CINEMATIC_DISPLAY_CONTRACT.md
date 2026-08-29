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
Based on original work by Supratim Sanyal of SANYALnet Labs.
-->

# Upstream Cinematic Display Contract

## Authority

This contract records the cinematic behavior that DNPPV-2.0 must migrate from
upstream DNPPV-1.0. The audit source is upstream commit `2e2fab0`, inspected on
2026-08-20. Numeric behavior must be ported from these sources instead of being
re-designed from screenshots:

- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`
- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml.cs`
- `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs`
- `src/PortfolioSaver.Render/Controls/TickerTapeControl.xaml(.cs)`
- `src/PortfolioSaver.Render/Services/TapeAnimationController.cs`
- `src/PortfolioSaver.Render/Controls/FloatingGraphControl.xaml(.cs)`
- `src/PortfolioSaver.Render/Services/FloatingSpriteMotionController.cs`
- `src/PortfolioSaver.Render/ViewModels/FloatingGraphViewModel.cs`
- `src/PortfolioSaver.Render/Controls/GlobalMarketsTapeControl.xaml(.cs)`
- `src/PortfolioSaver.Render/Controls/NewsFlasherControl.xaml(.cs)`
- `src/PortfolioSaver.Render/Controls/StatusBarControl.xaml`
- `docs/MANUAL_UI_QA_SUITE.md`
- `docs/screenshot-release-1.0.png`

The present Avalonia scene is integration scaffolding until every mandatory
behavior below is implemented and dynamically accepted. A still screenshot is
not evidence that motion behavior passes.

## Scene Composition

- The scene is a full-window cinematic canvas with 16-pixel outer margins.
- Floating graphs occupy a full-scene overlay spanning status, center, and
  bottom rows. They are not confined to a dashboard grid or graph-only band.
- The status/macro surface sits at the top with a 42-pixel top offset.
- Four ticker tapes occupy the center scene with a 188-pixel top offset.
- Global markets and finance news form a bottom vertical overlay.
- The centered product identity, version, copyright/image attribution, and
  delayed-data notices remain overlay watermarks with explicit z-order.
- An unavailable-network state uses the branded bouncing waiting overlay while
  any retained scene data remains stable. Upstream's market-critter path is
  compiled but explicitly disabled and is not an active parity requirement.
- The scene scales from actual viewport dimensions. Fixed design-canvas widths
  must not clip supported 1024x768 or larger displays.

## Background Cinema

- Two full-screen image layers use `Stretch=Fill`, matching upstream's explicit
  distortion-over-crop decision.
- Rotation cross-fades the inactive and active layers and preserves a committed
  fallback source during loading or recovery.
- Backgrounds use luminance-sensitive presentation opacity and the configured
  dim overlay.
- Slow zoom runs every 120 ms between scale `1.00` and `1.05` in steps of
  `0.00075`, reversing at each limit.
- Rotation, transition, decode, cancellation, attribution, and missing-source
  recovery remain non-blocking and traceable.
- The default rotation interval remains five minutes. Large images are decoded
  to a bounded width; changed catalogs rotate only as needed, preserve a valid
  current image, and select a different next image when possible.

## Status And Macro Surface

- The status surface has a minimum height of 92 pixels.
- It preserves market/session state, last-updated ticker, freshness/network
  state, UTC/date information, and the eight upstream macro meters: VIX,
  NASDAQ, UST10Y, UST3M, GOLD, CRUDE, DXY, and BTC.
- Macro meters preserve their compact `96x50` geometry and independent refresh
  lane. Macro refresh is not coupled to the four portfolio tape animations.
- The left status stack is capped at 248 pixels, the last-updated field reserves
  222 pixels, and the right clock card has a 102-pixel minimum width. These
  constraints keep the centered macro meters stable during text changes.
- Missing, stale, or loading data remains visually explicit.

## Portfolio Ticker Tapes

- Exactly four configured tapes appear with their configured direction and
  independent speeds. Default row height remains 56 pixels.
- Each tape keeps the upstream `8,3` outer margin, `9,4` outer padding, and
  7-pixel corner radius. Its title badge is vertically centered with `7,2`
  padding, a 10-pixel right margin, `Consolas` 12-point semibold text, and the
  upstream border and foreground colors.
- The visible ticker viewport is 28 pixels high, vertically centered, and has
  the upstream `4,0,4,0` margin inside the outer tape border.
- Each ticker sequence uses fixed symbol, value, change, gap, and separator
  geometry from `TickerTapeControl` rather than ad hoc text spacing. Symbol,
  last-value, and change-value text uses `Consolas` 15-point text (bold for
  symbols; semibold for values); the waiting glyph remains the sole emoji-font
  exception.
- The product deliberately uses mixed typography. Ticker and compact numerical
  data surfaces are monospaced; product identity and editorial/news typography
  retain their separately specified upstream faces. No global monospaced-font
  override is a migration requirement.
- Content is duplicated enough to cover the viewport and provide a seamless
  cycle in either direction.
- Motion is elapsed-time based, driven by the render surface, and throttled to
  roughly 30 FPS. The first resumed frame is bounded and hidden time does not
  cause a jump.
- Speed is `max(18, 72 * configured tape speed)` pixels per second. Default
  speeds derive from `0.45` using the upstream lane multipliers.
- A raw quote update surgically updates the matching displayed items without
  rebuilding or resetting the tape track.
- Quote-change cues, loading glyphs, missing-data glyphs, and trend colors are
  preserved.
- Empty or unmeasured tracks stay stopped; measured tracks start, data changes
  restart safely, unload prevents restart, and cached fixed-width measurement
  determines the exact cycle distance.
- Runtime quote dispatch remains one interleaved quote per second. Visual
  refresh settings govern freshness rather than dispatch cadence, and only
  recently fetched closed world-market symbols receive the closed-market
  slowdown.

## Floating Graph Cards

- Up to 16 unique selected mover cards appear, ranked globally by available
  absolute quote movement with configured tape and ticker order as stable
  fallback ordering. The retained `MaxFloatingGraphsPerTape` settings field is
  not applied by the current upstream selector and must not be invented as an
  active per-tape cap.
- Each card is `186x78`; each plot is `132x40`. Scale labels, time labels,
  green/red historical segments, and the emphasized latest segment remain.
- Cards are compact overlays on the full cinematic scene, not fixed grid cells.
- Initial placement is viewport-relative, collision-aware, and bounded.
- Nominal X and Y velocities independently use the configured range, whose
  defaults are 22 to 48 pixels per second, with randomized signs.
- Frame motion runs at 20 FPS using elapsed time capped at 100 ms. Cards bounce
  at viewport edges when enabled.
- Collision resolution separates intersecting cards, reverses the relevant
  velocities, clamps the result to safe bounds, and performs bounded passes.
- Layout and nominal motion survive graph refreshes where the symbol remains.
- On a raw price change, the card flashes with the trend color. A positive
  percentage sends it toward the top edge; a negative percentage sends it
  toward the bottom edge.
- Refresh travel uses zero horizontal velocity and at least 260 pixels per
  second vertically, targets approximately 1.4 seconds, and times out after
  four seconds. At the target edge it restores nominal X motion and reverses
  nominal Y away from that edge.
- A normal card flash follows the upstream multi-pulse sequence at approximately
  180, 620, 980, and 1680 ms. Refresh travel uses a sustained 220 ms
  auto-reversing pulse capped at four seconds.
- Initial hydration and structural graph replacement suppress false quote-change
  impulses.
- Stale quotes preserve last-known mover values. First-live and stale-to-live
  graph transitions flash, while percent-only or fetch-token-only changes do
  not create a raw-price impulse; ticker stale-to-live hydration does not flash.
- Graph history/build caches and in-flight warmup survive refresh ticks and
  batch synchronous layout work.

## Global Markets And News

- The global-market lane preserves the local-desk summary and all 18 upstream
  exchange centers, bundled flags, local exchange clocks, index
  value/direction, exchange-calendar session state, and weather, with its own
  refresh lane and recent NTP offset when available.
- It remains a continuously animated tape, not eight static squeezed cells.
  New York/NASDAQ is pinned in a 150-pixel card; the other 164x54 cards move
  through a 68-pixel clipped viewport with duplicated sequences, edge fades,
  and shrouds. It must fit supported viewport widths without clipping the pinned
  card, viewport, or text.
- Clocks tick every second without forcing every ancillary market redraw.
  Pinned New York status uses exchange-calendar truth when quote-session state
  lags and shows an explicit placeholder if the calendar is unavailable.
- Finance news preserves the upstream telegraph-style headline state machine:
  preparation, pause before scroll, vertical scrolling, pause after scroll, and
  transition to the next headline.
- RSS remains the default source. Optional styled AI summaries and RSS fallback
  remain supported without changing playback behavior.
- Playback debounces headline bursts, cancels pending restarts on unload,
  preserves the index for equivalent refreshes, recovers the current headline
  after viewport readiness, preserves explicit line breaks, carries the prior
  bottom line without retyping, and caches width measurements.
- Optional AI news fetches feeds in parallel with partial-feed tolerance,
  fences untrusted headline text, resolves applicable OpenRouter models,
  enforces retry/timeout budgets, parses structured responses, keys caches by
  writing style and response contract, and distinguishes credential, HTTP,
  malformed, timeout, RSS-backed, structured, and no-feed fallbacks.

## Lifecycle And Recovery

- Cinematic timers and render subscriptions start only while the scene is live,
  pause during validation/settings transitions, and are detached at shutdown.
- Render motion uses a heartbeat, startup grace period, bounded recovery
  attempts, and trace events for missing or recovered render callbacks.
- Bounded displayed-tape sample and lane traces support soak comparison without
  turning frame-rate work into unbounded diagnostics.
- Fullscreen, maximize, restore, and viewport resize preserve moving positions
  where possible and clamp every overlay to the new safe bounds.
- Background, quote, history, weather, market, and news failures degrade their
  own surfaces without stopping unrelated animation.

## Acceptance Evidence

Each cinematic CR requires all of the following:

1. deterministic tests for geometry, elapsed-time travel, wrapping, bouncing,
   collision handling, quote impulses, and lifecycle cleanup;
2. mandatory code review from a clean staged snapshot;
3. a clean pushed checkpoint before validation;
4. successful self-contained publishes for all six target RIDs;
5. visible runs on Lubuntu, Windows 10, and Windows 11;
6. at least two timestamped captures or a short capture sequence proving actual
   ticker/card displacement, not merely their presence;
7. a controlled quote-change fixture proving a rise travels upward, a fall
   travels downward, flashes occur, and nominal wandering resumes;
8. screenshot review at the smallest physical viewport and a wider viewport;
9. trace and process-cleanup review; and
10. mandatory TEST_ARTIFACT second-opinion review before closure.

## Current Gap Disposition

CR-010 remains open. The 2026-08-20 integration screenshots prove live data,
backgrounds, graphs, markets, weather, clocks, and RSS rendering, but do not
prove cinematic parity. The fixed-grid graph experiments and static ticker
lanes are rejected as closure evidence. Phase 3 cannot close until the CR queue
implements and accepts this contract.
