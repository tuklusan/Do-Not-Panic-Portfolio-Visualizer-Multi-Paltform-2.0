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

# CR-073: Bound and Trace AI News Summary Requests

## Functional Inventory

| ID | Upstream/product behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| AI-01 | A configured AI-news path must be exercised by real product validation. | `DNPPV_SOAK_REQUIRE_AI_NEWS` and `ProviderSecretStoreService`. | Implemented |
| AI-02 | A slow provider must not be cut off by a shorter client timeout than the configured request budget. | `FinanceNewsService` derives its client timeout from `AppSettings.HttpTimeoutSeconds`. | Implemented |
| AI-03 | AI request start, response, success, empty, and failure outcomes must be visible in the circular trace without secrets. | `FinanceNewsService` `AiSummary*` trace events. | Implemented |

## Required Gates

Complete a source-cited upstream and reverse behavior inventory before
implementation and repeat both scans at closure. Do not record an AI pass from
access validation alone; the real summary request must produce a response or a
bounded, explicit failure event.

## Acceptance

- Linux, Windows 10, and Windows 11 real-product runs exercise the AI path.
- At least one real summary response records HTTP status and success without
  exposing the API key.
- Slow/failing requests terminate within the configured budget and emit
  `AiSummaryFailed` when appropriate.
- Full build/test, license/syntax, NVIDIA NIM source/evidence, artifact, and
  process-cleanup gates pass.
