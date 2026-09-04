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

# CR-088: Restore Intel Mac YFinance Quote-Server Startup

## Functional Inventory

| CR-01 | Start the bundled YFinance server and prove live quotes on Intel Mac | Mac runtime and validation driver | circular trace evidence |

## Upstream Behavior Gate

Before implementation, inspect the upstream YFinance launch manager, published
server bundle, Mac harness, and circular-trace behavior line by line. Record the
source revision and map every relevant launch, permission, runtime, and cleanup
behavior here. Repeat the reverse scan at closure.

## Observed Gap

The keyed Intel Mac real-product run showed successful RSS and AI evidence, but
the circular trace recorded repeated `QuoteRequestFailed` events with
`Failed to start YFinance.NET.Server`. The product consequently entered
degraded quote lanes. The corrected Mac artifact layout is now direct under the
machine artifact root, so the trace is available for diagnosis.

The subsequent run after restoring the executable bit no longer recorded
`ServerLaunchFailed` or `QuoteRequestFailed`, which confirms the launch
permission defect was real. A five-minute run still showed placeholder quote
values and no `QuoteResponseObserved` event, so the downstream Yahoo response
path remains open for separate diagnosis.

The next run retrieved the previously omitted `yfinance.circular.log`. It
records `ServerStartup`, successful protocol handshakes, and multiple
`QuoteResponseObserved` events with live prices. The quote path is therefore
working after the execute-bit fix; two fresh targeted runs are still required
for closure evidence.

## Required Closure Evidence

- Two targeted keyed Mac runs show server startup and quote-response evidence.
- The product screenshot shows populated ticker and market lanes, or an
  explicit market-closed state consistent with upstream behavior.
- RSS-first publication and AI success remain present.
- The circular trace contains no repeated server-start failure for the run.
- Remote home confinement, one-gigabyte budget, process cleanup, and artifact
  retrieval remain intact.
- Full tests and all repository gates pass.
