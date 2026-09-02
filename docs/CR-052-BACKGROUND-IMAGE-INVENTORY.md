<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-052 Background Image Inventory

Pinned upstream: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`

## Functional Inventory

| ID | Upstream behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| IMG-01 | Discover supported image files from configured folders. | `BackgroundImageService.GetImages`. | Mapped. |
| IMG-02 | Optionally include nested folders and ignore unsupported formats. | `includeSubfolders` and extension filtering. | Mapped. |
| IMG-03 | Preserve stable identity/path selection during catalog refresh. | `BackgroundCinemaController` and scene catalog refresh. | Mapped. |
| IMG-04 | Rotate backgrounds with cross-fade/slow zoom and retain valid current image. | Product scene background cinema state and shell bindings. | Mapped. |
| IMG-05 | Display attribution for selected bundled/custom imagery. | `BackgroundAttributions` and footer attribution text. | Mapped. |
| IMG-06 | Test bundled, custom, nested, unsupported, refresh, and attribution paths. | Media and scene tests plus physical artifact review. | Mapped. |

## Reverse Scan

No active 2.0 background-image behavior lacks an upstream counterpart.
