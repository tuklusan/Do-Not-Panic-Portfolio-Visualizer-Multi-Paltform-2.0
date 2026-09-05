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

# CR-098: Restore Windows ARM RSS and AI News Soak Evidence

## Objective

Determine why the Windows ARM hosted product soak completes and validates AI
access, but emits neither usable RSS playback evidence nor an AI news request
and success pair.

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| NE-01 | Windows ARM real-product soak publishes usable RSS playback evidence. | Product RSS pipeline and hosted `news-evidence.json` assertion. | Open |
| NE-02 | Windows ARM real-product soak observes the configured AI news request and success path. | Product AI news orchestration and circular trace markers. | Open |
| NE-03 | Failure diagnosis distinguishes feed freshness, playback scheduling, credentials, and platform behavior. | Two circular traces plus `news-evidence.json`. | Open |

## Upstream and Reverse Gates

Before implementation, scan upstream RSS playback, AI news orchestration,
startup scheduling, and Windows-specific launch behavior line by line. Before
closure, reverse-scan those same paths and prove no mapped behavior is missing.

## Evidence

Hosted run `33979739957`, Windows ARM artifact, recorded `rssUsable=false`,
`rssPublished=false`, `aiRequestObserved=false`, and `aiSuccessObserved=false`
despite `openRouterKeyProvided=true` and successful AI access validation.

## Acceptance

The Windows ARM lane emits the required RSS and AI markers, passes its news
evidence gate, produces a complete inspected closure record, and leaves no
residual product or helper process.
