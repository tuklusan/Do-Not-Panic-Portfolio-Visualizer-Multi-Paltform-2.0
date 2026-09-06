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
The existing bounded local circular traces remain authoritative local evidence;
forwarding is an additional transport and must never weaken local retention,
secret scanning, cleanup, or reviewer gates.

## Functional Inventory

| ID | Requirement |
| --- | --- |
| LOG-01 | Every project-owned trace/log event selected by the logging contract is forwarded to the configured hostname and UDP port only. |
| LOG-02 | The destination hostname is resolved and transmitted with a UDP socket; no TCP, HTTP, HTTPS, or alternate endpoint fallback is permitted. |
| LOG-03 | Every datagram uses a documented Unix syslog format, facility/severity mapping, timestamp, hostname/app identity, and bounded payload length. |
| LOG-04 | Oversized events are deterministically bounded or split without corrupting syslog framing; a single event cannot block product startup or UI work. |
| LOG-05 | Forwarding is best-effort and non-blocking; DNS failure, socket failure, packet loss, and shutdown do not crash the product or alter local circular traces. |
| LOG-06 | Credentials, API keys, passwords, bearer values, private review material, and user secrets are redacted before serialization and never sent in a datagram. |
| LOG-07 | The destination is configuration-locked to the stated endpoint unless an explicitly documented test override is used; overrides cannot silently reach production. |
| LOG-08 | UDP forwarding itself is observable through bounded local trace metadata without recursively forwarding or duplicating its own diagnostics. |
| LOG-09 | Windows, Linux, macOS Intel, macOS ARM, hosted runners, and local lab harnesses use the same cross-platform implementation. |
| LOG-10 | Unit, integration, wire-format, failure, secret, shutdown, and cross-platform tests prove the contract; test packets use a local UDP receiver and never the production VPS. |

## Scope and Safety Boundary

This CR does not replace the two circular trace files, change their size
limits, add a remote reviewer, or permit arbitrary log exfiltration. The
implementation must define the exact project-owned event set, redact before
the network boundary, and document whether forwarding is enabled by default.
No TCP fallback, HTTP fallback, alternate DNS target, unbounded queue, or
blocking network call is acceptable.

## Acceptance Criteria

- Source-cited upstream and current-2.0 logging inventories pass forward and
  reverse migration gates with two successive zero-gap scans.
- A local receiver verifies valid Unix syslog datagrams arrive over UDP at the
  configured port and that no TCP/HTTP connection is attempted.
- Tests prove DNS failure, unreachable endpoint, full socket, malformed input,
  packet-size boundary, shutdown, and sustained-event behavior are nonfatal.
- Synthetic API keys, bearer tokens, passwords, and review material are absent
  from every captured datagram and local forwarding diagnostic.
- The implementation passes NVIDIA review, license/syntax/build/test gates,
  real-product 10-minute hosted/local validation, trace inspection, cleanup,
  commit, and push requirements.
- The final CR record documents the exact wire format, facility/severity map,
  event allowlist, redaction rules, default/override policy, and evidence.
