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
permission defect was real. An earlier five-minute run still showed placeholder
quote values, but later trace retrieval proved the downstream Yahoo response
path was functioning.

The next run retrieved the previously omitted `yfinance.circular.log`. It
records `ServerStartup`, successful protocol handshakes, and multiple
`QuoteResponseObserved` events with live prices. The quote path is therefore
working after the execute-bit fix; two fresh targeted runs are still required
for closure evidence.

Two additional keyed targeted runs on 2026-09-05 both completed successfully.
Each returned a real-product screenshot, RSS/AI evidence, and both circular
traces. Each `yfinance.circular.log` recorded `ServerStartup` on loopback port
14871 followed by multiple live `QuoteResponseObserved` events, with no
`ServerLaunchFailed` or `QuoteRequestFailed` events. The screenshot state was
explicitly market-closed where applicable and showed populated quote lanes.

## Evidence Summary

The two independent runs used the pushed build at commit `69f87d5` and the
Mac inventory entry `macos-x64-intel-big-sur|rumtuk|192.168.4.77`:

| Run | Server startup | First quote response | RSS/AI evidence | Screenshot |
| --- | --- | --- | --- | --- |
| `artifacts-r1` | `2026-09-05T07:19:55.8905120Z ... event=ServerStartup / port=14871 / bind_address=127.0.0.1` | `2026-09-05T07:20:09.0416110Z ... event=QuoteResponseObserved ... symbol=^VIX ... market_state=CLOSED` | `news-evidence.json`: `rssUsable=true`, `aiRequestObserved=true`, `aiSuccessObserved=true` | `screenshots/product-scene-20260905-072009.png` |
| `artifacts-r2` | `2026-09-05T07:22:47.2853020Z ... event=ServerStartup / port=14871 / bind_address=127.0.0.1` | `2026-09-05T07:22:59.5246950Z ... event=QuoteResponseObserved ... symbol=^VIX ... market_state=CLOSED` | `news-evidence.json`: `rssUsable=true`, `aiRequestObserved=true`, `aiSuccessObserved=true` | `screenshots/product-scene-20260905-072259.png` |

The table is a committed summary of the captured run artifacts. The complete
circular traces were inspected from the local run artifact directories before
cleanup; they are not claimed as independently retrievable evidence from this
document, and only minimum identifying excerpts are committed here rather than
turning project documentation into a runtime-log archive.

## Upstream Mapping

The upstream comparison was made against commit
`65a53bbbf0cf9af1058363f8939d464ca03858f8`:

| Upstream behavior | 2.0 mapping | Result |
| --- | --- | --- |
| Managed child server launch from the desktop scene | `YFinanceServerProcessManager` and `ProductSceneViewModel` | Mapped |
| Loopback quote service and observable quote responses | `YFinance.NET.Server`, `YFinanceProtocolRuntimeClient`, and circular traces | Mapped; port 14871 proven in both runs |
| Launchable published Unix executables | Mac archive extraction followed by executable-bit correction in `Invoke-LocalLabSoakCycle.ps1` | Mapped; no launch failures in either run |
| Runtime-owned server and product cleanup | Mac local-cycle cleanup and archive retrieval | Mapped; both cycle commands returned success |

The mandatory reverse scan and closure-gate record remain pending in
`docs/AUDIT_STATE.json`; this section does not claim CR-088 is closed.

## Gate Status

| Gate | Status | Authority |
| --- | --- | --- |
| Release build and test suite | Passed, 308 tests, 0 failures, 0 skips | `dotnet test DoNotPanicPortfolioVisualizer.sln -c Release --no-restore` on commit `69f87d5` |
| License headers and PowerShell syntax | Passed, 115 artifacts and 21 scripts | `build/Test-LicenseHeaders.ps1`, `build/Test-PowerShellSyntax.ps1` |
| Upstream forward/reverse migration gate | Pending; CR-088 inventory and closure records are not yet present in the tracker | `build/Test-MigrationBehaviorGate.ps1 -CrId CR-088` and `docs/AUDIT_STATE.json` |
| Remote home confinement | Passed by harness contract and run path | `build/Invoke-LocalLabSoakCycle.ps1`, Mac root `~/SOFTWARE_DEV/DNPPV_20` |
| One-gigabyte Mac budget | Enforced by the Mac driver; measurement is a required closure artifact | `build/vm/Invoke-MacConfigWindowValidation.sh` and CR-092 local-lab contract |
| Process cleanup and artifact retrieval | Passed in both runs | machine-result manifests `artifacts-r1` and `artifacts-r2`, each with `status=Passed` |

The gate table explains the status unambiguously: the runtime defect is fixed
and the required runtime evidence is complete, but the CR is not closure-ready
until the pending migration-gate records and explicit budget measurement are
added to the tracker.

## Required Closure Evidence

- Two targeted keyed Mac runs show server startup and quote-response evidence.
- The product screenshot shows populated ticker and market lanes, or an
  explicit market-closed state consistent with upstream behavior.
- RSS-first publication and AI success remain present.
- The circular trace contains no repeated server-start failure for the run.
- Remote home confinement, one-gigabyte budget, process cleanup, and artifact
  retrieval remain intact.
- Full tests and all repository gates pass.

## Current Evidence

The two fresh runs satisfy the Mac runtime, trace, screenshot, RSS/AI, and
cleanup portions of this CR. The CR as a whole is not yet closed: full tests,
all repository gates, fresh upstream forward/reverse records, and broader queue
dependencies remain mandatory closure conditions. The tracker therefore keeps
CR-088 open until those conditions are recorded.
