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

# CR-091: Restore AI response-shape parity

## Objective

Make the migrated AI-news path accept the response shapes used by the
upstream-compatible OpenAI chat-completions contract and remain reliable when
an otherwise successful provider response has empty content.

## Functional Inventory

| ID | Upstream behavior or contract | 2.0 counterpart | Required result |
| --- | --- | --- | --- |
| AI-SHAPE-01 | Read ordinary scalar assistant content. | `FinanceNewsService.ExtractAiSummary`. | Preserve existing behavior. |
| AI-SHAPE-02 | Accept structured assistant content parts and completion text where providers return them. | `FinanceNewsService.ExtractContentText`. | Extract text without secrets or provider-specific crashes. |
| AI-SHAPE-03 | Retry an empty successful response within the bounded request budget before falling back. | `FinanceNewsService` retry loop. | Emit circular retry and terminal outcome events. |
| AI-SHAPE-04 | A real provider response must produce usable parsed AI items before hosted evidence counts as AI success. | Hosted news evidence gate. | Two independent complete hosted cycles prove AI success on every lane. |

## Required Gates

Re-read the complete upstream AI response and test implementations before
further edits, then independently rescan them at closure. Pass the migration
behavior gate at both stages, focused and full tests, NVIDIA review, license
and syntax gates, and artifact review. Do not weaken the hosted AI evidence
requirement to accept key presence or request-start evidence alone.

## Acceptance

- Scalar, structured-part, empty-content, malformed, retry, timeout, and HTTP
  failure cases have focused tests.
- Circular traces contain redacted request, response, retry, success, and
  bounded fallback outcomes.
- The real product produces AI-backed news evidence on all available hosted
  lanes in two independent complete 10-minute cycles.
- No product process, secret, or disposable artifact remains after validation.
