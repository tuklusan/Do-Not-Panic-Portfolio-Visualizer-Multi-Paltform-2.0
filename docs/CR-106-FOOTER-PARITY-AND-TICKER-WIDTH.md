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

# CR-106: Footer Parity And Ticker Width Fidelity

**Status:** Open
**Phase:** Phase 7
**Priority:** Normal
**Evidence:** [21 historical product screenshots](attachments/CR-106/)

## Objective

Restore two visual details that must be migrated from the upstream product:

1. Make the footer disclaimer exactly match upstream: `Delayed by minimum 15 minutes.`
2. Make each live ticker lane only as wide as its label plus the measured live
   ticker content, with no more than 0 to 4 pixels of intentional trailing
   allowance. The lane must not expand to the full scene width merely because
   the containing grid has spare space.

The supplied screenshots are attached as visual evidence from historical run
`33988763870`. They are diagnostic baselines, not v2 closure evidence; that
run predates the v2 closure-receipt contract and must remain rejected by the
deterministic hosted-soak validator.

## Upstream inventory

The upstream reference commit is `65a53bbbf0cf9af1058363f8939d464ca03858f8`.
The relevant source was read from disk:

- `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml`
  contains the exact text `Delayed by minimum 15 minutes.`.
- `src/PortfolioSaver.Render/Controls/TickerTapeControl.xaml` defines the
  centered 28-pixel ticker viewport, the label badge, fixed-width Consolas
  fields, and clipped motion host.
- `src/PortfolioSaver.Render/Controls/TickerTapeControl.xaml.cs` and the
  related view model define the measured/fixed item geometry and continuous
  track behavior.

The current v2 footer is in
`src/DoNotPanicPortfolioVisualizer.App/Views/ProductShellWindow.axaml` and
currently says `Delayed market data may apply.`. The current lane template
uses a full-width quote host and a 230-pixel fixed item width; implementation
must reconcile that with the upstream measurement contract rather than blindly
removing fixed geometry needed for seamless motion. The outer lane now uses
Avalonia `Auto,Auto` layout for the label and bounded viewport, while the
duplicated internal track retains its fixed motion geometry.

## Functional Inventory

| ID | Functional behavior or proof obligation |
| --- | --- |
| FOOT-01 | Exact upstream disclaimer text, punctuation, and casing. |
| TICKER-01 | Four configured lanes remain present and independently moving. |
| TICKER-02 | The lane background is sized to the label plus measured content, with at most 4 pixels of trailing allowance. |
| TICKER-03 | Label and ticker viewport remain vertically centered and ticker content remains clipped without overlap or truncation caused by the narrower host. |
| TICKER-04 | Fixed-width fields, duplicated tracks, direction, speed, waiting glyphs, trend colors, and quote-only flash behavior remain intact. |
| TICKER-05 | Empty/unmeasured lanes remain stopped and measured lanes restart correctly when data or viewport geometry changes. |
| EVID-01 | Supplied screenshots are retained with the CR under the unique lane filenames in `docs/attachments/CR-106`. |

## Acceptance criteria

- The rendered footer contains exactly `Delayed by minimum 15 minutes.`.
- A narrow and wide display both show four complete lanes with no clipping,
  overlap, unexpected full-width stretch, or loss of continuous motion.
- Lane width is derived from the label and measured content, with a documented
  0-4 pixel trailing allowance; any retained fixed item width is justified by
  the upstream motion/measurement behavior.
- The lane title remains vertically centered and quote values remain readable.
- Focused unit tests cover width calculation, resize/re-measure, empty-data,
  clipping, and motion continuity behavior.
- The upstream forward inventory and reverse missing-behavior scan both pass,
  followed by the mandatory review gate, Release build/test, serialized hosted
  matrix, screenshot/trace inspection, and closure evidence review.

## Validation plan

1. Read the upstream footer and ticker implementation line-by-line again at
   implementation start and record any newly found behavior in this CR.
2. Add focused model/view tests before changing the layout.
3. Validate at compact, normal, wide, and high-DPI working areas.
4. Run the full real-product acceptance workflow and inspect every retained
   screenshot and both circular traces per lane.
