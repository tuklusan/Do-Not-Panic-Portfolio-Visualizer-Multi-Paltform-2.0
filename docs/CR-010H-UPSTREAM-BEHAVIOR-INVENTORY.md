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

# CR-010H Upstream Menu Surface Inventory

## Functional Inventory

This inventory was independently scanned against upstream commit
`2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5` on 2026-08-29. It addresses the
real product shell menu that was visually unreadable during physical Windows
11 acceptance, not a test fixture.

| ID | Upstream source and behavior | DNPPV-2.0 implementation and validation |
| --- | --- | --- |
| MS-01 | `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml` places a visible `MainMenu` above the production `VisualizerSceneControl` in normal and maximized window modes. | Make the Avalonia root menu foreground explicit at both menu and menu-item scope so a system theme cannot render the dark scene-shell menu as dark text on a dark surface. Verify in a real normal/maximized product capture. |
| MS-02 | The upstream menu exposes File, View, Options, and Help roots, with Exit, Full Screen, Settings, and About command paths. | Preserve the exact root/action structure and existing click handlers. Validate that every root and child label remains readable in default, hovered, and submenu-open states. |
| MS-03 | `MainWindow.xaml.cs` wires Full Screen to F11 and menu activation, and keeps the menu visible in normal/maximized state. | Retain the existing Avalonia F11/menu activation behavior while styling only the menu surface. Verify no event handler or input-gesture change. |
| MS-04 | `MainWindow.xaml.cs` collapses `MainMenu` on fullscreen enter and restores it on fullscreen exit while preserving the scene. | Keep the existing fullscreen visibility transition unchanged. Verify the menu returns with readable contrast after leaving fullscreen. |
| MS-05 | The upstream shell is usable without relying on an operating-system color preference to reveal navigation. | Use a product-owned dark shell palette with a high-contrast normal foreground, a distinguishable hover/open surface, and legible submenu text across the supported Avalonia desktop targets. Physical acceptance is the authority for platform rendering. |

### Gap Found And CR Boundary

The current Avalonia shell sets `Foreground` on `Menu`, but the active Fluent
menu-item template does not inherit that value for the top-level items under
the observed Windows 11 theme. The real product screenshot therefore shows
dark File, View, Options, and Help labels on the dark menu band. CR-010H adds
explicit scoped menu-item state styling only. It does not alter menu commands,
fullscreen transitions, settings ownership, About content, scene geometry, or
runtime scheduling.

## Current Execution Evidence

The 2026-08-30 Lubuntu physical run from pushed commit `43e45c1` passed with
the real self-contained product, after a 45-second settling interval. Its
menu-open capture visibly shows the File, View, Options, and Help roots plus
the File submenu in the product-owned dark palette. The same run recorded a
full-screen transition and later motion capture, and the final remote process
audit found no DNPPV application or managed YFinance sidecar process.

This was initially evidence for the Linux local-machine lane only. CR-010H
The 2026-09-01 rerun from pushed commit `34904be` additionally exercised F11
exit from full screen, restored the product window, activated its File control,
and visibly captured the File/Exit dropdown in
`build/vm-artifacts/cr010h/linux-fullscreen-exit-click/fullscreen-exit-menu.png`.

Windows 10 and Windows 11 subsequently passed that same exit-and-restoration
interaction from pushed commit `2786d7e`; their ignored artifacts are in
`build/vm-artifacts/cr010h/win10-fullscreen-exit` and
`build/vm-artifacts/cr010h/win11-fullscreen-exit` respectively. Each run
captured the restored File/Exit menu and produced a moving cinematic trace.

The 2026-08-30 Windows 11 physical run from the same pushed product source
also passed. The driver recorded its logical `1024x768` windowed startup,
opened the File submenu through UI Automation, captured normal, wide,
fullscreen, and later-motion frames, and removed its one-shot scheduled task.
The final remote process query found no product or managed YFinance sidecar.

Product diagnostics for these runs are retained only in the bounded
`trace/trace.circular.log` artifact. `step.log` remains harness control
evidence; no product diagnostic is written to a per-run text log.

Windows 10 remains the sole unaccepted local-machine menu lane. Fullscreen
exit/menu-restoration remains a dedicated interaction check for the eventual
final cross-machine closure pass.
