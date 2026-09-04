<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-090: Preserve Upstream Single-Symbol YFinance Refresh Semantics

## Functional Inventory

| YF-01 | After initial scene hydration, the upstream live scene dispatches one portfolio symbol per runtime YFinance request at its fixed one-second cadence. | `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml.cs` | `ProductSceneViewModel` and `ProgressiveQuoteRefreshPipeline` |
| YF-02 | Upstream's progressive startup/runtime coordinator also issues one-symbol requests, with a maximum depth of four outstanding requests and round-robin selection. | `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs` | `ProgressiveQuoteRefreshPipeline` |
| YF-03 | Ancillary macro and world-market lanes are separate lanes and must be compared independently; they are not evidence that portfolio requests may be bulked. | upstream scene lane methods | 2.0 macro/world-market refresh methods |
| YF-04 | Every request and response must retain per-symbol ordering, cancellation, timeout, stale fallback, and circular-trace observability. | upstream runtime quote queue and trace calls | 2.0 quote pipeline and circular trace |

## Gap

The 2.0 portfolio pipeline already calls the provider with `[symbol]` and caps
the progressive portfolio depth at four, matching the upstream coordinator's
current behavior. However, the 2.0 macro and global-market loops call
`GetQuotesAsync` with collections of symbols. Upstream's live scene dispatch
path is explicitly one symbol per request, so the ancillary refresh contract
must be resolved by a complete upstream comparison before implementation.

This CR must not infer that all upstream calls are serial: the upstream
progressive coordinator can have four concurrent one-symbol requests. The
acceptance question is request shape and lane ownership, not merely total
concurrency.

## Required Work

1. Re-read the complete upstream runtime quote scheduler, macro lane, global
   market lane, provider, cancellation, and trace paths from disk.
2. Re-read the complete 2.0 counterparts and record every difference.
3. Implement the smallest compatible request-shape change, preserving the
   upstream four-deep portfolio behavior unless the fresh audit proves a
   different limit for a specific lane.
4. Add deterministic tests proving one-symbol provider calls, bounded
   concurrency, round-robin fairness, cancellation, timeout, and stale
   behavior for each affected lane.
5. Run settled real-product acceptance and inspect circular traces before
   closure.

## Acceptance

- Two successive forward and reverse upstream scans show zero unmapped
  request-shape or scheduling behavior.
- Portfolio, macro, and global-market provider calls match their upstream lane
  contracts, including one-symbol calls where required.
- Focused and full tests pass with no warnings, and circular traces prove the
  request and response sequence without secrets.
- NVIDIA review, build/test, license, syntax, artifact review, cleanup, and
  commit/push gates pass.

## Status

Open. The portfolio one-symbol/four-deep path is present; ancillary lane
request-shape parity requires implementation and fresh validation.
