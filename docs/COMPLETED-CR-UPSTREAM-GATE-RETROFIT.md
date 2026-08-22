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

# Completed-CR Upstream Gate Retrofit

**Upstream authority:** `upstream/main` at
`2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`

**Purpose:** apply the mandatory migration behavior gates retrospectively to
CR-001 through CR-010E. A prior build, screenshot, review PASS, or physical run
does not override a source-confirmed missing behavior.

## Audit Method

Pass 1 followed production entry points through their callers, view models,
services, state transitions, failure handling, timers, persistence, and
shutdown paths. Pass 2 independently scanned the related upstream tests and
manual acceptance requirements. File-name comparison alone was used only to
locate candidates; every finding below was confirmed in source or tests.

The pre-development inventory is complete when all related behaviors are
listed, even if implementation gaps remain. A closure gate passes only when a
fresh current-snapshot audit finds no unresolved behavior.

## CR-001 - Acceptance Baseline

Sources: upstream `README.md`, `docs/RELEASE_1_0_BASELINE.md`,
`docs/MANUAL_UI_QA_SUITE.md`, desktop/application entry points, settings UI,
`StartupCoordinator`, `VisualizerSceneControl`, and the render controls.

Inventory: launch/single-instance notice; shell/menu/about; fullscreen and
pointer controls; progressive startup; four tapes; eight macro gauges; mover
graphs; local plus 18 exchange summaries; weather/NTP/calendar state; RSS and
structured optional-AI news; built-in/custom/managed backgrounds; validated
configuration; local YFinance; caches/secrets; degraded UI; diagnostics and
render recovery.

Pass-1 gaps: the local baseline omitted single-instance behavior, exact macro
count/gauges, the complete exchange catalog, NTP/calendar semantics, managed
background warmup, detailed AI-news fallback/cache behavior, and bounded
diagnostics/render recovery. These are now added to
`docs/UPSTREAM_ACCEPTANCE_BASELINE.md`. Closure requires the second zero-gap
documentation scan.

## CR-002 - Portable Solution Skeleton

Sources: upstream solution/project dependency graph and desktop/settings/render
project boundaries; local migration design's approved Avalonia-only delta.

Inventory: .NET 10 build; portable Core/Data/Media/Presentation/Render
boundaries; one Avalonia desktop host; test project; no WPF or installer in the
active solution; six RID-capable dependency graph.

Disposition: no missing product behavior belongs to this structural CR. The
absence of WPF and installer projects is an approved migration delta. Candidate
for retrospective closure PASS after pass 2.

## CR-003 - Identity, Storage, Process Isolation, Loopback

Sources: `PortfolioSaver.Shared/AppIdentity.cs`,
`Helpers/AppDataRootResolver.cs`, desktop `App.xaml.cs`,
`YFinanceServerProcessManager.cs`, and YFinance protocol/server constants.

Inventory: distinct 2.0 product/storage/single-instance identity; upstream-equivalent
Windows session scoping; Windows/Linux/macOS
data/cache/log/secret roots; absolute override validation; owned-server bundle
resolution and cleanup; duplicate-server protection; app duplicate-instance
notice; loopback-only endpoint at project-approved 2.0 port 14871.

Closure: the desktop and bundled YFinance service now use portable,
product-specific exclusive locks; the five-second duplicate notice and Windows
session scope match upstream behavior; physical Linux and Windows 10 evidence
proves live service operation and duplicate rejection. Deprecated 1.0 override
aliases remain accepted, while legacy file migration is intentionally excluded
by the user's no-history/no-traceback direction. Two fresh zero-gap upstream
closure scans and the mandatory closure gate passed on 2026-08-22.

## CR-004 - Portable Foundations And Diagnostics

Sources: upstream Core models/enums/normalizers/validators; Shared helpers,
diagnostics, filesystem interfaces, recovery policy, licensing, and internet
probe; corresponding upstream service tests.

Inventory: complete persisted models; normalization and validation; symbol and
time-zone helpers; app/version/path identity; sensitive-data redaction;
asynchronous structured circular trace; capped writer; portable internet probe;
render-run recovery state; release-manifest integrity and project-license
loading; portable interfaces needed by downstream services.

