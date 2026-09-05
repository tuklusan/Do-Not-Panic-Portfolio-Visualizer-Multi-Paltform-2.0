<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Based on original work by Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-081: Ticker Quote-Lane Contrast

## Functional Inventory

| CR-01 | Preserve readable, distinct ticker lanes over cinematic backgrounds | product shell and ticker styles | settled screenshots |

## Upstream Line-Level Mapping

The complete upstream ticker artifacts were read from disk: `TickerTapeControl.xaml`
and `TickerTapeControl.xaml.cs`, together with the scene composition and shared
render resources. The upstream lane is an opaque near-black `#EE050505` border
with a separate grey-blue name lane (`#FF121922`); its viewport is clipped,
28 pixels high, vertically centered, and the track is rebuilt from fixed-width
items before a repeating animation starts. The code-behind subscribes to load,
resize, data-context, collection, and update-sequence events; it stops motion
when unloaded or empty, defers measurement until a positive viewport exists,
and flashes only registered value/change borders.

The corresponding Avalonia implementation is `ProductShellWindow.axaml` and
`ProductSceneViewModel.cs`: the lane uses the darker neutral translucent
`#C52A3138` background, preserves the upstream grey name lane, clips a fixed
28-pixel viewport, centers all text vertically, uses fixed ticker columns, and
binds quote-only flash state to the value/change region. Motion, update
subscription, startup hydration, and empty-state behavior are covered by the
existing ticker presentation tests and the real-product soak evidence.

Reverse scan result: no additional upstream ticker contrast, alignment, motion,
startup, or flash behavior was found unmapped. The remaining work is focused
visual acceptance on a settled production scene, not a missing implementation.

## Purpose

Improve the contrast of the four moving quote lanes in the production scene.
The current quote lanes are black, which can merge with dark upstream-style
cinematic backgrounds. The ticker-name lanes already use a grey treatment that
should remain the visual anchor for the four lanes.

## Mandatory Upstream Gate

Before implementation, inspect the upstream ticker view, scene layout, styles,
and visual-acceptance harness line by line. Record the relevant source paths,
colors, opacity behavior, dimensions, alignment rules, motion behavior, and
startup settling behavior in this CR and in the tracker. Then run the reverse
scan: identify any upstream ticker behavior absent from the current Avalonia
implementation and route every gap to a CR before coding.

Reference acceptance contract: `docs/UPSTREAM_ACCEPTANCE_BASELINE.md`, section
4.4 and the related configuration and runtime sections.

## Required Change

Use a darker neutral grey for the quote lanes, with restrained transparency only
if the resulting screenshots demonstrate readable values over representative
dark and light background content. Preserve the existing name-lane grey, ticker
order, vertical centering, quote alignment, lane width, motion, and startup
settling behavior. Do not replace the real product scene with a visual fixture.

## Validation

- Focused layout/style tests cover the lane dimensions, alignment, and contrast.
- Settled real-product screenshots cover startup, motion, and a dark background.
- Linux, Windows, and macOS local evidence is collected when available; hosted
  smoke evidence covers the supported publish matrix.
- Circular traces, manifests, and screenshots are reviewed, then disposable
  artifacts and remote processes are removed.
- The upstream forward/reverse behavior gate, license and PowerShell syntax
  gates, build/test gate, NVIDIA review, artifact review, and commit/push gate
  all pass before closure.

## Status

Open. The upstream forward and reverse inventory is now recorded above and the
pre-development gate is ready to rerun. Current Avalonia styling already
contains the requested darker neutral quote-lane treatment; fresh production
screenshots and closure-gate evidence remain required before closing this CR.
