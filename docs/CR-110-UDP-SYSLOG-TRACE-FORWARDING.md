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

# CR-110: UDP-Only Unix Syslog Trace Forwarding

## Status

Closed after the exact reviewed candidate passed local transport tests, the
hosted 20-lane matrix, and an exact-candidate physical Windows 10 cycle.

## Objective

Forward project traces and logs to exactly
`sanyalnet-oracle-vps2.duckdns.org:65514` using UDP-only Unix syslog framing.
The requestor explicitly authorizes forwarding **all project-owned logs and
traces** because the destination is part of the requestor's secure network.
There must be no privacy-based suppression, event allowlist, content filtering,
or forwarding-time redaction. The existing bounded local circular traces remain
authoritative local evidence. Each new entry appended to either circular trace
file must be forwarded live as it is added when forwarding is enabled.
Forwarding is enabled only when the environment variable
`DNPPV_TRACE_FORWARD` exists and its value is exactly `Y` or `1` (case-sensitive);
otherwise no remote socket is opened. Forwarding is an additional transport
and must never weaken local retention, cleanup, or reviewer gates.

## Functional Inventory

| ID | Requirement |
| --- | --- |
| LOG-01 | Every project-owned trace/log event emitted by the product, hosted GitHub-runner harnesses, or local-lab harnesses, including every new entry appended to each bounded circular trace file, is forwarded live to the configured hostname and UDP port only when enabled; no event is suppressed for privacy reasons. |
| LOG-02 | The destination hostname is resolved and transmitted with a UDP socket; no TCP, HTTP, HTTPS, or alternate endpoint fallback is permitted. |
| LOG-03 | Every datagram uses a documented Unix syslog format, facility/severity mapping, timestamp, hostname/app identity, and bounded payload length. |
| LOG-04 | Oversized events are deterministically bounded or split without corrupting syslog framing; a single event cannot block product startup or UI work. |
| LOG-05 | Forwarding is best-effort and non-blocking; DNS failure, socket failure, packet loss, and shutdown do not crash the product or alter local circular traces. |
| LOG-06 | Forwarding performs no privacy-based content suppression or redaction. Datagrams contain the complete event content exactly as emitted by the source logger; this is explicitly authorized for the secure destination. |
| LOG-07 | The destination is configuration-locked to the stated endpoint unless an explicitly documented local test receiver override is used; overrides cannot silently reach production. |
| LOG-08 | UDP forwarding itself is observable through bounded local trace metadata without recursively forwarding or duplicating its own diagnostics; each circular-file append is forwarded once, at append time when `DNPPV_TRACE_FORWARD` is enabled. |
| LOG-09 | Windows, Linux, macOS Intel, macOS ARM, hosted runners, and local lab harnesses use the same cross-platform implementation and honor the same opt-in variable. |
| LOG-10 | Unit, integration, wire-format, failure, complete-payload-fidelity, shutdown, and cross-platform tests prove the contract; test packets use a local UDP receiver and never the production VPS. |

## Upstream behavior inventory

The pinned upstream baseline is commit
`65a53bbbf0cf9af1058363f8939d464ca03858f8`. The source-cited inventory below
was completed before implementation work:

| Upstream source | Observed behavior | 2.0 mapping and gap disposition |
| --- | --- | --- |
| `src/PortfolioSaver.Shared/Diagnostics/TraceLog.cs` | Structured events are queued to a background worker, bounded to 1900-character lines, and written to a fixed-size circular file under app data with a synchronized cursor and index checkpointing. | `src/DoNotPanicPortfolioVisualizer.Shared/Diagnostics/TraceLog.cs` is the mapped implementation. UDP forwarding must be opt-in and must not alter local queue, retention, or cleanup behavior. |
| `src/PortfolioSaver.Shared/Diagnostics/CappedFileLogWriter.cs` | Harness/log output is serialized and size-capped with deterministic rotation; rotation failures fall back to local append. | The fresh 2.0 line has no direct `CappedFileLogWriter` counterpart; its script/harness logs are a routed migration gap and must be covered by the new transport integration without weakening local output. |
| `src/PortfolioSaver.Shared/Diagnostics/CircularTraceSettings.cs` | Trace size is environment-configurable and clamped to documented minimum/maximum bounds. | `src/DoNotPanicPortfolioVisualizer.Shared/Diagnostics/CircularTraceSettings.cs` is the mapped settings contract; forwarding cannot change bounds. |
| `YFinance.net/YFinance.NET/Diagnostics/YFinanceCircularTraceSink.cs` | The secondary YFinance trace uses the same bounded asynchronous circular-write pattern with concurrency, burst, index-recovery, and shutdown-safe behavior. | `src/YFinance/YFinance.NET/Diagnostics/YFinanceCircularTraceSink.cs` is the mapped secondary stream; each append remains local-authoritative and is a separate forwarding event only when opted in. |
| `tests/PortfolioSaver.Tests/Services/TraceLogTests.cs`, `CappedFileLogWriterTests.cs`, and `YFinanceCircularTraceSinkTests.cs` | Tests prove bounded files, cursor recovery, concurrent writes, burst draining, and local retention under failure. | `tests/DoNotPanicPortfolioVisualizer.Tests/` contains the mapped tests; CR-110 adds local UDP receiver, wire-format, payload-fidelity, failure, shutdown, and opt-in matrix coverage without using the production VPS. |
| `build/validation/Analyze-InstalledSoakTrace.ps1` and `build/validation/Run-InstalledSoakOnce.local.ps1` | Validation retrieves and analyzes the two trace streams from product/local soak execution. | `build/Invoke-ProductSoak.ps1`, hosted matrix evidence, and local-lab scripts are the mapped validation surfaces; remote forwarding remains optional and secondary to retained traces. |

