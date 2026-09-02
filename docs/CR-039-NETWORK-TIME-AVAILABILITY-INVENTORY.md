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

# CR-039 Network Time and Availability Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| NTM-01 | Internet availability probing uses bounded HTTP requests, cached results, concurrent-probe coalescing, and explicit invalidation for recovery. | `InternetProbeService` uses shared bounded clients, caches results, coalesces concurrent refreshes, and exposes invalidation through `ConfigConnectivityService`. |
| NTM-02 | NTP correction tries `pool.ntp.org`, `0.pool.ntp.org`, and `1.pool.ntp.org` in order. | `NtpTimeService` uses the same three-host sequence. |
| NTM-03 | DNS resolution is limited to 1.5 seconds and each NTP host attempt to 4 seconds; cancellation propagates distinctly from timeout. | The portable service links caller cancellation, applies the exact DNS/host budgets, converts only internal timeout cancellation to timeout diagnostics, and rethrows caller cancellation. |
| NTM-04 | Successful synchronization returns explicit success, source host, and UTC time; complete failure falls back to the local UTC clock. | `NtpSyncResult` exposes those fields and `TryGetUtcNowAsync` returns the local-clock fallback after all hosts fail. |
| NTM-05 | NTP, timeout, DNS, and all-host failure diagnostics are safe and bounded. | NTP paths use shared circular `TraceLog` events with source/host and bounded metadata only. |
| NTM-06 | The scene periodically refreshes NTP only when connectivity exists and uses a recent offset for clocks without blocking visual updates. | `ProductSceneViewModel` refreshes NTP asynchronously from its background loop, retains a recent offset for clock rendering, and falls back to local UTC. |
| NTM-07 | Clock and market status refresh remains deterministic across local, online, offline, timeout, and market-boundary states. | The scene retains UTC/local timezone conversion and market-session formatting while NTP correction is optional and failure-isolated. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Fresh availability cache | Return cached result without a new probe. | Internet probe tests. |
| Concurrent cache miss | Coalesce callers into one bounded probe. | Concurrent probe tests. |
| Offline | Skip NTP and use local clock. | Scene refresh path. |
| DNS timeout | Record bounded DNS timeout and try the next host. | NTP implementation. |
| Host timeout | Record bounded host timeout and try the next host. | NTP implementation. |
| Caller cancellation | Propagate cancellation immediately; do not convert it to a normal timeout. | Linked-token handling. |
| All hosts fail | Return `Success=false`, source `Local clock`, and local UTC. | NTP fallback path. |
| Successful sync | Return source and corrected UTC; scene uses offset for recent clock updates. | NTP result and scene integration. |
| Recovery | Availability invalidation permits a fresh probe and later NTP attempt. | Connectivity service and background loop. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream availability, NTP, clock, and
associated tests, followed by scans of the migrated shared services,
presentation scene, and tests, found no unmapped behavior for NTM-01 through
NTM-07 after the NTP implementation was added.
