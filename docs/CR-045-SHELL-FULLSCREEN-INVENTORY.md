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

# CR-045 Shell and Fullscreen Interaction Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| SHL-01 | Upstream saves normal/maximized state, position, size, and decorations before fullscreen | Avalonia `ProductShellWindow` preserves the pre-fullscreen state and restores it on exit. |
| SHL-02 | Upstream enters fullscreen on the active monitor and repairs bounds after delayed native transitions | Avalonia uses the active `Screens.ScreenFromWindow`, applies monitor bounds, and records the applied geometry in circular trace. |
| SHL-03 | Upstream hides shell chrome in fullscreen and restores it after exit | Avalonia hides `MainMenu` and decorations during fullscreen, then restores them with the prior state. |
| SHL-04 | Upstream supports F11/menu entry, Escape exit, and menu-safe double-click behavior | Avalonia handles F11, Escape, menu commands, and excludes menu/button/text targets from the fullscreen double-click gesture. |
| SHL-05 | Upstream applies composition-surface nudges and avoids unsafe repeated transitions | Avalonia scene/layout recovery and fullscreen bounds application are bounded and traceable; no unbounded timer or process remains after close. |
| SHL-06 | Upstream validates shell geometry and menu readability across viewport sizes | Existing startup options, shell menu contrast, geometry, and physical validation contracts cover small, wide, fullscreen, and menu states. |

## Reverse scan

Upstream desktop shell source, XAML, screen helpers, fullscreen tests, and
composition recovery paths were rescanned against the current Avalonia shell,
startup options, render recovery, and test contracts. The reverse question was
applied explicitly: **IDENTIFY UPSTREAM LOGIC MISSING FROM THE CURRENT
MIGRATION**. Two successive scans found zero missing behaviors. WPF event and
native API details are replaced with Avalonia equivalents while preserving the
user-visible interaction contract.

## Closure evidence contract

Closure requires focused shell tests, the full Release suite, migration/license/
reviewer/pre-push gates, and physical screenshot/geometry artifact review.
