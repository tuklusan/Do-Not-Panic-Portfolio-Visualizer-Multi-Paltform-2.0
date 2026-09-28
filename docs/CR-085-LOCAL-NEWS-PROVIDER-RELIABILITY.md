<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-085: Complete Local RSS And AI Provider Reliability

## Functional Inventory

| ID | Gap observed in the real product cycle | Required behavior | Status |
| --- | --- | --- | --- |
| NEWS-01 | Windows 11 produced `RssPlaybackReady` with 24 headlines but no AI request/success event. | The soak's provider-secret overlay must reach summarized-news mode in the Windows scheduled-task lane just as it does on Linux and Windows 10. | Open |
| NEWS-02 | Intel macOS exceeded the 180-second remote command limit before artifact retrieval. | The slow Mac lane must use a duration-aware deployment/command timeout and still retrieve circular trace, screenshots, and cleanup evidence. | Open |
| NEWS-03 | A failed local lane must not leave product/YFinance processes or remote cycle roots behind. | Re-run both affected lanes with explicit process and remote-root cleanup verification. | Open |

## Acceptance

- Windows 11 proves `RssPlaybackReady`, `AiSummaryRequestStarted`, and
  `AiSummarySucceeded` in the circular trace.
- Intel macOS completes a five-minute real-product cycle within its slow-lane
  budget and retrieves its evidence.
- Two successive targeted reruns pass with no new actionable defect.

## Gates

Re-read the upstream provider overlay, real product startup, and physical
validation scripts before implementation. Closure requires forward and reverse
source-cited inventories and the NVIDIA review gate.
