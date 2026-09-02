<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-048 HTTP Lifecycle Inventory

Pinned upstream: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`

## Functional Inventory

| ID | Upstream behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| HTL-01 | Startup creates bounded clients for quote, macro, global-market, and news calls. | `ProductSceneViewModel` owns providers/services with explicit disposal. | Gap: ownership and configured timeout policy are not unified. |
| HTL-02 | Summarized news receives the extended timeout required for bounded retries. | `FinanceNewsService` uses a fixed 15-second client timeout. | Gap. |
| HTL-03 | Weather creates and disposes a bounded client per refresh. | `WorldWeatherService` owns one fixed-timeout client. | Gap. |
| HTL-04 | Request and response objects are disposed on all normal and fault paths. | Service methods dispose response streams; lifecycle tests are incomplete. | Gap: test depth. |
| HTL-05 | YFinance transport reuses its client and bounds idle/shutdown handling. | YFinance server/client runtime has explicit lifecycle controls. | Mapped; add cross-service lifecycle assertions. |
| HTL-06 | Timeout, cancellation, provider failure, and disposal are observable through circular tracing. | Current services trace failure states, but shared HTTP lifecycle evidence is incomplete. | Gap: trace coverage. |

The reverse scan covers the source and test files named by CR-048 and must be
repeated at closure with no unresolved lifecycle behavior.
