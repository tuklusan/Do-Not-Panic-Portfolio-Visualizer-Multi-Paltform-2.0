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

# Upstream Behavior Gap Items

This is the behavior-level companion to `UPSTREAM-2.0-GAP-LEDGER.md`. Items
below are derived from the pinned upstream source, test, workflow, and
documentation scans. A filename difference is not a gap; each item names an
observable behavior, UI rule, business rule, or test-depth obligation.

## Confirmed Gap Workstreams

| Item | Upstream line-level evidence | 2.0 status | CR |
| --- | --- | --- | --- |
| AI-01 | `AiNewsAccessValidationService_SummarizedMode_ReportsTimeout`, `...RejectsMalformedEndpointWithoutHttp`, `...Treats429AsTransientSkippedValidation` | AI request fallback is covered, but configuration-access probe depth and user-facing timeout/rate-limit states need full parity confirmation. | CR-018 |
| AI-02 | `BuildSummarizedNewsPrompt_FencesAndQuotesUntrustedHeadlines` | Prompt-injection-safe prompt construction is not yet proven in the 2.0 suite. | CR-018 |
| UI-01 | `MainWindowFooter_HasPrimaryOkWorkflowAndValidatedCancelButton`, `ApplyValidatedConfiguration_SavesSeedsQuotes_AndRequestsClose` | Three-feed UI exists, but complete OK/Cancel/validation-state acceptance must be proven on all local targets. | CR-019 |
| UI-02 | `GeneralTab_UsesResponsiveScrollAndSharedColumns`, `AdvancedTab_UsesStretchGridAndBoundedColumnMinimums`, `ConfigApp_ForcesSoftwareRendering...` | Configuration layout and small-screen rendering need explicit 2.0 visual assertions. | CR-019 |
| VIS-01 | `TickerTapeControl_CachedWidthDrivesExpectedAnimationCycleDistance`, `...StopsAndRestartsAcrossDataTransitions`, `...WithNoItems_LeavesAnimationStopped` | Ticker movement, width measurement, empty-lane, unload, and restart behaviors need complete parity evidence. | CR-020 |
| VIS-02 | `ApplyQuoteToGraph_PositiveRefreshQueuesTopEdgeImpulse`, `...NegativeRefreshQueuesBottomEdgeImpulse`, `...StaleToLiveRecovery_TriggersCardFlash` | Graph drop/lift impulses and stale-to-live flash transitions need full production-scene coverage. | CR-020 |
| VIS-03 | `FloatingGraphCards_UseIndependentMotionAcrossSceneWideSafeInset_AndRightLabelGutter`, `GraphMotionBounds_UseSceneWideSafeInset...` | Scene-wide graph bounds, independent motion, and label gutters require explicit geometry acceptance. | CR-020 |
| VIS-04 | `BackgroundTransition_PreloadsBitmap_AndKeepsSlowZoomLoopLightweight`, `BackgroundRotationCancellation_DoesNotBlockDispatcher...` | Background preload, cancellation, and transition settlement need parity tests without UI starvation. | CR-020 |
| LOGIC-01 | `BuildOrderedRuntimeSymbols_StagesMacrosThenWorldMarketsThenTapeSymbols`, `StatusMacroMeters_UpdateInPlace_AndPreserveStaleMacroValues` | Runtime symbol ordering and stale macro preservation require a complete 2.0 business-rule audit. | CR-021 |
| LOGIC-02 | `GetConfiguredRefreshWindow_UsesNewYorkMarketWindowWithExclusiveClose`, `ClockTick_RecomputesPinnedNewYorkStatusBandEverySecond` | Market-session boundaries and pinned status-band updates need explicit cross-platform evidence. | CR-021 |
| DATA-01 | `Client_DisposeWhileRequestPending_CompletesAndSettlesPendingRequest`, `Client_ReconnectsAfterMalformedFrameBreaksReceiveLoop`, `Client_SkipsCorruptResponse...` | YFinance client lifecycle, reconnect, malformed-frame, and pending-request semantics need depth parity. | CR-022 |
| DATA-02 | `YFinanceHttpDegradationPolicy_UsesRetryAfterOrExponentialBackoffForRateLimits`, `...RefreshesSessionOnlyForAuthAndCrumbFailures` | Retry classification and backoff business rules need a complete 2.0 evidence matrix. | CR-022 |
| TEST-01 | `GuestUxDeepExercise_*`, `RunVmUxValidation_*`, `PostProcessReferenceSpotChecks_*` | Harness logging, screenshot provenance, dimensions, multi-monitor behavior, and cleanup need one-to-one 2.0 test proof. | CR-023 |
| TEST-02 | `ProcessDocs_MakeDeepSeekReviewMandatoryBeforeCommitAndValidation`, `WorkflowGate_HardStopsWhenEndpointOrKeyIsUnavailable` | Review/checkpoint gates need direct tests for every failure and bypass attempt. | CR-023 |

## Repeat Rule

Every item must be resolved by a mapped 2.0 implementation and a focused test
or an explicit architecture replacement. Repeat the upstream line-by-line scan
after each CR; two successive scans with no new behavior-level items are
required before declaring this audit complete.
