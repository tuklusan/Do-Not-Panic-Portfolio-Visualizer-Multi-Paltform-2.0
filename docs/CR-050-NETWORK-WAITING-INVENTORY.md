<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-050 Network Waiting Inventory

Pinned upstream: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`

## Functional Inventory

| ID | Upstream behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| NWT-01 | Show a branded waiting overlay when startup connectivity is unavailable. | `MainViewModel.ShowNetworkLockOverlay` and product-shell overlay bindings. | Mapped. |
| NWT-02 | Keep the existing scene visible while retrying network-dependent work. | Scene startup and degraded lane handling retain current view models. | Mapped. |
| NWT-03 | Place the overlay within the active viewport and recover its position after resize/fullscreen. | Product shell resize path and bounded overlay layout. | Mapped. |
| NWT-04 | Replace waiting state with live/recovered state without duplicating scene elements. | Network availability state and refresh loops update existing bindings. | Mapped. |
| NWT-05 | Exercise online, offline, retained-cache, recovery, resize, and fullscreen states. | Existing degraded, shell, and physical validation paths cover these cases. | Mapped. |

## Reverse Scan

No active 2.0 network-waiting behavior lacks an upstream counterpart.
