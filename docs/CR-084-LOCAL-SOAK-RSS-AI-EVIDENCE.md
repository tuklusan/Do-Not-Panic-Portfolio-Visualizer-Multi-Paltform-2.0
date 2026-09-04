<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, governing-law, and other provisions.
-->

# CR-084: Require Real RSS And AI Evidence In Local Soaks

## Objective

Make a local real-product soak prove the news path that the product is expected
to ship: configured RSS feeds are reachable and produce usable current or
partial news, and a supplied OpenRouter credential reaches successful AI
summary generation. A screenshot alone is not sufficient evidence because the
text can be between playback states; the retrieved size-bounded circular trace
is the authoritative runtime record.

## Functional Inventory

| CR-084-01 | The product fetches the configured RSS catalog and records a usable `Fresh` or `Partial` news state before optional AI work. | The local soak must retrieve the circular trace and require `RssPlaybackReady` with a positive headline count and `Fresh` or `Partial` state. | Open |
| CR-084-02 | The summarized-news path calls the configured AI endpoint and records a successful generated summary. | When the protected OpenRouter key is supplied, the local soak must require `AiSummarySucceeded`. | Open |
| CR-084-03 | Provider credentials are process-scoped and never become evidence content. | The harness passes the key only through the child environment/remote secret and writes only boolean/key-free evidence. | Open |
| CR-084-04 | A failed or unavailable RSS/AI path remains diagnosable without arbitrary logs. | `news-evidence.json` records bounded event-presence booleans and the circular trace remains the source artifact. | Open |

## Acceptance Criteria

- Each available local machine is run as a concurrent real-product soak.
- A soak with a configured OpenRouter key hard-stops unless the retrieved
  circular trace proves usable RSS and `AiSummarySucceeded`.
- The artifact contains `news-evidence.json` and the two circular trace files;
  no API key or headline content is copied into the evidence manifest.
- Missing, stale-only, unavailable, or AI-failed evidence creates a failed
  machine result and routes the defect to a new CR rather than being reported
  as a passing soak.
- The evidence gate is exercised on Linux, Windows 10, Windows 11, and Intel
  macOS whenever each machine is available at cycle start.
- Two successive successful validation passes show no missing RSS or AI
  evidence before CR-084 is closed.

## Validation Plan

1. Run the PowerShell syntax and license gates.
2. Run a short concurrent local cycle and inspect every machine's
   `news-evidence.json`, circular trace, screenshot set, and cleanup state.
3. Exercise the forced-news-failure path and verify it fails with
   `RSS_USABLE=false` without leaking credentials.
4. Repeat the successful cycle and compare both manifests for the same
   evidence contract before closure.

## RSS-First Runtime Contract

The real product publishes a usable RSS snapshot to the cinematic news lane
before awaiting optional AI summarization. A slow, rate-limited, malformed, or
unavailable AI response must therefore never leave the scene on its bootstrap
text or blank the already-fetched RSS headlines. A successful AI response may
replace the RSS playback after `AiSummarySucceeded`; otherwise the RSS
headlines remain visible. The circular trace records the publication stages as
`NEWS_PLAYBACK_PUBLISHED;SOURCE=RSS` and, when applicable,
`NEWS_PLAYBACK_PUBLISHED;SOURCE=AI`.

The first post-fix Linux physical run (`local-rss-ai-fix-r31`) proved fresh RSS,
successful AI generation, cleanup, and a settled real-product screenshot with
finance-news text. Two successive full local cycles and the required hosted
four-hour cycles are still required before this CR can close.

## Upstream And Reverse Checks

The upstream `FinanceNewsService` and cinematic news loop were re-read for the
RSS freshness state, optional AI summary path, RSS fallback, and circular trace
behavior. The implementation must preserve RSS playback when optional AI fails,
while this soak gate separately requires successful AI evidence only when the
soak explicitly supplies the protected key. Closure requires an independent
reverse scan against those upstream behaviors and two successive zero-gap
passes under `build/Test-MigrationBehaviorGate.ps1`.
