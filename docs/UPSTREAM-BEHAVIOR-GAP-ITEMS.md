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
| LOGIC-01 | `BuildOrderedRuntimeSymbols_StagesMacrosThenWorldMarketsThenTapeSymbols`, `StatusMacroMeters_UpdateInPlace_AndPreserveStaleMacroValues`, `DataSourceCapabilities` | Runtime symbol ordering, source capability limits, and stale macro preservation require a complete 2.0 business-rule audit. | CR-021 |
| LOGIC-02 | `GetConfiguredRefreshWindow_UsesNewYorkMarketWindowWithExclusiveClose`, `ClockTick_RecomputesPinnedNewYorkStatusBandEverySecond`, `ExchangeCalendarSet`, `ExchangeCalendarStatus` | Market-session boundaries, exchange-calendar overlay/status models, and pinned status-band updates need explicit cross-platform evidence. | CR-021 |
| DATA-01 | `Client_DisposeWhileRequestPending_CompletesAndSettlesPendingRequest`, `Client_ReconnectsAfterMalformedFrameBreaksReceiveLoop`, `Client_SkipsCorruptResponse...` | YFinance client lifecycle, reconnect, malformed-frame, and pending-request semantics need depth parity. | CR-022 |
| DATA-02 | `YFinanceHttpDegradationPolicy_UsesRetryAfterOrExponentialBackoffForRateLimits`, `...RefreshesSessionOnlyForAuthAndCrumbFailures` | Retry classification and backoff business rules need a complete 2.0 evidence matrix. | CR-022 |
| TEST-01 | `GuestUxDeepExercise_*`, `RunVmUxValidation_*`, `PostProcessReferenceSpotChecks_*` | Harness logging, screenshot provenance, dimensions, multi-monitor behavior, and cleanup need one-to-one 2.0 test proof. | CR-023 |
| TEST-02 | `ProcessDocs_MakeDeepSeekReviewMandatoryBeforeCommitAndValidation`, `WorkflowGate_HardStopsWhenEndpointOrKeyIsUnavailable` | Review/checkpoint gates need direct tests for every failure and bypass attempt. | CR-023 |

## Repeat-Scan Additions

| Item | Upstream line-level evidence | 2.0 status | CR |
| --- | --- | --- | --- |
| DATA-03 | `QueueLegacyRootMigrationForStartup_CopiesLegacyRootInBackground`, `...RespectsExistingSentinel`, `...ReusesScheduledMigrationTask` | Legacy-to-2.0 data migration idempotency and background scheduling need explicit parity proof. | CR-024 |
| DATA-04 | `ReleaseManifestGuard_BackgroundApiQueuesFullDirectoryValidation`, `ValidateDirectory_ReturnsInvalid_WhenChecksumMismatch` | Release integrity validation must be proven for missing, corrupt, and asynchronously checked bundles. | CR-024 |
| SEC-01 | `ProviderSecretStoreService_OverlaySecrets_MigratesLegacySerializedAiSecret`, `Save_StripsSecretsFromPersistedSettingsFile` | Secret migration, redaction, and persisted-settings hygiene need a complete cross-platform test matrix. | CR-025 |
| AI-03 | `OpenRouterModelResolverTests` model ranking, free/instruct filtering, cache, cancellation, and discovery fallback cases | Model discovery behavior needs parity confirmation at the current endpoint/model contract, including cache and cancellation. | CR-025 |
| LIFE-01 | `DesktopRenderRecoveryPolicyTests` clean-exit, abnormal-exit, run-id, and managed-fatal-marker cases | Renderer recovery marker transitions require complete 2.0 lifecycle coverage. | CR-026 |
| LIFE-02 | `ServerProcessManager_*`, `Client_*Dispose*`, and `OwnedModeStartup_IsWiredIntoInteractiveApps` | Owned YFinance process shutdown, duplicate launch prevention, and disposal idempotence need end-to-end proof. | CR-026 |
| UI-03 | `AboutWindow_UsesBrandSplashAndPublisherMetadata`, `HelpAndAboutDocuments_AreBundled_NonEmpty_AndLicenseAligned`, `HelpBadges_ArePresentOnAllKeySections` | About/help/attribution surfaces require real Avalonia UI and accessibility acceptance. | CR-027 |
| TEST-03 | `DeepSeekArtifactReview_IncludesAppNativeSceneCaptureTiming`, `RunVmUxValidation_RecordsActualCaptureDimensions_AndFlagsFramebufferMismatch` | Screenshot provenance, timing, dimensions, and framebuffer mismatch detection need direct 2.0 harness tests. | CR-027 |

## Second Repeat-Scan Additions

