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

# CR-010F Upstream Behavior Inventory

**Upstream commit:** `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`

**Scope:** final cinematic-display parity reconciliation. The numeric contract
in `docs/UPSTREAM_CINEMATIC_DISPLAY_CONTRACT.md` is derived from, but does not
replace, the source inventory below.

## Source Scan

The pre-development scan read the complete upstream scene composition and
followed its control, view-model, service, timer, recovery, and refresh paths:

- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`
- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml.cs`
- `src/PortfolioSaver.Render/Controls/StatusBarControl.xaml`
- `src/PortfolioSaver.Render/Controls/TickerTapeControl.xaml` and `.xaml.cs`
- `src/PortfolioSaver.Render/Controls/FloatingGraphControl.xaml` and `.xaml.cs`
- `src/PortfolioSaver.Render/Controls/GlobalMarketsTapeControl.xaml` and `.xaml.cs`
- `src/PortfolioSaver.Render/Controls/NewsFlasherControl.xaml` and `.xaml.cs`
- `src/PortfolioSaver.Render/Services/HistoricalGraphBuilder.cs`
- `src/PortfolioSaver.Render/ViewModels/FloatingGraphViewModel.cs`
- `src/PortfolioSaver.Render/ViewModels/MacroMeterViewModel.cs`
- `src/PortfolioSaver.Render/ViewModels/StatusBarViewModel.cs`
- `src/PortfolioSaver.Media/Services/BackgroundImageService.cs`
- `src/PortfolioSaver.Media/Services/BackgroundPreloadService.cs`
- `src/PortfolioSaver.Desktop/Assets/ExchangeBackgrounds/exchange-photo-attribution.txt`

## Functional Inventory

