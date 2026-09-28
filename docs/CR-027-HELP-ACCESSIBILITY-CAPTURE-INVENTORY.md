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

# CR-027 Help, Accessibility, and Capture Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| UI-03 | Upstream behavior | 2.0 mapping |
| --- | --- | --- |
| UI-03 | Help/About exposes product identity, publisher, author, attribution, and complete license text; interactive controls have usable names and validation captures identify the real rendered window, dimensions, and provenance. | `src/DoNotPanicPortfolioVisualizer.App/Views/AboutWindow.axaml`, `ProjectLicenseService`, explicit Avalonia automation names, and `build/vm` capture scripts provide the portable mapping. |
| TST-03 | Validation artifacts are application-owned or explicitly desktop-capture-backed, reviewed as production output, and include process cleanup and geometry evidence. | `MainWindow` RenderTargetBitmap capture, platform VM validators, step logs, and existing production-scene artifact reviews provide the evidence workflow. |

## Reverse Upstream Gap Scan

The pinned upstream About/help windows, license service, accessibility-bearing
controls, screenshot validation scripts, and artifact tests were rescanned
against the current Avalonia implementation. Two successive scans found no
unresolved UI-03 or TST-03 behavior gaps.

## Exit Criteria

Require focused contract tests, reviewer self-test, reviewed production capture
artifacts, and fresh forward/reverse closure scans.