Trace behavior includes ordered burst draining, no per-line disk flush, lazy
network metadata, secret-field redaction, corrupt-cursor recovery, and worker
restart after a loop exception. Degraded view models expose stable dash
placeholders, accessible state text, and durable automation identifiers.
Recovery-state storage prefers the product root and falls back through writable
platform locations to an absolute last-resort path.

Unresolved gaps: models/enums and basic redaction are present, but active
TraceLog/capped logging, portable internet probing, render-run recovery state,
exchange time-zone helpers, symbol normalization/profile heuristics, and
OpenRouter model resolution are absent. Packaged release-manifest validation
and complete project-license loading are also absent. CR-004 is reopened.

## CR-005 - Quote/History Provider And YFinance Pipeline

Sources: upstream `PortfolioSaver.Data` providers/services, all `YFinance.net`
protocol/client/server/runtime files, `StartupCoordinator` quote pipeline, and
provider/protocol tests.

Inventory: owned server startup/health/shutdown; exact framed protocol and
mapping; one-at-a-time interleaved progressive quote dispatch; quote/history
caches; symbol mapping/profile resolution; Treasury-yield fallback; retry/rate
limit/provider-health state; runtime quote seed fallback; exchange timing;
cancellation, in-flight timeout/stale-completion handling, bounded transport
recovery, owned-server shutdown queueing, trace redaction, and partial-failure
behavior. The retained provider-budget type is not active in the upstream
startup coordinator and therefore is not an active product behavior.

The provider inventory also preserves ten-minute memory/persistent caches with
expiry and bounded LRU behavior; locale query scope and generic request
identity; malformed/null/error response distinctions; reported-change
normalization; auth/crumb-only session refresh; Retry-After/exponential
rate-limit handling; bounded transient-server retries; serialized shared-client
retirement/recovery; bounded parallel metadata/history fetch; stale-history
fallback; consume-once runtime seeds; and pipelined request draining without UI
continuation capture.

Partial quote responses retain resolved requested symbols, deduplicate input,
distinguish partial compatibility from total failure, and propagate intentional
cancellation. YFinance uses a dedicated bounded/redacted circular trace and a
nonfatal, cancellable, disableable upstream-sync metadata monitor.

Unresolved gaps: the retained YFinance protocol/server lane works, but the
portfolio layer lacks Treasury-yield fallback, provider health, retry/rate-limit
orchestration, runtime seed/profile services, exchange timing/calendar service,
the upstream one-second interleaved scheduler, in-flight request pruning, and
threshold/cooldown transport recovery. CR-005 is reopened.

## CR-006 - Settings Persistence And Validation Services

Sources: Core settings/normalizer/validator; settings protection/file service;
RSS, Yahoo-symbol, connectivity, AI-access, buffered-progress, trusted-profile,
and dialog services; upstream validation tests.

Inventory: normalized defaults; atomic file save; protected secrets and
permissions; RSS validation/fallback; fresh connectivity probe; rate-limit-aware
symbol validation; trusted profiles; optional AI endpoint/key/model validation;
cancelable buffered progress; apply/rollback semantics.

Normalization deep-clones the settings graph, caps four tapes/eight symbols,
restores the approved empty-portfolio defaults, preserves explicit direction
and differentiated speed choices, clears secret placeholders, defaults RSS/AI
mode and style, and canonicalizes chat-completions endpoints to provider bases.

Unresolved gaps: persistence, structural validation, RSS validation, symbol
validation, and secret storage exist. Connectivity gating, optional AI access
validation, OpenRouter discovery, trusted profiles, validation progress, and
the full cancellation/error contract are absent. The required matrix includes
offline RSS preservation, 30-minute news floor, rate-limit deferral, explicit
configured-key ownership, endpoint/timeout handling, and reusable validation
state for unchanged saved AI settings. CR-006 is reopened.

## CR-007 - Avalonia Configuration Workflow

