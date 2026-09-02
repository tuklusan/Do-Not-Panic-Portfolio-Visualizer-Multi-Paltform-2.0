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

# CR-010J Multi-Source RSS Architecture Contract

## Change Intent

Replace the temporary single-source RSS default with a portable, multi-source
finance-news architecture. The product ships with the following three default
feeds and allows up to three user-configured sources. The supplied reference contract reads
the following feeds, identifies each channel, and handles the first usable
entries with their title, link, and publication date:

1. CNBC: `https://www.cnbc.com/id/100003114/device/rss/rss.html`
2. MarketWatch / Dow Jones: `https://feeds.content.dowjones.io/public/rss/mw_topstories`
3. Investing.com: `https://www.investing.com/rss/news.rss`

The Python example is behavioral reference only. DNPPV-2.0 remains a .NET 10
and Avalonia product; it must not introduce a Python runtime dependency.

## Required Product Behavior

- Ship the three sources as ordered defaults and expose three bounded editable
  configuration slots. Empty slots are allowed, but every non-empty slot must
  pass RSS/XML verification and at least one must pass live verification before
  RSS settings can be saved.
- Fetch sources independently and concurrently with bounded timeout, retry,
  cancellation, response-size, XML-hardening, and per-source diagnostic
  behavior.
- Parse RSS/Atom channel identity, title, canonical link, and item publication
  dates without assuming one publisher-specific date format.
- Normalize dated entries to UTC; reject malformed, future-dated, duplicate,
  and stale entries according to documented policy. A current channel build
  time does not make stale item content current.
- Merge valid entries across sources in descending publication order, with a
  deterministic source/order tiebreaker. Do not repeatedly play duplicates
  that differ only in feed formatting or tracking parameters.
- Preserve source attribution in diagnostics and any UI surface that presents
  a merged headline. Feed failure or staleness must degrade only that source;
  other current sources remain eligible for playback.
- When no usable current entry remains, show an explicit aggregate degraded
  state. Do not silently substitute an unrelated publisher or present historic
  material as live news.
- Preserve the existing telegraph-style playback state machine independently
  from fetching, aggregation, and failure handling.

## Migration And Validation Gates

## Functional Inventory

Upstream baseline: `65a53bbbf0cf9af1058363f8939d464ca03858f8`.

| ID | Upstream behavior and citation | DNPPV-2.0 mapping and evidence |
| --- | --- | --- |
| RS-01 | `PortfolioSaver.Presentation/Services/FinanceNewsService.cs` normalizes the configured RSS URL and fetches RSS mode directly. | Preserve legacy `NewsFeedUrl` settings while migrating to three bounded `NewsFeedUrls` slots and a verified-source configuration gate. |
| RS-02 | The same service fans out summarized-news feed fetches concurrently and keeps successful results when a peer fails. | Fetch the built-in catalog concurrently with isolated per-source diagnostics and deterministic merging. |
| RS-03 | `PortfolioSaver.Core/Validation/SettingsValidator.cs` requires an HTTP/S RSS URL only in RSS mode. | Retain mode-aware URL validation and settings-screen editing. |
| RS-04 | `PortfolioSaver.Settings/ViewModels/MainWindowViewModel.cs` validates the configured feed and reports reset, skipped, and failure states. | Preserve validation messages and add catalog/source diagnostics without silently changing a user source. |
| RS-05 | Upstream `FinanceNewsService.cs` caches/uses RSS fallback when summarized news is unavailable. | Preserve RSS fallback and the independent telegraph playback state machine while replacing only the source aggregation layer. |
| RS-06 | Upstream fetch paths cap failure effects and refresh on normal cadence. | Bound every request, reject unsafe XML/content, and emit aggregate degradation only when no current entry remains. |

Before implementation, inventory the upstream news service and every current
DNPPV-2.0 consumer/configuration/trace/test path. Pass
`Test-MigrationBehaviorGate.ps1 -CrId CR-010J -Stage PreDevelopment` with
source citations and map all current France 24 freshness behavior to either a
retained multi-source equivalent or an explicitly approved replacement.

The implementation must add deterministic tests for at least:

- every individual source current, stale, malformed, unavailable, and
  timeout/failure state;
- partial success with one or two current sources;
- all-source aggregate degradation;
- offset/UTC date parsing, future dates, malformed dates, duplicate canonical
  links, deterministic ordering, and title/link attribution;
- XML security and size limits; and
- playback continuity when the merged snapshot changes, equivalent entries
  refresh, or source availability changes.

Physical acceptance must run the normal product without visual fixtures on the
four local desktop machines, capture the rendered multi-source result and
degraded result, inspect capped traces, and confirm process cleanup. It also
requires the full six-RID publish matrix, mandatory reviewer and TEST_ARTIFACT
review gates, a pushed checkpoint before each physical run, and a fresh
closure rescan of upstream/current behavior.

## Transition Rule

CR-010J established the multi-source service foundation. CR-014 completes the
configuration-screen contract and AI-generator acceptance matrix; it does not
erase the requirement to reject stale item content.
