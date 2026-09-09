<!--
============================================================================
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
============================================================================
-->

# CR-089: Macro-Bar Startup Parity

## Functional Inventory

| CR-01 | Render the macro bar at startup before live values arrive | product scene and startup coordinator | first-render screenshot |

## Upstream Behavior Gate

Before implementation, inspect the upstream macro-bar view, view-model,
startup sequencing, placeholder values, and screenshots line by line. Record
the source revision and map every behavior to the 2.0 implementation. Repeat
the reverse scan at closure.

## Observed Gap

The current product constructs the macro bar before quote values arrive, but
its empty arc and needle paths make the startup gauges visually disappear
against the scene. Upstream constructs the bar immediately with stable labels
and `--` placeholders while values are loading or degraded.

The source comparison covered upstream `MacroMeterViewModel.cs` lines 20-78 and
`StatusBarControl.xaml` lines 30-149. The Avalonia counterpart now preserves
the same early construction and placeholder text, and initializes a zero-fill
arc and needle so the gauge cards have a visible startup state.

## Required Closure Evidence

- First-render screenshot shows the complete macro bar before network data is
  available.
- Settled screenshot shows the same stable structure with live or explicit
  unavailable values.
- Startup and degraded states are covered by tests and circular trace evidence
  on every supported platform lane.

## Closure Evidence

The existing implementation and focused regression `MacroViewModel_StartsWithVisiblePlaceholderGauge` preserve the upstream startup contract: six macro cards are constructed before quote responses, each starts with `--` value/change text, a visible zero-fill arc, a visible needle, and stable labels/order.

The fresh Win10 physical capture `dnppv2-local-cycle-cr089-first-render-win10` passed the frozen configuration harness with `SceneWarmupSeconds=2` and `SoakDurationMinutes=0`. Its `small-viewport.png` is the first-render evidence: all six macro cards are visible with readable labels, `--` placeholders, and visible gauge geometry while the scene still reports `LOADING - waiting for data`; `step.log` records the settled viewport/fullscreen captures and `trace/trace.circular.log` records the ordered startup transitions.

Hosted matrix run `34363171543` completed all 20 supported hosted lanes. Each lane retained the required settled product screenshot and circular traces, and the aggregate closure validator passed. The matrix run's AI evidence was negative provider-quota evidence and is unrelated to this macro-bar acceptance.

The upstream forward inventory and reverse closure scan are recorded against upstream commit `65a53bbbf0cf9af1058363f8939d464ca03858f8`; the closure gate requires two successive zero-gap scans and reports the same source set used by the implementation inventory.
