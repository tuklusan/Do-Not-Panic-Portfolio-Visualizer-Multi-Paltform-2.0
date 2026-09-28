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

# CR-031 Protocol Framing and Disposal Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| NET-01 | Length-prefixed transport rejects negative, oversized, and truncated frames and reads exactly the declared payload. | `LengthPrefixedProtocolStream` enforces `MaxMessageBytes`, handles EOF at frame boundaries, and uses exact asynchronous reads. |
| NET-02 | Empty frames, pooled buffers, and payload lifetime are deterministic and safely disposable. | Read and pooled-read APIs preserve empty-frame semantics; `PooledProtocolPayload` returns buffers once and rejects access after disposal. |
| NET-03 | Request/response envelopes carry checksums and malformed or mismatched payloads fail closed. | `ProtocolIntegrity` stamps and verifies checksums; client/server fault paths reject missing or mismatched integrity metadata. |
| NET-04 | Transport operations observe cancellation and late responses for canceled requests do not publish stale results. | Client request registrations cancel pending operations, track late canceled responses, and propagate cancellation through the receive/write paths. |
| NET-05 | Client/server disposal is idempotent, releases pending work and sockets, and tolerates fault cleanup. | `YFinanceServerClient` and server paths guard disposal with interlocked state, fail pending requests, cancel connection work, and perform best-effort resource cleanup. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Zero-length frame | Return an empty payload without allocation failure. | Protocol stream tests. |
| Negative or oversized length | Reject before allocation. | Protocol stream tests. |
| Truncated prefix or body | Fail rather than accept partial data. | Protocol stream and stalled-client tests. |
| Missing or wrong checksum | Reject and trace integrity failure. | Client/server protocol tests. |
| Canceled request with late response | Cancel caller and discard late response. | Client cancellation tracking tests. |
| Repeated synchronous/asynchronous disposal | Complete without double-release or leaked pending tasks. | Client and pooled-payload disposal tests. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream protocol, client/server, and test
implementations found no unmapped behavior for NET-01 through NET-05. The
portable implementation already contains the corresponding safeguards; this CR
requires verification and evidence closure rather than new product code.
