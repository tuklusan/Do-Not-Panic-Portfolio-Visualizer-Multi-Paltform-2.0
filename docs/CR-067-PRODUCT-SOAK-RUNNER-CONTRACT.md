<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
-->

# CR-067 Product Soak Runner Contract

## Functional inventory

| ID | Required behavior | Implementation/evidence |
| --- | --- | --- |
| SOAK-01 | Launch the real published Avalonia product with an isolated local-data root. | `build/Invoke-ProductSoak.ps1` |
| SOAK-02 | Keep the process alive for a caller-selected duration and fail if it exits early. | `build/Invoke-ProductSoak.ps1` |
| SOAK-03 | Retrieve only the product's size-bounded circular trace pair. | `build/Invoke-ProductSoak.ps1` and `trace/` artifact |
| SOAK-04 | Record run identity, timing, samples, outcome, and cleanup status in machine-readable evidence. | `soak-result.json` |
| SOAK-05 | Terminate the launched process on pass, failure, or interruption. | `finally` cleanup path |
| SOAK-06 | Do not redirect product stdout/stderr into arbitrary product log files. | Process starts without output redirection |
| SOAK-07 | Permit a secret OpenRouter key to be injected into the child process for AI-news validation without persisting or echoing it. | `DNPPV_OPENROUTER_API_KEY` or standard `OPENROUTER_API_KEY` protected input; provider secret overlay consumes it only in memory |
| SOAK-08 | Request a settled real-product scene capture and fail the hosted lane when the PNG is absent. | `DNPPV_PRODUCT_CAPTURE_PATH`, `ProductShellWindow`, and screenshot evidence |
| SOAK-09 | In soak mode, capture timestamped settled real-product screenshots after warmup and every 30 minutes thereafter, canceling the capture loop during shutdown. | `DNPPV_PRODUCT_CAPTURE_INTERVAL_MINUTES=30`, timestamped PNG set, count/hash manifest, and artifact review |
| SOAK-10 | Poll launched-process health at a 30-second cadence and avoid repeated unchanged status output between observations. | `PollIntervalSeconds=30`; matrix observer contract in CR-068 |

## Acceptance

The runner must pass script syntax and license gates, execute a short controlled
real-product rehearsal, produce a passing result with circular trace and
real-product screenshot evidence, and leave no product process running. When AI-news validation is requested, the
operator or CI secret store supplies `DNPPV_OPENROUTER_API_KEY` or
`OPENROUTER_API_KEY` (the explicit
parameter remains available for controlled callers); the key must never
appear in `soak-result.json`, traces, screenshots, logs, or command output. It is
a reusable primitive; the hosted
matrix, four lab machines, DeepSeek evidence review, and six progressive
durations are tracked by CR-068 and CR-069.

## Upstream and reverse gates

The upstream comparison reviewed Ludo-Arena's real-app execution, Xvfb handling,
physical self-hosted lanes, proof artifact collection, and explicit process
cleanup. The 2.0 runner preserves those behaviors while using DNPPV's circular
trace contract. A reverse scan of the current script and this inventory found
no unmapped behavior; two successive zero-gap scans are required at closure.

**Status:** Open
