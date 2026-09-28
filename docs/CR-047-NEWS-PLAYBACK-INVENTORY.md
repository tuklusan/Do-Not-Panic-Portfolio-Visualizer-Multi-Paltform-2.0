<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-047 News Playback Inventory

Pinned upstream: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`

## Functional Inventory

| ID | Upstream behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| NPB-01 | Preserve ordered summarized items rather than collapsing them into one opaque paragraph. | `FinanceNewsService` result list and `NewsPlaybackController.SetHeadlines`. | Gap: parser/item preservation is incomplete. |
| NPB-02 | Append a style-specific closing quotation after successful summarized news. | No equivalent closing-quote item. | Gap. |
| NPB-03 | Cycle each headline through typing, reveal pause, scrolling, post-scroll pause, and between-headline delay. | `NewsPlaybackController.Step` and phase tests. | Mapped. |
| NPB-04 | Reconfigure wrapping from the current viewport while retaining the active headline when possible. | `ConfigureViewport` preserves headline index and resets segment state. | Mapped. |
| NPB-05 | Replace refreshed headlines without resetting equivalent playback content. | `SetHeadlines` compares normalized content before reset. | Mapped. |
| NPB-06 | Keep multi-line segment movement bounded to the visible line height and cap frame time. | `VisibleLineHeight`, bounded `Step`, and scrolling tests. | Mapped. |
| NPB-07 | Exercise playback, refresh replacement, viewport changes, and summarized output through focused tests. | `AmbientSceneServicesTests` plus service tests. | Gap: missing upstream item/quote assertions. |

## Reverse Scan

The reverse scan must confirm every active 2.0 playback behavior is mapped to the
upstream playback/service behavior or explicitly approved as an Avalonia-only
implementation detail before closure.