| ID | Upstream behavior and authority | DNPPV-2.0 implementation target | Required evidence |
|---|---|---|---|
| SC-01 | `VisualizerSceneControl.xaml`: full-window scene, 16-pixel inner margin, independent title/watermark overlays, explicit z-order | `ProductShellWindow.axaml` scene root and overlays | XAML audit; 1024-or-wider screenshots |
| SC-02 | Same XAML: status at top offset 42, four tapes at offset 188, markets/news at bottom | Avalonia row composition with matching offsets | geometry inspection and screenshots |
| SC-03 | `VisualizerSceneControl.xaml.cs`: fullscreen/maximize/restore/resize preserve and clamp sprite positions | viewport callbacks and graph/global/news reconfiguration | resize/fullscreen captures and controller tests |
| SC-04 | network-unavailable startup/runtime state shows the upstream branded waiting overlay with bounce motion while retained/cached scene data remains stable | accessible Avalonia waiting overlay driven by runtime connectivity state | offline fixture, motion trace and screenshot |
| SC-05 | title/version watermark and footer attribution remain independent, explicitly ordered overlays; the dormant market-critter code path remains disabled upstream and is not an active product requirement | separate title/version/footer surfaces; no active critter animation | XAML/source audit and screenshot |
| BG-01 | `RotateBackgroundAsync`, `LoadBackgroundAsync`: inactive-layer preparation, committed old frame during decode/failure, cross-fade after readiness | two Avalonia image layers with asynchronous frame preparation and retained committed source | transition trace, malformed-source test/run, timed captures |
| BG-02 | `CalculateBackgroundPresentationOpacity`: sample to 48 pixels, BGRA luminance weights and thresholds | `BackgroundPresentationOpacityPolicy` and async loader | deterministic threshold/pixel tests and committed-opacity trace |
| BG-03 | background animation helpers: `Stretch=Fill`; zoom every 120 ms, 1.00-1.05, step .00075, reverse at limits | `BackgroundCinemaController` and full-screen layers | elapsed-time controller tests and timed screenshots |
| BG-04 | background preload/load paths: cancellation, stale-load rejection, attribution, missing-source recovery, isolated errors | generation-checked cancellable loader, active attribution, trace, retained frame | trace and shutdown/process audit |
| BG-05 | default rotation baseline is five minutes; large images are downscaled during decode; catalog changes rotate only when required and preserve a still-valid current image while choosing a different next image | bounded decode, catalog identity comparison and source-validity rotation policy | deterministic catalog/decode tests and trace |
| ST-01 | `StatusBarControl.xaml`: 92-pixel bar; left stack max 248; updated field 222; right clock min 102 | matching Avalonia status surface | XAML audit and narrow-screen review |
| ST-02 | `UpdateStatusMacroMeters`/`EnsureMacroMetersInitialized`: eight meters VIX, NASDAQ, UST10Y, UST3M, GOLD, CRUDE (`BZ=F`), DXY, BTC | eight independently refreshed macro quote meters | provider test/trace and screenshot |
| ST-03 | `MacroMeterViewModel`: 96x50 cards, 210-degree start, 240-degree arc, normalized maxima, needle; VIX/DXY risk colors inverted; stale is gold | portable path geometry and color/fill logic | deterministic view-model tests and screenshot |
| ST-04 | status update paths: market/session, last symbol, live/delayed/missing state, UTC date/time | scene status properties and independent macro loop | state tests, live/degraded traces |
| TK-01 | `TickerTapeControl`: exactly four configured lanes, row 56, inner viewport 28, fixed symbol/value/change/separator geometry | `TickerLaneViewModel` and Avalonia lane template | geometry/model tests and screenshot |
| TK-02 | ticker render callback: direction, per-lane speed `max(18,72*speed)`, upstream default multipliers, ~30 FPS, elapsed capped at 100 ms | independent 33 ms ticker loop and `TickerMotionController` | elapsed-time/direction/speed tests and displacement trace |
| TK-03 | sequence construction/wrap: enough duplicated content in both directions for continuous viewport coverage | viewport-relative copies and stable cycle anchor | narrow/wide coverage and wrap tests |
| TK-04 | quote application: mutate matching quote models without rebuilding track; trend, loading, missing and flash cues | stable `TickerQuoteViewModel` objects and visual state | identity/update/cue tests and live trace |
| TK-05 | empty/unmeasured lanes remain stopped; measured lanes start; data transitions restart; unload prevents restart; cached fixed-width measurement controls the exact cycle distance | explicit tape lifecycle and cached cycle metrics | lifecycle and cycle-distance tests |
| TK-06 | runtime dispatch is one quote per second, ignores legacy visual refresh sliders for dispatch cadence, applies freshness policy separately, and slows only recently fetched closed world-market symbols | independent interleaved scheduler and market-session-aware eligibility | scheduler-order/cadence tests and trace |
| GR-01 | `SelectGraphTickerPairs`: up to 16 unique cards ranked globally by available absolute movement, then configured group/ticker order; the retained per-tape-cap setting is not used; structural hydration suppresses impulses | graph refresh loop and keyed content replacement | graph-count/ranking trace and selection tests |
| GR-02 | `FloatingGraphControl.xaml`: card 186x78, plot 132x40, value/percent, min/mid/max and time labels | matching Avalonia card template | builder tests and screenshots |
| GR-03 | `HistoricalGraphBuilder`: green rising and red falling segments; latest segment emphasized and trend-colored | per-segment path collections and 3-pixel latest path | deterministic mixed-history test and screenshot |
| GR-04 | placement/motion paths: viewport-relative collision-aware seed, independent randomized X/Y at configured 22-48 defaults, 20 FPS, 100 ms cap, edge bounce | `FloatingGraphMotionController` and 50 ms ambient loop | bounds/collision/elapsed-time tests and motion trace |
| GR-05 | refresh preserves X/Y and nominal velocities for same keyed card; resize clamps without reseeding | `CopyContentFrom` plus viewport configuration | identity/layout preservation tests |
| GR-06 | `ApplyRefreshMotionCue`/impulse reset: positive to top, negative to bottom, X=0, min 260 px/s, ~1.4 s target, 4 s timeout, resume away from edge | directional refresh travel state machine | deterministic crowded-scene test and physical impulse fixture |
| GR-07 | `FloatingGraphControl.xaml.cs`: ordinary flash at 180/620/980/1680 ms; sustained travel flash auto-reverses every 220 ms and ends by four seconds | portable opacity state machines using shared overlay | key-time tests and impulse screenshot/trace |
| GR-08 | stale quotes retain last-known mover/value; first live value flashes; raw price changes drive impulses while percent-only/fetch-token-only changes do not; stale-to-live card recovery flashes without causing ticker hydration flash | quote-cue decision policy separated from content update | deterministic transition matrix tests |
| GR-09 | graph rebuilds progressively reuse history/build cache, preserve in-flight warmup across refresh ticks, and batch synchronous layout work | keyed history/build cache and bounded warmup coordinator | cache/warmup tests and startup trace |
| GM-01 | `FloatingClockBuilder`/`GlobalMarketsTapeControl`: local summary plus 18 exchange cards and flags; pinned 150-pixel New York/NASDAQ card; remaining 164x54 cards in a 68-pixel clipped continuous lane with copies, edge treatment and shrouds | complete center catalog, pinned card and `GlobalMarketsMotionController` lane | catalog/geometry/wrap tests, displacement trace, screenshots |
| GM-02 | world refresh paths: local clocks, exchange/index, direction, Yahoo timing/calendar session and countdown, weather, and recent NTP offset; failures isolated | global market models and independent quote/calendar/weather/NTP loops | live and degraded traces |
| GM-03 | clocks tick each second while ancillary market redraws are throttled; pinned New York status uses the exchange calendar when quote session state lags and renders a placeholder when calendar data is absent | split clock/display refresh and calendar-authoritative pinned status | timing/calendar tests and trace |
| NW-01 | `NewsFlasherControl`: telegraph preparation/type, pre-scroll pause, vertical scroll, post-scroll pause and next-headline transition | `NewsPlaybackController` and independent 40 ms loop | phase tests and phase/headline trace |
| NW-02 | news refresh: configured RSS default, optional styled summary, RSS fallback, playback independent of source choice/failure | `FinanceNewsService` and scene news loop | RSS/service tests and live/degraded traces |
| NW-03 | headline bursts debounce to one restart; unload cancels pending restart; equivalent speed refresh preserves index; changed headlines reset safely; pre-layout waits, viewport recovery restarts the current headline, explicit line breaks survive, prior bottom line carries without retyping, and width measurements are cached/cleared | viewport-aware, cancellable playback continuity state | deterministic playback transition tests |
| NW-04 | optional AI news fetches configured feeds in parallel with partial-feed tolerance; fences untrusted headlines; resolves OpenRouter free instruct models; enforces timeout/retry budgets; parses strict/compatible structured responses; caches by writing style/contract; appends required style quotation; and distinguishes RSS-backed, structured, credential, HTTP, malformed, timeout and no-feed fallbacks | full portable structured-news service contract | service matrix tests with controlled HTTP responses |
| LC-01 | loaded/unloaded/settings lifecycle: start only while live; pause hidden/settings work; resume with bounded first frame; detach/cancel on shutdown | scene pause/resume/disposal and window subscriptions | lifecycle tests and zero-process audit |
| LC-02 | render heartbeat: 30 s trace, 10 s startup grace, missing after 5 s, recovery no more often than 30 s, max three per episode, recovered trace | `RenderSurfaceHeartbeatController`, watchdog and invalidation callback | deterministic clock tests and heartbeat fixture trace |
| LC-03 | all background/quote/history/weather/market/news paths isolate failures so unrelated motion continues | lane-specific catches and fallback state | controlled fault evidence and continuing displacement |
| LC-04 | scene trace emits bounded displayed-tape samples/lanes for soak comparison in addition to render heartbeat/recovery events | structured sampled diagnostics through the capped trace lane | trace schema/rate test and physical soak artifact |

