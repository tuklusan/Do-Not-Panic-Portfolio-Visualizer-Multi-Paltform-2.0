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

The saved hosted job log shows the product soak itself passed, then the
evidence-extraction step failed closed. The hosted and local evidence parsers
were matching `/` between structured trace fields, while the canonical
circular trace writer emits ` | `. This was a harness classification defect,
not evidence that the Windows ARM product execution or provider access failed.
Both parsers now accept the canonical pipe delimiter and retain compatibility
with the older slash form. A focused matcher self-test, workflow gate, harness
freeze gate, and full .NET test suite pass locally. Fresh hosted proof remains
required before closure.

Fresh candidate run `34290530942` on commit `5461c63235a396371e610e9b8d9ecc54e45ed31c`
completed the Windows ARM product soak with usable RSS publication and an AI
request, but the provider returned HTTP 429 on both attempts; the retained
trace therefore correctly contains no `AiSummarySucceeded` event and the
key-free evidence records `aiQuotaLimited=true`. The candidate also exposed a
review-disposition defect: the reviewer treated quota-shaped AI findings and a
10-minute RSS-refresh expectation as authoritative even though the project
policy treats provider 4xx responses as advisory and the trace proves fresh
RSS playback was published. The workflow now retains those raw findings while
normalizing only those evidence-matched conditions to advisory. A fresh
candidate after this correction is still required before closure, and a
successful Windows ARM AI response remains required for the CR's success-path
claim.

The operator-authorized autonomous resume directive explicitly authorizes this
workflow harness correction. The change preserves the frozen harness contract:
raw reviewer output remains retained, only evidence-matched external/quota
conditions are normalized, and no product or test harness behavior is weakened.

## Acceptance

The Windows ARM lane emits the required RSS and AI markers, passes its news
evidence gate, produces a complete inspected closure record, and leaves no
residual product or helper process. Current status: implementation corrected;
pending fresh hosted validation.
