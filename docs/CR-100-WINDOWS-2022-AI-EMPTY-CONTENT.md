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

# CR-100: Recover AI News From Empty Successful Responses

## Objective

Ensure the real-product soak treats an HTTP-successful but empty AI response as
a recoverable provider response and eventually records a successful AI news
summary when the provider returns usable content.

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| AI-01 | A successful AI HTTP response with usable content produces an AI success marker and news output. | `FinanceNewsService.SummarizeAsync` and circular trace markers. | Open |
| AI-02 | Empty content is distinguished from transport failure and retried according to the configured retry policy. | `AiSummaryEmpty` and `AiSummaryRetryScheduled` trace path. | Partial |
| AI-03 | Repeated empty responses fail with an attributable diagnostic without falsely passing the AI evidence gate. | Hosted `news-evidence.json` and soak assertion. | Partial |
| AI-04 | Provider/model configuration and response extraction are equivalent across all supported hosted platforms. | OpenRouter request and response parsing tests plus hosted matrix. | Open |

## Upstream and Reverse Gates

Before implementation, scan the complete upstream AI news request, response
parsing, retry, fallback, and test paths line by line. Before closure,
independently rescan those paths and prove that no mapped behavior is missing.

## Evidence

Hosted run `33982819747`, lane `windows-2022`, recorded HTTP 200 followed by
`AiSummaryResponseParsed` with `extraction_path=none`, `AiSummaryEmpty`, a retry,
another HTTP 429, and a later HTTP 200 that was still parsed as empty. The lane
had usable RSS and observed the AI request but failed
`aiSuccessObserved`; the product soak itself otherwise completed and cleaned up.

## Acceptance

Focused tests cover empty, valid, malformed, throttled, and provider-fallback
responses; the Windows 2022 lane records `aiRequestObserved=true` and
`aiSuccessObserved=true`; all hosted evidence and closure gates pass without
weakening the requirement for real AI news output.
