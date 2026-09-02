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

# CR-044 Market-Critter Motion Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| CRT-01 | Upstream declares market critter presentation disabled in the production scene | Avalonia scene matches `EnableMarketCritters = false`; the production acceptance surface does not show critters. |
| CRT-02 | Upstream retains the dormant sprite implementation for future use | Avalonia retains sprite models, bounds, initialization, chase/drift motion, and safe clamping behind the same production gate. |
| CRT-03 | Upstream protects the dormant implementation from affecting active scene timing | Avalonia only seeds and steps sprites when enabled; active ticker, graph, and overlay lanes are unaffected. |
| CRT-04 | Upstream includes sprite logic in source/test parity scope despite disabled production visibility | The dormant implementation is included in the reverse scan and remains available for a future explicitly authorized behavior change. |

## Reverse scan

Upstream `VisualizerSceneControl.xaml.cs`, its XAML sprite host and view model,
related motion helpers, and tests were rescanned against the current Avalonia
scene and render model. The reverse question was applied explicitly:
**IDENTIFY UPSTREAM LOGIC MISSING FROM THE CURRENT MIGRATION**. Two successive
scans found zero missing behaviors. Enabling critters would be a behavior change,
not a migration fix, and is therefore not performed by this CR.

## Closure evidence contract

Closure requires focused motion/source-contract tests, the full Release suite,
all migration/license/reviewer/pre-push gates, and confirmation that no dormant
sprite code affects the production scene while the upstream gate remains false.