The inventory found no upstream remote-transport behavior to copy. The mapped
gap is a new cross-platform, opt-in UDP syslog transport and its harness/test
integration; local circular writers, boundedness, failure nonfatality, and
evidence retention are required parity constraints.

## Scope and Safety Boundary

This CR does not replace the two circular trace files, change their size
limits, or add a remote reviewer. When `DNPPV_TRACE_FORWARD=Y` or `1`, the
shared implementation forwards the complete product, hosted-runner harness,
and local-lab harness event streams, including both circular-file append
streams, under the explicit secure-network authorization; with the variable
absent or any other value, product and harnesses perform no remote forwarding;
there is no privacy-based event allowlist or forwarding-time redaction. No
TCP fallback, HTTP fallback, alternate DNS target, unbounded queue, or blocking
network call is acceptable.

The operator-authorized CR-110 harness integration is an approved frozen-harness
change. `build/Invoke-ProductSoak.ps1` and
`build/Invoke-LocalLabSoakCycle.ps1` route their lifecycle and sample events
through the same shared forwarding entry point when the exact opt-in variable is
enabled, while preserving their existing local output and cleanup behavior. The
freeze approval is recorded for the candidate commit and is consumed only for
this CR-110 change.

## Acceptance Criteria

- Source-cited upstream and current-2.0 logging inventories pass forward and
  reverse migration gates with two successive zero-gap scans.
- A local receiver verifies valid Unix syslog datagrams arrive over UDP at the
  configured port for product and harness events, and that no TCP/HTTP
  connection is attempted when
  `DNPPV_TRACE_FORWARD=Y`; tests also prove that absent, `0`, and other values
  produce no remote socket or datagram.
- Tests prove DNS failure, unreachable endpoint, full socket, malformed input,
  packet-size boundary, shutdown, and sustained-event behavior are nonfatal.
- Captured datagrams and local forwarding diagnostics prove that complete
  project-owned event content is forwarded without privacy-based suppression;
  forwarding must not silently drop or redact an event.
- The implementation passes NVIDIA review, license/syntax/build/test gates,
  real-product 10-minute hosted/local validation, trace inspection, cleanup,
  commit, and push requirements.
- The final CR record documents the exact wire format, facility/severity map,
  complete-event forwarding authorization, `DNPPV_TRACE_FORWARD` enablement
  policy, test override policy, and evidence.

## Closure Evidence

- Candidate `7483f7e51f86710597f8b2174642b1d7395b81e3` passed the focused UDP
  receiver tests, full Release tests, syntax/license/workflow/freeze gates,
  NVIDIA CODE review, protected pre-push gates, and the fresh Closure
  migration gate with two successive zero-gap scans.
- Hosted run `34306816264` completed with
  `HOSTED_SOAK_CLOSURE=Passed;RUN_ID=34306816264;LANES=20;REMOTE_REVIEW_CALLS=0`.
  Retained lanes include Windows, Linux, macOS Intel, and macOS ARM, with
  passed ten-minute soaks, both circular traces, complete inspected closure
  records, cleanup, and semantic TEST_ARTIFACT PASS results.
- Exact-candidate physical cycle `dnppv2-local-cycle-cr110-exact` recorded a
  passed ten-minute Windows 10 real-product validation with screenshots, both
  circular traces, and clean remote teardown. The all-machine attempt was
  retained as harness-failure evidence because the frozen Mac path requires
  interactive SSH; it was not reclassified as product unavailability or PASS.
- `DNPPV_TRACE_FORWARD` accepts only exact `Y` or `1`; the production endpoint
  remains locked to `sanyalnet-oracle-vps2.duckdns.org:65514`, while the local
  receiver override is guarded by `DNPPV_TRACE_FORWARD_TEST_OVERRIDE=Y`.
