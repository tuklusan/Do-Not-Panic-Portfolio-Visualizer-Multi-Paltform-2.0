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

## Upstream Behavior Gate

Before implementation, inspect the upstream macro-bar view, view-model,
startup sequencing, placeholder values, and screenshots line by line. Record
the source revision and map every behavior to the 2.0 implementation. Repeat
the reverse scan at closure.

## Observed Gap

The current product hides the macro bar until quote values arrive. Upstream
shows the bar immediately, preserving its labels and layout while values are
loading or degraded.

## Required Closure Evidence

- First-render screenshot shows the complete macro bar before network data is
  available.
- Settled screenshot shows the same stable structure with live or explicit
  unavailable values.
- Startup and degraded states are covered by tests and circular trace evidence
  on every supported platform lane.