## Pre-Development Reconciliation

The scan found and corrected these gaps before the gate pass:

1. status/macro geometry had been split into two bands;
2. the product title was not an independent overlay;
3. ticker row/viewport geometry was implicit;
4. background luminance and asynchronous retained-frame loading were absent;
5. active image attribution was absent;
6. ticker/news scheduling shared the graph loop;
7. render heartbeat/recovery was absent;
8. historical graphs used one aggregate trend color rather than per-segment color;
9. the ordinary multi-pulse graph flash was absent; and
10. the local/contract macro list incorrectly had seven text-only meters instead
    of the upstream eight gauge meters;
11. network-waiting overlay motion and independent title/version/footer behavior
    were not explicit;
12. background catalog refresh/downscale/default cadence behavior was not
    explicit;
13. ticker lifecycle, measurement, interleaved dispatch and closed-market
    scheduling were not explicit;
14. stale/initial graph cue decisions and graph warmup caching were not
    explicit;
15. pinned-market calendar authority and ancillary redraw throttling were not
    explicit;
16. news debounce, viewport continuity and the complete structured-AI fallback
    matrix were not explicit; and
17. bounded displayed-tape soak sampling was not explicit.

Every discovered item is now mapped above. No known behavior is omitted from
the CR scope. This is the pre-development inventory only; it cannot satisfy the
mandatory fresh closure audit.
