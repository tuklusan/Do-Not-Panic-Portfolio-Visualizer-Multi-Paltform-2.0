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

Open. Queued for execution after the current higher-priority closure work.

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