Sources: upstream settings `MainWindow.xaml/.xaml.cs`,
`MainWindowViewModel.cs`, editor view models, validation progress window,
content/help text, and `MainWindowViewModelValidationTests.cs`.

Inventory: General/Advanced editors; four tapes/eight symbols; background and
news/AI fields; explicit Validate; fresh network lock/retry; staged RSS/AI then
symbol validation; progress/log experience; deferred symbol handling;
auto-naming; validation invalidation on persisted edits; validated OK/Cancel;
Cancel rollback and settings publication. Provider-filled names are read-only;
retired ticker metadata editors are absent; help badges/tooltips, responsive
scrolling/shared columns, and a persistent compact primary-action footer are
part of the visible workflow.

Unresolved gaps: the local editor covers basic fields and structural/RSS checks
but explicitly defers AI validation. It lacks network lock/retry, validation
progress/log, trusted/deferred symbol handling, and upstream validated OK/Cancel
workflow semantics. It must also enforce the per-tape symbol cap, invalidate on
all persisted collection/property edits, publish validated quote seeds only on
apply, and treat user cancellation as an ordinary close. CR-007 is reopened.

## CR-008 - Shell And Fullscreen Lifecycle

Sources: desktop `App.xaml.cs`, `MainWindow.xaml/.xaml.cs`, About window, and
`DesktopShellMigrationTests.cs`.

Inventory: maximized and optional direct-fullscreen startup; F11/Escape; menu
visibility; left double-click away from menus/interactive controls; exact state
restore; settings pause/resume; single settings owner; About branding/full
license; asynchronous packaged-release integrity notice; cross-platform
render-surface recovery; shutdown cleanup. Brand artwork preserves transparent
corners and target-appropriate icon sizes.

Unresolved gaps: basic Avalonia fullscreen and settings ownership work, but any
double tap currently toggles even over interactive/menu content, direct
fullscreen startup is absent, About omits upstream branding/license detail, and
render-run recovery is incomplete. Windows-native composition nudges are not
ported literally; their cross-platform behavioral requirement is a healthy,
recoverable render surface. CR-008 is reopened.

## CR-009 - Tapes, Macro Ribbon, Status, Runtime Integration

Sources: `TickerTapeControl`, `StatusBarControl`, related view models,
`TapeAnimationController`, `StartupCoordinator` runtime quote paths, and render
behavior tests.

Inventory: four configured lanes; 56/28 geometry; fixed item fields; seamless
copies; exact direction/speed; stable models during quote updates; waiting and
missing glyphs; trend and quote flash; eight 96x50 macro gauges; independent
macro refresh; market/latest/freshness/UTC status; one-at-a-time progressive
runtime updates.

Unresolved gaps: the original seven-meter closure claim was incorrect; the
active CR-010F work now restores eight gauges and geometry. Waiting/missing
ticker glyph state remains absent, and local quote cadence is 200 ms in
configured lane order instead of the upstream one-second interleaved pipeline.
The scheduler also lacks the separate freshness window, 15-minute hard-stale
floor, and recently fetched closed-world-market slowdown. Initial runtime order
must be macro symbols, then world-market symbols, then configured tape symbols.
CR-009 is reopened.

## CR-010A - Cinematic Contract Audit

Sources: complete scene XAML/code-behind; ticker, status, graph, global-market,
news controls and view models; background services; render behavior tests.

Inventory: every numeric/dynamic item in the cinematic contract plus the
complete exchange catalog, macro catalog, active provider timing, retained
background preparation, and lifecycle/recovery paths.

Pass-1 gaps corrected: seven was changed to eight macro meters; the asserted
per-tape graph cap was removed because current upstream ranks globally; global
markets now explicitly require local summary plus 18 exchanges, flags,
calendar/timing and NTP. The independent test pass additionally made waiting
overlay motion, background catalog decisions, control lifecycle, quote
scheduler policy, graph transition decisions, news continuity, and soak-trace
sampling explicit. Closure requires successive post-correction zero-gap scans.

## CR-010B - Scene Geometry And Background Cinema