| Item | Upstream line-level evidence | 2.0 status | CR |
| --- | --- | --- | --- |
| UI-04 | `BuildTapeItem_*`, `ApplyQuoteToGraph_OlderQuoteStillShowsCurrentValues`, `...StaleQuoteKeepsMoverVisibleUsingLastKnownData`, `...PercentOnlyChange_DoesNotTriggerCardFlash` | Waiting, missing, previous-close, stale, and structural-change display rules need an explicit 2.0 presentation matrix separate from animation travel tests. | CR-028 |
| LOGIC-03 | `BuildGraph_Cache*`, `BuildGraph_RebuildsCachedGraphWhenBounceSettingChanges`, `...HistorySnapshotChanges`, `...FetchTimestampChanges` | Graph cache key isolation, LRU eviction, and rebuild invalidation rules are not represented by a dedicated 2.0 test workstream. | CR-029 |
| UI-05 | `ConnectivityChanged_*`, `ValidateConfigurationAsync_*`, `EnsureValidationConnectivityAsync_*`, `ExecuteCancel_RequestsCloseWithoutPublishingValidatedQuotes` | Connectivity transitions, validation ordering, cancellation, and no-publish-on-cancel behavior need direct configuration workflow parity evidence. | CR-030 |
| DATA-05 | `LengthPrefixedProtocolStream_*`, `ProtocolIntegrity_*`, `ClientAndServer_TraceEveryMessageAtTransportBoundary`, `Client_*Dispose*` | Framing limits, zero/truncated/oversized payload handling, checksum compatibility, transport tracing, and pending-request disposal need a unified protocol safety matrix. | CR-031 |
| DATA-06 | `WarmDefaultManifestCacheAsync_*`, `BackgroundCatalogRefreshDecision_*`, `BackgroundPreparation_*` | Background download staging, content validation, concurrent warmup serialization, cancellation, and catalog rotation decisions need explicit cache-integrity coverage. | CR-032 |

## Third Repeat-Scan Additions

