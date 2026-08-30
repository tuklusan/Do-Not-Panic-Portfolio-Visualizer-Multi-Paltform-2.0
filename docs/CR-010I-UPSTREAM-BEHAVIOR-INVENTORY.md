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

# CR-010I Upstream Behavior Inventory

## Authority

This inventory was manually reviewed on 2026-08-29 against upstream commit
`2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`. It also records the observed
external-feed condition that motivated this product-integrity CR: the
user-mandated France 24 business URL returned a current feed build timestamp
while its newest item was weeks old. That upstream condition must not be
misrepresented as a local cache or live-news success.

## Functional Inventory

| ID | Upstream source | Required behavior | DNPPV-2.0 mapping |
| --- | --- | --- | --- |
| NF-01 | `PortfolioSaver.Presentation/Services/FinanceNewsService.cs` | RSS mode normalizes the configured HTTP(S) feed, fetches it online, and falls back gracefully when unavailable or empty. | Retain the configured France 24 default and the normal RSS/AI fallback model. |
| NF-02 | `FinanceNewsService.cs`, `PortfolioSaver.Core/Constants/Defaults.cs` | News refresh is bounded by the 30-to-240 minute settings interval; network failures do not stop other visual lanes. | Retain the independent refresh cadence and failure isolation. |
| NF-03 | `PortfolioSaver.Render/Controls/NewsFlasherControl.xaml.cs` | Valid headlines feed telegraph-style playback without stalling scene motion. | Preserve current playback state-machine behavior for fresh content and explicit degraded content. |
| NF-04 | `tests/PortfolioSaver.Tests/Services/FinanceNewsServiceTests.cs` | Source behavior recognizes empty/failed feeds and cache-mode separation, but does not parse item publication dates or reject a stale yet syntactically valid feed. | Add the user-required freshness integrity extension without reducing source fallback behavior. |
| NF-05 | User-mandated DNPPV-2.0 default and `Defaults.DefaultNewsFeedUrl` | The default remains `https://www.france24.com/en/business/rss`. | Do not silently substitute another publisher or change the saved default. |
| NF-06 | Live evidence captured 2026-08-29 | An HTTP-successful feed can advertise a current channel build timestamp while its newest `pubDate` is stale. | Parse item publication dates; a newest item older than seven days is stale-source; distinguish fresh, stale-source, malformed-date, and unavailable states in traceable output. |

## Closure Conditions

CR-010I closes only after the product preserves the configured France 24
default, never presents stale source material as current live news, retains
the source-derived refresh/fallback behavior, and proves fresh, stale, empty,
and failed-feed cases with deterministic tests plus real-product trace
evidence.
