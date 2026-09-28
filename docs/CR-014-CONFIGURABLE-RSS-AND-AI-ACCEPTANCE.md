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

# CR-014 Configurable RSS Sources And AI News Acceptance

## Objective

Complete the multi-source news configuration contract. DNPPV-2.0 must ship
with the three approved finance feeds, allow no more than three configured
sources, and refuse to save RSS mode unless every non-empty source has passed
RSS/XML verification and at least one has passed live verification. Existing single-source settings must migrate
without losing the user-selected source.

## Functional Inventory

Upstream baseline: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

| ID | Upstream behavior and citation | Required DNPPV-2.0 behavior |
| --- | --- | --- |
| AI-01 | `src/PortfolioSaver.Settings/Windows/MainWindow.xaml` exposes news mode, feed editing, and validation feedback. | Avalonia configuration exposes three labeled RSS slots and a visible verification result. |
| AI-02 | `src/PortfolioSaver.Settings/ViewModels/MainWindowViewModel.cs` validates settings before save and reports provider failures. | RSS mode requires one live-verified source; invalid optional slots do not erase valid slots. |
| AI-03 | `src/PortfolioSaver.Settings/Services/NewsFeedValidationService.cs` bounds HTTP/XML validation and rejects unreadable feeds. | Validate each non-empty slot independently with bounded, hardened parsing and aggregate results. |
| AI-04 | `src/PortfolioSaver.Presentation/Services/FinanceNewsService.cs` aggregates news and keeps RSS fallback when AI summarization fails. | Fetch configured sources independently, merge current headlines, and preserve RSS output for empty, malformed, timeout, HTTP error, empty-AI, malformed-AI, and cancellation-safe AI responses. |
| AI-05 | `src/PortfolioSaver.Core/Models/AppSettings.cs` persists the selected news source and AI settings. | Persist three sources, migrate legacy `NewsFeedUrl`, cap at three, and ship the approved three-feed default. |

## Acceptance

- Unit tests cover defaults, persistence migration, zero/one/two/three slots,
  invalid optional slots, no verified source, one verified source, all-source
  failure, partial success, deduplication, and source attribution.
- AI tests cover successful structured output, style/model/auth request shape,
  missing key, timeout, cancellation, HTTP 401/429/5xx, malformed JSON, empty
  content, and RSS fallback for every non-cancellation failure.
- The configuration window is exercised on Lubuntu, Windows 10, and Windows
  11 with the default three slots visible and a live verification result.
- The real production scene, not a fixture, shows merged RSS output and remains
  useful when AI generation is unavailable.
- Six self-contained RID publishes, mandatory review gates, artifact review,
  and process cleanup pass before closure.

## Gates

Run the upstream pre-development and closure behavior gates using this file as
the cited inventory. Require two successive zero-gap manual source scans,
reviewer approval, full automated tests, physical acceptance, and a pushed
checkpoint before each physical run.
