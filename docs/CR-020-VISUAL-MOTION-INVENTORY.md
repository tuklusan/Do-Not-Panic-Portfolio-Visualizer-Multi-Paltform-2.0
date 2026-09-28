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

# CR-020 Visual Motion Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| VIS-01 | Upstream source | Behavior and 2.0 counterpart |
| --- | --- | --- |
| VIS-01 | `src/PortfolioSaver.Render/Services/TapeAnimationController.cs`; `src/PortfolioSaver.Render/Controls/TickerTapeControl.xaml`; `src/PortfolioSaver.Render/Controls/TickerTapeControl.xaml.cs` | Each tape uses a measured repeating cycle, direction, anchor offset, elapsed-time motion, frame throttling, hidden-state reset, and bounded frame steps. 2.0 maps this to Avalonia scene track offsets and timer-driven invalidation; no WPF dependency is retained. |
| VIS-02 | `src/PortfolioSaver.Render/Services/FloatingSpriteMotionController.cs`; `src/PortfolioSaver.Render/ViewModels/FloatingGraphViewModel.cs`; `src/PortfolioSaver.Render/Controls/FloatingGraphControl.xaml.cs` | Floating graph cards remain inside scene bounds, reverse velocity at configured boundaries, and flash on quote refresh. 2.0 maps this to `FloatingGraphMotionController`, `FloatingGraphViewModel`, and Avalonia property-driven card visuals. |
| VIS-03 | `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml`; `src/PortfolioSaver.Desktop/Windows/MainWindow.xaml.cs`; `src/PortfolioSaver.Presentation/Services/VisualizerSceneState.cs` | The scene respects the active desktop viewport, keeps overlays and graphs inside safe bounds, supports full-screen composition, and preserves state while the window changes size or monitor. 2.0 maps this to `ProductShellWindow`, `ProductSceneViewModel`, and responsive Avalonia layout. |
| VIS-04 | `src/PortfolioSaver.Media/Services/BackgroundImageService.cs`; `src/PortfolioSaver.Media/Services/ImageTransitionController.cs`; `src/PortfolioSaver.Presentation/Services/VisualizerSceneState.cs` | Background images are filtered from the selected folder, transitions are animated, invalid folders produce no crash, and the active scene retains background identity and attribution. 2.0 maps this to the portable media services and scene state. |

## Validation Contract

Focused tests must prove track motion, graph impulses and boundary safety,
responsive scene bounds, background filtering and transition state, and the
absence of toy or fixture-only product evidence. Physical acceptance uses the
real production screen on the four local desktops plus the six publish
runtime identifiers. Screenshots and traces remain application-owned artifacts.

## Audit Exit

Two successive fresh line-by-line scans of every cited upstream source must
report zero unmapped behaviors before CR-020 closure.

## Reverse Upstream Gap Scan

Starting from the current 2.0 files, tests, and evidence named above, two
successive scans were performed against upstream commit
`2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`. The scans asked explicitly which
upstream motion, layout, monitor, freeze-nudge, timing, cancellation, or
background behavior was missing from the current migration. The result was
zero missing behaviors and zero unresolved gaps. Any implementation change
requires this reverse scan to be repeated before the CR can proceed.