Sources: scene XAML/background methods,
`BackgroundImageService`, `BackgroundPreloadService`,
`ExchangePhotoCacheService`, `ImageTransitionController`, and background tests.

Inventory: full-scene two-layer Fill rendering; async decode and stale-load
rejection; committed-frame retention; cross-fade; luminance/dim; slow zoom;
built-in/custom/managed sources; manifest warmup; JPEG validation; partial-file
cleanup; attribution; cancellation and recovery trace.

Unresolved gaps: CR-010F is repairing asynchronous retained-frame decode,
luminance, attribution, and trace. Managed manifest download/cache warmup,
partial cleanup, JPEG validation, bounded large-image decode, five-minute
default rotation, complete attribution, and catalog-change rotation policy
remain absent. CR-010B is reopened.

## CR-010C - Continuous Tapes And Quote Cues

Sources: ticker control/view model, tape animation controller, scene surgical
quote update path, and render behavior tests.

Inventory: configured four lanes; fixed geometry; bidirectional duplicated
tracks; 30 FPS elapsed motion with 100 ms cap; stable position across refresh;
waiting/missing glyphs; trend; 180/620/980/1680 ms quote flash.

Unresolved gap: motion, wrapping, geometry and flash timing are present, but
waiting/missing glyph properties and visuals are absent. Explicit empty,
unmeasured, data-transition and unload lifecycle behavior plus cached
cycle-distance measurement also remain absent. CR-010C is reopened.

## CR-010D - Floating Graphs And Quote Impulses

Sources: `StartupCoordinator` graph selection/cache/fallback paths,
`HistoricalGraphBuilder`, graph control/view model, scene placement/motion and
render behavior tests.

Inventory: global unique mover ranking; max 16; progressive cache/live/current
session/daily-close/quote fallback; 186x78/132x40 labels and per-segment colors;
collision-aware full-scene motion; keyed refresh preservation; directional
travel and both flash modes.

Unresolved gaps: CR-010F is repairing per-segment colors and ordinary flash.
The local selector incorrectly applies the unused per-tape-cap setting, and the
history lane lacks upstream progressive current-session/daily-close/quote
fallback and graph-build caching behavior. The quote-transition matrix also
needs first-live, stale retention/recovery, raw-price-only impulse, and
fetch-token-only suppression coverage. The fallback must seed current session
from previous close, retain five distinct exchange-local daily dates, use
exchange-local labels, and keep a collision-safe case-insensitive bounded LRU
graph cache keyed by all rendering inputs. CR-010D is reopened.

## CR-010E - Status, Global Markets, Weather And News

Sources: status/macro control and update paths; `FloatingClockBuilder`, global
market control, Yahoo exchange timing, NTP and weather services; full
`FinanceNewsService`; news control/view model and service tests.

Inventory: eight gauges; status states; local summary plus 18 flag-bearing
exchange cards; pinned New York plus continuous lane; exchange calendars,
pre/post/countdown, NTP, weather; telegraph playback; RSS; structured
multi-feed optional-AI summary, input fencing, retries/budgets, style-specific
cache, closing quotation, and differentiated fallbacks.

Unresolved gaps: CR-010F is repairing eight gauges and status geometry. The
local lane has only eight exchanges and no local summary, flags, NTP, or Yahoo
calendar/countdown behavior. The local AI-news implementation is a single
paragraph call and omits the upstream multi-feed structured format, safe prompt
handling, caching, retries, closing quotation, and differentiated fallbacks.
The playback controller also lacks the full burst debounce, unload cancellation,
equivalent-refresh index preservation, pre-layout/viewport recovery, prior-line
carry, and width-cache contract.
Startup must use cached news without blocking scene construction and move live
news retrieval to its independent refresh lane; NTP DNS and host waits must be
bounded. Weather must use bounded parallel city fetch, cached/offline fallback,
failed-city omission, stale-entry trimming, and cancellation-safe gate release.
News preserves the actual item count, uses an explicit waiting item when empty,
and emits the closing quotation once per sequence.
CR-010E is reopened.

## Pass-1 Result