| Item | Upstream line-level evidence | 2.0 status | CR |
| --- | --- | --- | --- |
| DATA-07 | `AppSettingsNormalizerTests`, `SettingsFileServiceTests`, `TickerGroupEditorViewModelTests`, `LocalAppDataStorageScriptTests` | Legacy settings fields, profile/group mutation, normalization, and storage-root migration need one explicit persistence/compatibility matrix. | CR-033 |
| DATA-08 | `HistoricalCacheServiceTests`, `HybridHistoricalDataProvider_*`, `StartupCoordinatorGraphSelectionTests`, `StartupCoordinatorNewsTests` | Historical cache corruption/expiry, stale fallback, graph fallback, and startup-news cache semantics need dedicated parity evidence. | CR-034 |
| UI-06 | `DegradedUxContractTests`, `FloatingClockBuilderTests`, `WorldWeatherServiceTests`, `RuntimeFreshnessBehavior_*` | Degraded-state text, clock/weather ancillary rendering, and accessible freshness states need a unified real-scene acceptance matrix. | CR-035 |
| TEST-04 | `ItchPublishWorkflowTests`, `VirusTotalReleaseReportScriptTests`, `DesktopWerLocalDumpsScriptTests`, `DeepSeekCodeReviewGateTests` | Release publication authorization, advisory reporting, diagnostic-dump safety, and reviewer-gate automation need explicit 2.0 workflow coverage. | CR-036 |
| LOGIC-04 | `SymbolNormalizerTests`, `SymbolProfileHeuristicsTests`, `YahooSymbolValidationServiceTests`, `MarketSessionResolverTests` | Symbol canonicalization, asset-class inference, validation disablement, and market-session classification need a dedicated business-rule parity matrix. | CR-037 |
| LOGIC-05 | `RuntimeQuoteInFlightTracker_*`, `RuntimeQuoteRecoveryGate_*`, `ProviderBudgetLedgerService_*`, `StartupCoordinator_Build*` | Runtime request de-duplication, timeout pruning, recovery cooldown, provider-budget persistence, and staged scene-state construction need explicit 2.0 architecture and tests. | CR-038 |
| LOGIC-06 | `NtpTimeService.TryGetUtcNowAsync` (lines 20-67), `QueryHostAsync` (69-118), `ResolveHostAsync` (120-136), `NtpSyncResult` (139-144), `NtpTimeService_BoundsDnsAndHostTimeouts`, `NetworkAvailabilityService`, `VisualizerSceneControl.GetCachedStatusNetworkAvailability`, `ClockTick_*` | The 2.0 source has no NTP service. CR-039 must provide the bounded three-host NTP sequence, 1.5-second DNS and 4-second per-host limits, cancellation propagation, explicit success/source/UTC result, local-clock fallback after all hosts fail, timeout diagnostics, cached availability, and time/status refresh parity. | CR-039 |
| UI-07 | `ConfigDialogService`, `VisualizerSettingsService`, `VisualizerSceneState`, `MainWindowViewModelValidationTests` | Avalonia replacements for configuration-dialog ownership, settings loading, scene-state transfer, and validation-dialog outcomes need direct workflow evidence. | CR-040 |
| TEST-05 | `VmHarnessScriptTests`, `PortfolioSaver.VmAgent.Program`, `GuestUxDeepExercise_*`, `DesktopWerLocalDumpsScriptTests` | The maintained 2.0 physical-test agent, remote launch/cleanup contract, failure injection, and diagnostic collection need a platform-neutral replacement and self-tests. | CR-041 |
| DATA-09 | `Normalize_AiApiKey_ClearsPlaceholder`, `Normalize_RetiresLegacyRefreshPair_ToDesktopDefaults`, `Normalize_RetiresRemoteBackgroundPaths_ToLocalOnlyDefaults`, `Normalize_AppliesAlternatingDirectionsForLegacyAllLeftSettings`, `Normalize_DefaultsAiWritingStyleToDouglasAdams` | Exact settings-normalization compatibility rules need explicit 2.0 tests, including secret placeholder removal and intentional retirement of unsupported legacy values. | CR-042 |
| LOGIC-07 | `BuildDefault_CreatesLocalSummaryPlusEighteenExchangeCards`, `BuildDefault_HasBundledFlagAssetsForEveryExchange`, `BuildDefault_UsesCanonicalYahooGlobalExchangeBenchmarks`, `GetWorldIndexSymbols_MatchesExchangeCards` | Default portfolio/catalog construction, exchange-card count/assets, canonical global benchmarks, and world-index mapping need explicit 2.0 business-rule evidence. | CR-043 |
| VIS-05 | `FloatingSpriteMotionController_ClampsBouncingSprites_AndReversesVelocity`, `...ClampsNonBouncingSprites_WithoutReversingVelocity`, `VisualizerScene_IncludesAnimatedMarketCritterOverlay` | Market-critter sprite bounds, bounce reversal, non-bouncing clamp, and overlay continuity need dedicated visual/runtime parity coverage. | CR-044 |
| UI-08 | `DesktopShell_DoubleClickToggleDecision_RequiresLeftButtonAwayFromMenu`, `DesktopShell_NativeDoubleClickMessage_TogglesFullScreen`, `DesktopShell_CompositionNudgeDecisionHelpers_CoverNativeBoundsAndDpiTolerance`, `DesktopShell_ImplementsFullScreenToggle_AndEscExit` | Menu-safe double-click handling, native fullscreen messages, DPI-tolerant composition nudges, and Escape exit need a complete Avalonia shell interaction matrix. | CR-045 |
| DATA-10 | `NewsHeadlineCache`, `GetHeadlinesAsync_SummarizedMode_CachesStructuredFallbackAfterAiFailure`, `...ReplacesExpiredCacheWithStructuredFallbackWhenRefreshFails`, `...UsesRestylingOnlyPrompt_AndCachesAtCurrentMinimumFloor` | Persistent structured news caching, minimum freshness-floor replacement, and degraded refresh behavior need explicit 2.0 implementation and tests. | CR-046 |
| VIS-06 | `BuildNews_PreservesOriginalHeadlineCountWithoutArtificialDuplication`, `BuildNews_OnlyIncludesClosingQuoteOncePerPlaybackSequence`, `NewsFlasher_UsesTeleprinterPlaybackInsteadOfMarqueeLoop`, `NewsFlasherControl_*` | News sequence construction, teleprinter phase timing, viewport recovery, debounce, and headline replacement semantics need dedicated production-scene parity coverage. | CR-047 |
| DATA-11 | `DefaultHttpClients_ReuseSharedHandlers`, `GetHttpClientTimeout_*`, `ClientAndServer_TraceEveryMessageAtTransportBoundary` | Shared HTTP-handler ownership, per-request timeout policy, and transport resource reuse need explicit 2.0 lifecycle and performance-safety tests. | CR-048 |
| UI-09 | `FloatingClockViewModel`, `FloatingClockBuilder`, `StatusBarViewModel`, `MacroMeterViewModel`, `VisualizerSceneState.Clock`, `VisualizerSceneState.Status` | The upstream floating clock, macro-meter status overlay, and scene-state bindings are not represented by an equivalent 2.0 overlay model; the current fixed clock/status text is insufficient for full behavioral parity. | CR-049 |
| UI-10 | `NetworkWaitingViewModel`, `VisualizerSceneControl.ApplyNetworkWaitingOverlay`, `NetworkWaitingOverlay`, `DegradedUxContractTests` | The upstream branded network-waiting overlay, bounded placement, retained-scene behavior, and recovery visibility are not represented by a dedicated 2.0 scene surface. | CR-050 |
| DATA-12 | `WorldWeatherService.GetWeatherAsync`, `WeatherSnapshot`, `WorldWeatherServiceTests` | Structured weather snapshot persistence, bounded parallel fetches, stale per-city fallback, cancellation-safe refresh, and removal of unrequested cities need explicit 2.0 data-contract coverage. | CR-051 |
| VIS-07 | `BackgroundImageInfo`, `ImageFileHelper.IsSupported`, `BackgroundImageService.GetImages` | Background selection needs a structured image identity/display-name contract, exact supported-format policy, and attribution-safe path handling rather than returning raw paths alone. | CR-052 |
| LOGIC-08 | `VisualizerSceneControl_ResolveTimeZone_CachesLookupResultsForSchedulerTicks`, `TimeZoneLookupCache`, `ClockCityViewModel` | Cross-platform exchange-clock updates need a bounded timezone lookup cache and explicit invalid-ID fallback behavior instead of resolving timezone IDs afresh on every tick. | CR-053 |
| TEST-06 | `UPSTREAM-2.0-GAP-LEDGER.md` generated mappings for active `src/` and `tests/` artifacts | The ledger now emits calculated paths, but the normalized upstream-to-2.0 path is absent for 161 active rows. Each such row needs an exact existing counterpart mapping, a line-level content mapping, or an explicit architecture disposition; installer/Inno rows remain retired. | CR-054 |

