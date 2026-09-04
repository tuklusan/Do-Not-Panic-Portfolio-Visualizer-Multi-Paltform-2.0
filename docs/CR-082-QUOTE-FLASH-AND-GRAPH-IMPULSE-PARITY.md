<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-082: Quote Flash And Graph Impulse Parity

## Findings

The upstream production scene applies a value flash for every fresh usable
quote, including a quote whose displayed value is unchanged. The flash color is
green for a positive changed value, red for a negative changed value, and blue
for an unchanged value. Stale quotes do not create this fresh-update cue.

Upstream graph cards use a different rule: a raw last-price change triggers the
card itself to flash repeatedly while it makes rapid travel toward the ceiling
for an increase or floor for a decrease, then restores the normal card
appearance and swimming velocity. An unchanged raw value,
initial hydration, percent-only change, stale data, and structural replacement
must not create the directed impulse. A bounded timeout and boundary completion
must restore normal motion.

## Required Work

Compare the upstream implementations line by line with the current Avalonia
view models, scene coordinator, graph-motion controller, and visual controls.
Restore any missing behavior, with the ticker blue unchanged-value flash as an
explicit acceptance case. Keep the real production scene as the demonstrated
surface; fixtures may only provide deterministic test inputs.

## Acceptance

- Fresh positive, negative, unchanged, stale, and initial-hydration quote cases
  are covered by focused tests and circular trace assertions.
- Graph increases flash visibly while travelling rapidly to the top boundary,
  decreases flash visibly while travelling rapidly to the bottom boundary, and
  each card then returns to its base appearance and prior swimming velocity;
  no-op cases remain ordinary.
- Settled production screenshots show the flash and directed graph motion on an
  available local machine, with hosted smoke coverage where applicable.
- Upstream forward/reverse inventory, NVIDIA review, build/test, license,
  syntax, artifact review, cleanup, and commit/push gates pass.

## Status

The ticker update rule is implemented: post-hydration fresh usable quotes flash
green/red when their value changes and blue when the displayed value is
unchanged; initial hydration and stale refreshes remain quiet. Focused tests
cover these cases. Full production-scene visual and circular-trace evidence
for the flash and graph impulse remains required before closure.
