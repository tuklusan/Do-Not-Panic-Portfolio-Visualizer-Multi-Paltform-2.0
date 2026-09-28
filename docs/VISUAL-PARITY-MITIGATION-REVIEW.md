<!--
  ============================================================================
  Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
  Proprietary rights reserved except as expressly licensed herein.

  DO NOT PANIC PORTFOLIO VISUALIZER
  This file is governed by the SANYALnet Labs Non-Commercial License in the
  root LICENSE file.
  Attribution is required: "Based on original work by Supratim Sanyal of
  SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
  patent, trademark, and governing-law provisions.
  ============================================================================
-->

# Visual-parity mitigation review

This record documents the ticker-lane regression reproduction, the repair, and
the current runtime comparison. It does not authorize release publication,
signing, or announcement.

## Source contract

The comparison uses the pinned upstream behavior recorded in
`docs/UPSTREAM_CINEMATIC_DISPLAY_CONTRACT.md` and
`docs/CR-106-FOOTER-PARITY-AND-TICKER-WIDTH.md`:

- four independently moving lanes;
- a centered 28-pixel clipped ticker viewport;
- fixed-width fields inside a duplicated continuous track;
- a lane background sized to its label plus measured content, rather than the
  spare width of the scene;
- distinct waiting/missing rendering; and
- the exact footer wording `Delayed by minimum 15 minutes.`.

The implementation comparison is against the upstream source inventory named
by those documents, including `TickerTapeControl.xaml(.cs)` and
`VisualizerSceneControl.xaml`, rather than against screenshot appearance alone.

## Exit-criteria evidence matrix

| Requirement | Current evidence | Disposition |
| --- | --- | --- |
| Reproduce the jagged/incomplete lanes from a clean build | Isolated pre-mitigation `e48e5ad7` Linux publish and `pre-mitigation-linux-old-scene.png` | Proven |
| Compare against pinned upstream behavior | `UPSTREAM_CINEMATIC_DISPLAY_CONTRACT.md`, `CR-106-FOOTER-PARITY-AND-TICKER-WIDTH.md`, and the source inventory | Proven for the reviewed ticker/scene scope |
| Correct geometry, density, waiting/missing states, and startup sizing | `TickerLaneViewModel`, `ProductShellWindow.axaml(.cs)`, 24 focused tests, and post-fix Win10/Linux captures | Proven for the repaired scope |
| Fresh Windows and Linux screenshots/traces | `post-fix-windows10-flashscope` and `post-fix-linux-flashscope`, both passed and cleaned up remotely | Proven |
| Re-audit closed visual/parity CRs | A broad user-visible/parity scan covered 34 closed records; 33 have complete upstream closure audits and zero unresolved gaps. The sole exception, CR-119, is arm64 test synchronization rather than visual/product behavior and is excluded. No closed visual/parity record is contradicted by current evidence. | Proven; no closed visual CR contradicted |
| Release authorization | CR-010C and CR-010F remain open; Mac/provider evidence and full cinematic closure are outstanding | Explicitly blocked |

## Clean-build reproduction

The isolated pre-mitigation revision `e48e5ad7` was published self-contained
for `linux-x64` and launched on the Linux lab machine. Its source and render
evidence show the regression directly:

- `ProductShellWindow.axaml` used `ColumnDefinitions="Auto,*"` for the lane
  host, so the lane background stretched across the available scene width;
- it had no measured `LaneWidth` or `ContentViewportWidth` binding; and
- the footer still read `Delayed market data may apply.`.

The remote capture is retained at
`docs/attachments/CR-010F/pre-mitigation-linux-old-scene.png`. It shows the
four full-width lanes and obsolete footer. No application was launched on the
development machine. The old validation wrapper initially failed before
capture because its historical X11 socket selection chose the wrong socket;
the same old product was then captured remotely with the verified `DISPLAY=:0`
and Xauthority environment.

## Repaired state

The current repaired revision binds the outer lane to `LaneWidth`, binds the
clipped viewport to `ContentViewportWidth`, keeps the translated track left
aligned at `TrackWidth`, and derives the viewport from at most four fixed-width
items. Empty lanes stop and clear their track; populated lanes retain repeated
copies and their direction/speed motion anchor. Waiting and missing states are
covered separately by `TickerPresentationTests`.