No undocumented finding remains from the production-source pass. CR-002 is the
only completed implementation CR currently eligible for a retrospective
closure scan without remediation. CR-001 and CR-010A need their independent
post-correction documentation scans. CR-003 through CR-010E, excluding the
audit-only CR-010A, contain at least one active upstream behavior gap and must
not retain an unqualified `closed` status.

## Pass-2 Result - Independent Upstream Test Scan

The second pass read upstream behavioral test names and assertions without
using the pass-1 source checklist as its search index. It found the additional
details now recorded above and in the acceptance/cinematic contracts:

- branded bouncing network-waiting state and disabled market-critter status;
- five-minute background cadence, bounded decode and catalog refresh rules;
- tape measurement/start/stop/unload rules and closed-market quote scheduling;
- stale, first-live and non-raw-price graph transition decisions plus warmup
  cache continuity;
- pinned-calendar authority and ancillary world-market redraw throttling;
- news debounce, viewport recovery, line carry and complete AI fallback matrix;
- bounded displayed-tape soak sampling.

Because pass 2 found gaps, the zero-gap counter reset. All findings have been
mapped into the on-disk contracts. A fresh pass 3 must find no further gap
before CR-001 or CR-010A may regain `closed` status.

## Pass-3 Result - README And Manual UI QA

The line-by-line README and manual-UI-QA pass found additional configuration
details: bounded refresh controls, managed-cache presentation, read-only
provider-filled names, validation badges, mode-dependent RSS rules, the
YFinance runtime summary, per-tape symbol caps, help affordances, responsive
tabs, explicit AI-validation outcomes, quote-seed publication, and scene pause
during configuration. These were added to the baseline and CR-006/CR-007
inventory, resetting the zero-gap counter.

## Pass-4 Result - Complete Production-File Inventory

The full upstream `src/` file inventory found unowned packaged-release
integrity validation, complete About-license loading, recovery-root fallback,
owned-server shutdown queueing, and runtime in-flight request recovery. These
were assigned to CR-004, CR-005, and CR-008, resetting the counter.

## Pass-5 Result - Upstream Regression Suites

The NB040/NB048/NB049/NB051/NB058/NB060 suites added provider cache/parser,
backoff, client retirement, runtime ordering, independent refresh-lane and
bounded NTP details. The remaining product-service tests added graph fallback
and LRU behavior, weather cancellation/cache behavior, news sequence rules,
accessible placeholders/automation IDs, and trace-worker semantics. All are
now mapped; this pass found gaps and reset the counter.

## Pass-6 Result - Remaining Product Tests

The final unreviewed product-test files added partial-quote semantics,
YFinance-specific trace and nonfatal upstream-sync monitoring, transparent
branding, responsive configuration/help behavior, and absolute recovery-path
fallback. Windows-installer/uninstaller assertions were excluded only under the
explicit Avalonia-only, no-installer architecture delta. This pass found gaps
and reset the counter.

## Pass-7 Result - Tracker And Provenance Consistency

The first tracker scan found stale upstream path strings, an unqualified
seven-symbol closure sentence, and CR-002 still marked closed without the new
closure-audit object. The paths and sentence were corrected, every cited source
was proven to exist at the pinned upstream commit, and CR-002 was reopened for
administrative reclosure under the new gate. Because corrections were needed,
this pass is not a zero-gap scan.

## Pass-8 Result - Zero Gaps (1 of 2)

The corrected tracker parsed, no CR remained closed without a closure-audit
object, every retrofitted pre-development gate passed, and every cited upstream
source resolved at the pinned commit. No new internal or cross-document gap was
found. Successive zero-gap count: 1.

## Pass-9 Result - Zero Gaps (2 of 2)

An independent structure/constants pass verified exact CR cardinality and
uniqueness, required runtime constants and behavior statements, license headers,
PowerShell syntax, and whitespace integrity. It found no internal or
cross-document gap. Successive zero-gap count: 2.

CR-001, CR-002, and CR-010A may now close under the retrospective gate. Every
implementation CR with a behavior gap remains open; documentation completeness
does not waive implementation parity.
