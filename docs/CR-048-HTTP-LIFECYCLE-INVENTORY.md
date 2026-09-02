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
| HTL-01 | Startup creates bounded clients for quote, macro, global-market, and news calls. | `ProductSceneViewModel` owns providers/services with explicit disposal; ambient services use the shared `HttpClientFactory` handler. | Mapped. |
| HTL-02 | Summarized news receives the extended timeout required for bounded retries. | `FinanceNewsService` applies the settings-derived playback timeout through a linked cancellation budget. | Mapped. |
| HTL-03 | Weather creates and disposes a bounded client per refresh. | `WorldWeatherService` uses the shared handler, explicit timeout, and deterministic disposal. | Mapped. |
| HTL-04 | Request and response objects are disposed on all normal and fault paths. | Service clients, response streams, and JSON streams are disposed; injectable lifecycle tests cover the ambient services. | Mapped. |
| HTL-05 | YFinance transport reuses its client and bounds idle/shutdown handling. | YFinance server/client runtime has explicit lifecycle controls. | Mapped; add cross-service lifecycle assertions. |
| HTL-06 | Timeout, cancellation, provider failure, and disposal are observable through circular tracing. | Current services trace failure states and bounded timeout/cancellation paths; full suite covers the failure contracts. | Mapped. |

The reverse scan covers the source and test files named by CR-048 and must be
repeated at closure with no unresolved lifecycle behavior.