Fresh remote evidence:

- Windows 10: `docs/attachments/CR-010F/repaired-windows10-scene.png`;
- Linux: `docs/attachments/CR-010F/repaired-linux-scene.png`;
- Linux result: `H:\dnppv2-local-cycle-linux-parity-h-fixed2\linux-x64-lxqt\linux-x64-lxqt-machine-result.json`;
- Linux trace: `H:\dnppv2-local-cycle-linux-parity-h-fixed2\linux-x64-lxqt\trace\trace.circular.log`.

Both repaired captures show four bounded continuous lanes, readable values or
waiting/missing glyphs, the upstream footer, and the full scene. The Linux
trace records changing `MARKETS` offsets with `SEQUENCE_WIDTH=1246.00` and
`COPIES=2` during the remote ten-minute soak.

Post-fix Linux evidence was refreshed after narrowing the ticker flash overlay
to the displayed last-value column. The one-minute remote cycle
`dnppv2-local-cycle-linux-flashscope-framework2` passed with product-scene,
fullscreen, menu, motion, product/YFinance circular traces, and remote-root
cleanup proof. The retained screenshot and manifest are under
`docs/attachments/CR-010F/post-fix-linux-flashscope/`. Two earlier refresh
attempts are explicitly invalid: one exceeded the remote temporary-disk quota
during self-contained extraction, and one reached teardown but failed copying
artifacts to the canonical path because the harness does not quote spaces in
the SCP destination.

Post-fix Windows 10 evidence was then refreshed from the same corrected
self-contained source. Cycle `dnppv2-local-cycle-win10-flashscope-selfcontained`
passed the product-scene, fullscreen, menu, motion, circular-trace, and
cleanup gates; its retained screenshot and manifest are under
`docs/attachments/CR-010F/post-fix-windows10-flashscope/`. The intermediate
framework-dependent attempt is invalid because the remote process exited before
opening a window with runtime error `-532462766`.

The early portion of the current Linux trace records `GRAPH_COUNT=0` while the
scene is still hydrating. Later settled samples transition to and remain at
`GRAPH_COUNT=16`. A separate settled remote Linux capture records 1,478
`GRAPH_COUNT=16` samples and visibly contains the floating graph cards. This
resolves the apparent startup contradiction, but does not by itself close the
full graph-impulse acceptance gate: that particular settled run recorded no
`GRAPH_IMPULSE` events because no qualifying raw-price change arrived.

## Verification

- Clean Release build and full solution test: 346 passed, 0 failed, 0 skipped.
- Focused ticker/layout tests: 24 passed, 0 failed, including the upstream
  188-pixel lead-in and compact-viewport clamp contract plus hydration,
  directional, stale-quote flash semantics, and last-value-only flash scope.
- Windows 10 remote cycle: passed, including startup sizing, motion, and
  cleanup evidence.
- Linux remote cycle: passed, including product-scene capture, fullscreen and
  motion captures, RSS/AI evidence, ten-minute soak, and cleanup.
- Current development-machine product-process check: no product or YFinance
  process was running; all product execution was remote.

## CR re-audit disposition

- `CR-009` is now closed: fresh Linux and Windows traces prove the ordered
  MacroQuotes → WorldMarketQuotes → PortfolioQuotes startup sequence.
- `CR-010C` and `CR-010F` remain open because this mitigation does not prove
  the complete upstream cinematic contract or all-machine closure.
- `CR-010B`, `CR-010D`, and `CR-020` remain closed after re-audit: the settled
  remote Linux capture proves the floating graph population and the repaired
  scene composition, while the earlier `GRAPH_COUNT=0` samples are startup
  hydration rather than settled behavior.
- `CR-082` remains closed on the strength of its prior qualifying live impulse
  receipt; the new settled run is neutral because it had no qualifying raw
  price change and therefore emitted no impulse event.
- `CR-081` and `CR-106` are supported by the new Windows/Linux captures for
  contrast, bounded width, footer wording, clipping, and continuous motion.
- No closed CR is reopened solely from this lane review, but older closure
  evidence must not be substituted for the current two-machine evidence above.

Release publication, signing, announcement, and final review certification
remain blocked pending the remaining cross-platform and upstream-parity gates.
