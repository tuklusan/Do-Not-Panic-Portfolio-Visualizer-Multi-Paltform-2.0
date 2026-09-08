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

# CR-075: Require AI Evidence in Hosted Soaks

**Status:** Closed by hosted acceptance run `34276086967`

## Objective

| CR-01 | Require key-free RSS and AI success evidence on hosted product soaks | hosted workflow and evidence reviewer | circular traces and manifests |

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| SOAKAI-01 | A hosted soak with an injected OpenRouter key must exercise AI access validation and summary generation. | `DNPPV_SOAK_REQUIRE_AI_NEWS` plus `FinanceNewsService` circular trace events. | Implemented on current main; absent from the older cycle artifact. |
| SOAKAI-02 | Hosted evidence review must inspect trace events, not only the manifest key-presence flag. | Post-soak review and CR-066 evidence contract. | Gap routed; fresh current-SHA cycle required. |
| SOAKAI-03 | A cycle without AI trace events cannot count toward CR-066 acceptance. | CR-066 validation ledger. | Implemented in this record. |

## Required Gates

The historical cycle `33689685166` was manually inspected from downloaded
artifacts. It provided 18 manifests, 18 screenshots, and non-empty circular
traces, and each manifest reported key injection, but no trace contained
`AiAccessValidation` or `AiSummary` events. The cycle ran at an older SHA and
is therefore evidence-incomplete, despite its GitHub conclusion being success.

The next cycle must run from current `main`, prove all 20 AI trace paths, pass
NVIDIA NIM artifact review, and remain separate from the incomplete cycle.

## Acceptance

- Every hosted runner has AI access and summary trace evidence.
- Missing AI trace evidence fails the post-soak review and routes a CR.
- The current evidence set must have 20 manifests, non-empty screenshots and trace pairs,
  cleanup proof, and a passing NVIDIA NIM artifact review.

## Latest Evidence

Hosted run `34276086967` is the closure proof. All 20 current lanes observed
the AI request, retained RSS/news evidence, and emitted complete screenshot,
trace-pair, semantic-review, and closure-record evidence. Provider quota
responses were retained as advisory AI evidence, not misclassified as cadence
or product failures. The aggregate hosted closure passed.
