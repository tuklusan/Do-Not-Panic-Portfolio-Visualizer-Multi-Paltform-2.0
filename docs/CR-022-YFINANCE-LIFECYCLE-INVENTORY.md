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

# CR-022 YFinance Lifecycle Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| DAT-01 | Upstream behavior | 2.0 mapping |
| --- | --- | --- |
| DAT-01 | Pending requests settle during disposal; malformed frames break and reconnect the receive loop; corrupt responses are skipped safely; rate limits use Retry-After or exponential backoff and refresh sessions only for authentication/crumb failures. | `src/YFinance/YFinance.NET.Client`, `src/YFinance/YFinance.NET`, and the portable protocol/server tests implement and exercise these rules. |

## Reverse Upstream Gap Scan

The current YFinance client, transport, protocol, server, diagnostics, and test
artifacts were scanned back against the pinned upstream sources. Two successive
scans found no untracked missing behavior; all remaining broader runtime gaps
are explicitly queued under CR-023 and later.

## Exit Criteria

Require the focused lifecycle/degradation tests, self-contained publish, process
cleanup inspection, and fresh forward and reverse scans before closure.
