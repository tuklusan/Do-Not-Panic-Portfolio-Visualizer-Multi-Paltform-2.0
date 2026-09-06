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

# CR-074: Restore Upstream AI Request Parity

## Functional Inventory

| ID | Upstream behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| AI-04 | Summary requests bound generation with `temperature=0.2` and `max_tokens=2000`. | `FinanceNewsService.SummarizeAsync`. | Implemented |
| AI-05 | OpenRouter summary requests ask the provider to sort by latency. | OpenRouter-only `provider.sort` payload member. | Implemented |
| AI-06 | OpenRouter requests include attribution headers. | `OpenRouterModelResolver.AddAttributionHeaders`. | Implemented |
| AI-07 | The request uses the resolved model when OpenRouter `auto` is configured. | `ResolveAiModelForRequestAsync` in the summary path. | Implemented |
| AI-08 | RSS retrieval and optional summarized-news generation respect the upstream effective refresh cadence: 30 minutes minimum/default, with no extra external calls from a faster scheduler poll. | `ProductSceneViewModel.RunNewsRefreshLoopAsync`; `Defaults` and `AppSettingsNormalizer` enforce the 30-minute floor. | Implemented; cadence evidence remains part of closure |

## Required Gates

The upstream source and tests were manually re-read from disk at commit
`65a53bbbf0cf9af1058363f8939d464ca03858f8`. The reverse scan must confirm that
each inventory row maps to executable 2.0 code and focused tests. Repeat both
scans at closure; any unmapped behavior reopens this CR or creates a successor.

## Acceptance

- Non-OpenRouter requests carry the bounded generation settings and no provider
  routing member.
- OpenRouter requests carry bounded generation, latency routing, attribution,
  and the resolved model without exposing credentials.
- RSS retrieval and optional AI summarization use the upstream effective refresh
  cadence: 30 minutes minimum/default, with no extra external calls from a
  faster scheduler poll.
- A provider HTTP `429` during an optional AI request is not itself a cadence
  or parity defect when the bounded upstream retry/fallback path is exercised;
  acceptance requires eventual AI success evidence or an attributable
  upstream-compatible fallback, not first-attempt success under concurrent
  hosted lanes.
- Focused tests, full build/test, license and syntax gates, NVIDIA NIM source and
  evidence review, and artifact/process cleanup all pass.
