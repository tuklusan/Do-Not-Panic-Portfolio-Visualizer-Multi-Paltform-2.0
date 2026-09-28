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

# CR-108: Refresh The Reviewed YFinance Upstream Baseline

**Status:** Closed
**Phase:** Phase 7
**Priority:** Normal
**Depends on:** CR-105

## Objective

Investigate and close the yfinance upstream-sync warning observed in hosted run
`33999741342`. The retained trace reports that the reviewed baseline is
`38c73ce33fb1ee77d37a0998c95c06e60356298e` while the live upstream check sees
`3d9d2f0cacb662bff689874cd6113bae3a30a885`. The reference to `CR-139` in the
reviewer text is not accepted as a local tracker identity without verification;
the local ledger currently ends at CR-107.

## Acceptance criteria

- Verify the upstream yfinance commit and date from the source repository.
- Read the changed upstream yfinance behavior relevant to quote fetching,
  session handling, retries, parsing, and the local YFinance.NET adapter.
- Either update the reviewed baseline and its source-cited ledger entry, or
  document why the warning is intentionally retained and route any behavioral
  gap to a separate CR.
- Focused YFinance protocol/parser tests, full Release build/test, upstream
  forward/reverse gates, mandatory NVIDIA review, and a fresh hosted evidence
  cycle pass without an unexplained upstream-sync warning.

## Initial evidence

- Run `33999741342` yfinance circular trace: `UpstreamYFinanceNewerThanReviewed`.
- Run `33999741342` product lane: quotes returned HTTP 200 and populated the
  scene, so this warning is a review-baseline defect rather than proof of a
  quote-fetch failure.

## Current upstream verification

On 2026-09-08, the live upstream repository reported commit
`3d9d2f0cacb662bff689874cd6113bae3a30a885`, dated `2026-08-26T18:20:38+01:00`
and released as yfinance `1.7.0`. A shallow source comparison against the
reviewed `38c73ce33fb1ee77d37a0998c95c06e60356298e` baseline found material
changes in `yfinance/data.py`, `yfinance/scrapers/history.py`,
`yfinance/exceptions.py`, quote metadata, and tests. The relevant behaviors
include active-session cache detection, transient cookie/crumb degradation,
preservation of user-supplied proxies, retrying without a crumb, lazy trading
period metadata, 30-minute interval handling, currency repair/reversal, and
more attributable missing-price errors.

## Functional Inventory

| CR-108 | Upstream yfinance 1.7.0 behavior mapping | Complete; all applicable portable adapter behaviors are dispositioned below. |
| YF-10 | Quote/session/history/parser behavior inventory | Complete; source-cited disposition follows. |

## Source-cited behavior disposition

The source comparison was completed against the upstream repository at
`https://github.com/ranaroussi/yfinance/commit/3d9d2f0cacb662bff689874cd6113bae3a30a885`
and the full comparison range at
`https://github.com/ranaroussi/yfinance/compare/38c73ce33fb1ee77d37a0998c95c06e60356298e...3d9d2f0cacb662bff689874cd6113bae3a30a885`.
The release metadata was independently verified in `yfinance/version.py` and
the commit metadata.

| Upstream source behavior | 2.0 disposition | Local evidence |
| --- | --- | --- |
| `data.py`: active cached-session detection | Equivalent; the portable adapter owns a bounded in-process session cache and refresh lock. | `YahooSessionManager.GetSessionAsync`; `YahooSessionState.IsValid`; `YFinanceInfrastructureTests` |
| `data.py`: transient cookie-bootstrap degradation and caller proxy preservation | Equivalent at the transport boundary; cookie bootstrap is centralized and caller transport configuration is not overwritten. | `YahooSessionManager.RefreshAsync`; `YFinanceOptions`; `YahooFinanceHttpClient` |
| `data.py`: retry without a crumb after crumb-related failure | Equivalent outcome; invalid-cookie/crumb/CSRF responses invalidate the session and retry centrally. | `YahooFinanceHttpClient.ShouldRefreshSession`; `SendJsonStringAsync` |
| `history.py`: attributable missing-price errors and 30-minute interval context | Equivalent portable contract; malformed chart, empty result, and chart error states remain attributable adapter outcomes, with interval retained in traces. | `HistoryService.GetHistoryResponseAsync`; `ParseHistoryResponse`; `YFinanceApiException` |
| `history.py`: lazy metadata and current trading periods | Equivalent; metadata and pre/regular/post windows are parsed and carried through protocol DTOs. | `HistoryService.ParseMetadata`; `ParseCurrentTradingPeriods`; `HistoryMetadataDto` |
| `history.py`: pandas currency conversion and price repair | Not applicable to the portable C# chart contract; raw Yahoo values and metadata are preserved, with no implicit currency mutation. | `HistoryResponse`; `HistoricalBar`; `HistoryMetadata` |
| `scrapers/quote.py`: quote metadata null safety and missing-field handling | Equivalent; nullable quote fields are mapped without fabricated values and partial-quote policy remains explicit. | `QuoteService`; `QuoteSummaryService`; provider tests |
| `exceptions.py` and regression tests: rate-limit and cookie/crumb classifications | Equivalent; 429 is typed, 5xx retry is bounded, and cookie/crumb failures are traced before refresh. | `YFinanceRateLimitException`; `YahooFinanceHttpClient`; `YahooSessionManager` |

The Python-only pandas/currency-repair details are intentionally not copied into
the protocol adapter. The portable equivalent preserves raw Yahoo values and
exposes metadata so presentation or currency decisions remain explicit. The
mapping is complete with zero unmapped 2.0 behaviors, and the reviewed baseline
is updated to the verified upstream `1.7.0` commit.
