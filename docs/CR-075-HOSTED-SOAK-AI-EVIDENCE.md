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

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| SOAK-AI-01 | A hosted soak with an injected OpenRouter key must exercise AI access validation and summary generation. | `DNPPV_SOAK_REQUIRE_AI_NEWS` plus `FinanceNewsService` circular trace events. | Implemented on current main; absent from the older cycle artifact. |
| SOAK-AI-02 | Hosted evidence review must inspect trace events, not only the manifest key-presence flag. | Post-soak review and CR-066 evidence contract. | Gap routed; fresh current-SHA cycle required. |
| SOAK-AI-03 | A cycle without AI trace events cannot count toward CR-066 acceptance. | CR-066 validation ledger. | Implemented in this record. |

## Required Gates

The completed cycle `33689685166` was manually inspected from downloaded
artifacts. It provided 18 manifests, 18 screenshots, and non-empty circular
traces, and each manifest reported key injection, but no trace contained
`AiAccessValidation` or `AiSummary` events. The cycle ran at an older SHA and
is therefore evidence-incomplete, despite its GitHub conclusion being success.

The next cycle must run from current `main`, prove all 18 AI trace paths, pass
NVIDIA NIM artifact review, and remain separate from the incomplete cycle.

## Acceptance

- Every hosted runner has AI access and summary trace evidence.
- Missing AI trace evidence fails the post-soak review and routes a CR.
- The evidence set has 18 manifests, non-empty screenshots and trace pairs,
  cleanup proof, and a passing NVIDIA NIM artifact review.

## Latest Evidence

Cycle `33902675076` proved the real product completed its 10-minute soak on
`macos-26-intel`, but its circular evidence did not contain the required AI
summary success event. The matrix therefore failed closed rather than treating
secret injection as proof. The runner-specific environment, endpoint response,
and trace path must be diagnosed and a fresh current-SHA cycle must pass on all
18 runners before this CR can close.