## Test-Class Disposition Cross-Check

The following upstream test families were re-read from disk during the repeat
scan. They do not create additional behavior categories because their
assertions are already routed to the workstreams above or to an earlier
closed foundation CR. The installer-only family is intentionally retired by
the Avalonia-only, bundle-delivery architecture.

| Upstream test families | Disposition |
| --- | --- |
| `AppDataRootResolverTests`, `PathHelperTests`, `LocalAppDataStorageScriptTests`, `SettingsFileServiceTests`, `AppSettingsNormalizerTests`, `TickerGroupEditorViewModelTests` | Routed to DATA-03/DATA-07 and the closed portable-foundation CRs. |
| `FinanceNewsServiceTests`, `NewsFeedValidationServiceTests`, `MainWindowViewModelValidationTests`, `StartupCoordinatorTapeItemTests`, `StartupCoordinatorNewsTests`, `StartupCoordinatorGraphSelectionTests`, `VisualizerRenderBehaviorTests`, `FloatingClockBuilderTests`, `WorldWeatherServiceTests`, `DegradedUxContractTests` | Routed to AI-01/UI-01/UI-04/UI-06, VIS-01/VIS-02/VIS-03/VIS-04, and LOGIC-02/LOGIC-03. |
| `HistoricalCacheServiceTests`, `SymbolNormalizerTests`, `SymbolProfileHeuristicsTests`, `YahooSymbolValidationServiceTests`, `MarketSessionResolverTests`, `QuoteRefreshPolicyTests`, `ProviderHealthServiceTests`, `SensitiveDataRedactorTests`, `YFinanceExchangeTimingServiceTests` | Routed to DATA-01/DATA-02/DATA-08, LOGIC-01/LOGIC-02/LOGIC-04, and SEC-01. |
| `YFinanceClientServerProtocolTests`, `YFinanceServerClientPipelineTests`, `YFinanceCircularTraceSinkTests`, `YFinanceUpstreamSyncMonitorTests`, `TraceLogTests` | Routed to DATA-05, LIFE-02, TEST-04, and the closed diagnostics foundation CRs. The upstream capped-file helper is intentionally omitted because 2.0 product diagnostics are circular-trace-only. |
| `ConfigTextConsistencyTests`, `LegalHeaderPolicyTests`, `DeepSeekCodeReviewGateTests`, `VmHarnessScriptTests`, `DesktopWerLocalDumpsScriptTests`, `ItchPublishWorkflowTests`, `VirusTotalReleaseReportScriptTests`, `EnvironmentSerialCollection` | Routed to TEST-01/TEST-02/TEST-04 and the repository license/workflow gates. |
| `BrandingAssetTransparencyTests`, `ExchangePhotoCacheServiceTests`, `ReleaseManifestValidatorTests`, `ProjectLicenseServiceTests` | Routed to DATA-04/DATA-06, UI-03, and the closed asset/integrity foundation CRs. |
| `InnoInstallerScriptTests` | Intentionally retired; no Windows installer exists in the target architecture. |
| `Nb040BehaviorTests`, `Nb048BehaviorTests`, `Nb049BehaviorTests`, `Nb051BehaviorTests`, `Nb058Nb060BehaviorTests` | Historical upstream regression labels; their assertions are routed to the corresponding runtime, scene, and workflow rows above, with no separate product behavior. |

## Repeat Rule

Every item must be resolved by a mapped 2.0 implementation and a focused test
or an explicit architecture replacement. Repeat the upstream line-by-line scan
after each CR; two successive scans with no new behavior-level items are
required before declaring this audit complete.
