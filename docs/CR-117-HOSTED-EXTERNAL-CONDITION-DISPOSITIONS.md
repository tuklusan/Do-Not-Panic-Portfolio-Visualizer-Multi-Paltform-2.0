<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.
Based on original work by Supratim Sanyal of SANYALnet Labs.
DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
-->

# CR-117: Hosted External-Condition Dispositions

## Status

Open. Discovered while consuming hosted run `34065484870`.

## Objective

Restore a meaningful all-lane hosted result without hiding product failures.
The product soak, cleanup, screenshot, trace, RSS, and reviewer evidence gates
remain mandatory. A reviewer finding may be moved from blocking to advisory
only when a deterministic evidence rule matches an already-approved bounded
external condition and the raw finding is retained.

## Required behavior

| ID | Requirement | Acceptance |
| --- | --- | --- |
| EXT-01 | Every lane still produces the complete retained evidence contract. | Missing, malformed, secret-bearing, or hash-mismatched evidence remains blocking. |
| EXT-02 | Provider HTTP 429 or known provider-route HTTP 404 during the one upstream news refresh is recorded as quota/route-limited AI evidence. | RSS remains usable; `aiSuccessObserved` stays false; no artificial success is emitted. |
| EXT-03 | NTP all-host timeout is advisory only when the trace proves the documented local-clock fallback. | The raw finding is retained under `advisoryFindings`; both the project-specific `NTP-ALL-HOSTS-FAILED` identifier and NVIDIA's evidence-matched generic `B-001` alias are accepted. CR-039 remains the disposition authority. |
| EXT-04 | Render recovery is advisory only when the trace proves bounded recovery and subsequent heartbeats. | Unbounded, repeated, or unrecovered stalls remain blocking under CR-115; both the project-specific `RENDER-RECOVERY-*` identifiers and NVIDIA's evidence-matched generic `B-002` alias are accepted. |
| EXT-05 | NVIDIA output cancellation or unavailability never fabricates a PASS receipt. | The lane remains incomplete and the aggregate fails closed until a real receipt exists. |
| EXT-06 | YFinance upstream errors and unknown reviewer findings remain blocking until their CR disposition is independently closed. | No broad provider-error allowlist is permitted. |

## Functional Inventory

| EXT-01 | Hosted product lanes retain complete soak, cleanup, screenshot, dual-trace, RSS/AI, reviewer, and closure evidence. | `publish-six-rids.yml`, `Test-HostedSoakClosure.ps1`, and the retained lane artifacts. |
| EXT-02 | Provider HTTP 429 or known provider-route HTTP 404 is preserved as failed AI evidence and never fabricated as success. | `FinanceNewsService`, `AiNewsAccessValidationService`, and news evidence receipts. |
| EXT-03 | Only approved bounded NTP fallback, render recovery, and provider quota/route findings can become advisory. | Deterministic workflow normalization plus raw `advisoryFindings`; generic aliases require matching finding text and trace evidence. |
| EXT-04 | Missing, canceled, malformed, secret-bearing, or hash-mismatched reviewer evidence remains blocking. | Closure validator negative cases and aggregate gate. |

## Evidence from discovery run

Run `34065484870` produced `Passed` soak results, cleanup proof, four circular
trace files, and screenshots on all 21 product lanes. The aggregate failed in
the review/closure layer: quota-limited AI, bounded NTP/render diagnostics,
one YFinance upstream error, and one canceled NVIDIA review. This CR separates
those cases without changing the product cadence or falsely reporting AI
success.

The follow-up run `34068136420` produced `Passed` soak results on every lane
that reached closure and normalized the approved bounded NTP/render/provider
findings into retained `advisoryFindings`. Four blockers remained correctly
blocking: YFinance baseline/sync findings on macOS 14, Windows 2025, and
Xcode 27, plus an AI request with no terminal completion event on macOS latest.
The Ubuntu Slim reviewer call was canceled and therefore produced no semantic
receipt; this remains a fail-closed evidence gap under EXT-05.

Run `34070178397` confirmed that NVIDIA may express the already-approved NTP
and render diagnostics as generic `B-001` and `B-002` findings. The raw review
was otherwise complete, with successful RSS/AI evidence and a passed soak; the
lane failed only because those evidence-matched aliases were not normalized.
The normalization now accepts those two aliases only with the required trace
evidence and matching finding text; the same bounded rule covers the observed
NTP and provider-quota aliases `NTP-TIME-SYNC-FAILURE`,
`NTP-RECURRING-AT-SHUTDOWN`, and `AI-NEWS-SUMMARIZATION-QUOTA-FAILURE`.
Unknown generic findings remain blocking.
The fresh matrix for this bundle uses the current 20-lane set after CR-104
retirement.

## Closure gates

Run the upstream forward and reverse gates, focused closure self-tests, the
full Release suite, license/PowerShell/workflow gates, NVIDIA source review,
and one fresh serialized 20-lane matrix. Inspect every raw reviewer result,
advisory disposition, screenshot, both circular traces, RSS/AI evidence, and
closure record. Close only when all lanes have authoritative receipts and no
unapproved blocking finding remains.

## Reverse Upstream Gap Scan

The first implementation scan covers the upstream hosted acceptance contract,
the upstream RSS-first/AI fallback behavior, and the corresponding 2.0
workflow, validator, service, and tests. A second committed-disk scan is
required before closure; no provider exception may alter the upstream cadence
or fabricate a successful AI result.
