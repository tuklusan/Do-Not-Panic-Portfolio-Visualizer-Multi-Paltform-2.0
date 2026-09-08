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

**Status:** Open
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

The current 2.0 YFinance.NET port has not yet completed a source-cited,
behavior-by-behavior mapping and focused test set for all of those changes.
Therefore the reviewed commit remains `38c73ce...` for now. Updating the
metadata to `3d9d2f0...` would incorrectly claim that the newer behavior had
been reviewed and migrated. CR-108 remains open until the affected behaviors
are either demonstrated equivalent in the portable adapter or explicitly
split into tracked parity work.
