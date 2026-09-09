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
| AI-02 | Empty content is distinguished from transport failure and retried according to the configured retry policy. | `AiSummaryEmpty` and `AiSummaryRetryScheduled` trace path. | Complete |
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

Fresh hosted run `34298682486` on commit `90b269bbb5d15a5b64c5d27d1ddf64153e216f38`
completed the Windows 2022 soak and retained a complete inspected closure
record, settled screenshot, both circular traces, and a semantic NVIDIA
`TEST_ARTIFACT` PASS. Its evidence records `rssUsable=true`, `rssPublished=true`,
and `aiRequestObserved=true`, but `aiSuccessObserved=false` with
`aiQuotaLimited=true` and `aiExternalFailureDisposition=provider-quota-rate-limited`.
The soak passed and cleaned up its process; this fresh result attributes the
remaining acceptance gap to provider quota rather than silently treating an
external failure as product success.

## Upstream behavior inventory

The pinned upstream baseline is commit
`65a53bbbf0cf9af1058363f8939d464ca03858f8`. The source-cited inventory below
was completed before any CR-100 product change:

| Upstream source | Observed behavior | 2.0 mapping and gap disposition |
| --- | --- | --- |
| `src/PortfolioSaver.Presentation/Services/FinanceNewsService.cs` (AI request/retry path, upstream lines 416-461) | Sends the bounded AI request, retries cancellation/retryable failures, distinguishes empty choices and missing message content, and returns an attributable structured fallback after exhaustion. | `src/DoNotPanicPortfolioVisualizer.Presentation/Services/FinanceNewsService.cs` retains the equivalent bounded retry, empty-content trace, retry scheduling, fallback, and success-marker path. |
| `tests/PortfolioSaver.Tests/Services/FinanceNewsServiceTests.cs` (HTTP failure, credential, strict-format, malformed-response, and successful-content cases) | Covers throttled/service-unavailable responses, invalid credentials, strict JSON fallback, malformed JSON, and valid content extraction with deterministic retry-delay assertions. | The migrated service test suite contains the corresponding focused cases; a fresh focused run and hosted Windows 2022 proof remain required. |
| `src/PortfolioSaver.Core/Services/OpenRouterModelResolver.cs` and `tests/PortfolioSaver.Tests/Services/OpenRouterModelResolverTests.cs` | Resolves the configured/free model and preserves provider fallback behavior used by the AI request path. | The migrated resolver and tests are the mapped counterparts; no unmapped resolver behavior was found. |
| `build/validation/Analyze-InstalledSoakTrace.ps1` and `build/validation/Run-InstalledSoakOnce.local.ps1` | Retains product soak traces and diagnoses AI request, parse, retry, and fallback outcomes without treating an external provider failure as a process failure. | The 2.0 hosted workflow and `build/Invoke-ProductSoak.ps1` provide the mapped evidence and cleanup path; fresh Windows 2022 evidence is the remaining gap. |

The inventory found no missing upstream behavior requiring a new product
algorithm before validation. The open work is to prove the existing parity on
the current Windows 2022 hosted lane and close any evidence-specific gap found
by that run without weakening the real AI-output requirement.

## Acceptance

Focused tests cover empty, valid, malformed, throttled, and provider-fallback
responses, including recovery from an HTTP-successful empty response to later
usable content. The Windows 2022 lane currently records
`aiRequestObserved=true` but remains quota-limited without
`aiSuccessObserved=true`; all hosted evidence and closure gates pass without
weakening the requirement for real AI news output. CR-100 remains open pending
a fresh Windows 2022 provider-success proof.
