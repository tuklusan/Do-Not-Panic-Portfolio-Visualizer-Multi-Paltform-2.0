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

# CR-019 Configuration UI Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| UI-01 | Upstream source | Behavior to preserve in 2.0 |
| --- | --- | --- |
| UI-01 | `src/PortfolioSaver.Settings/Windows/MainWindow.xaml`; `src/PortfolioSaver.Settings/ViewModels/MainWindowViewModel.cs` | Provide General and Advanced settings, stable minimum window dimensions, scrollable content, responsive layout, and user-editable portfolio, background, ticker, market, news, and AI settings. |
| UI-02 | `src/PortfolioSaver.Settings/ViewModels/MainWindowViewModel.cs`; `src/PortfolioSaver.Settings/Windows/MainWindow.xaml.cs` | Keep connectivity gating, Validate-before-Save, validation progress/cancellation, success-state Save/Cancel, rejected-edit invalidation, and close behavior. |
| UI-03 | `src/PortfolioSaver.Settings/Services/NewsFeedValidationService.cs`; `src/PortfolioSaver.Settings/Services/YahooSymbolValidationService.cs` | Validate configured feeds and symbols before accepting settings, preserve actionable status, and retain validated symbol metadata. |
| UI-04 | `src/PortfolioSaver.Settings/Services/SettingsFileService.cs`; `src/PortfolioSaver.Data/Services/SettingsProtectionService.cs` | Save only validated settings, preserve non-secret settings across reload, protect provider secrets, and discard edits on Cancel. |
| UI-05 | `src/PortfolioSaver.Settings/Windows/MainWindow.xaml` | Preserve keyboard/focus access, readable contrast, tab navigation, and non-clipped content at supported small and large viewports. |

## Validation Contract

The physical workflow verifies the real Avalonia configuration window at small
and large viewports on all four local desktops: Windows 10, Windows 11,
Lubuntu/LXQt, and Intel macOS Big Sur. The Mac run is confined below
`~/SOFTWARE_DEV/DNPPV_20/` and must remain within its 1 GB project limit.
Deterministic tests cover validation, cancellation, save/reload, and
cancel/discard paths. No WPF or installer behavior is in scope.

## Audit Exit

Two successive fresh line-by-line scans of the cited upstream configuration
sources must report zero unmapped behaviors before closure.

## Current Acceptance State

The deterministic suite and physical configuration runs on Lubuntu, Windows 10,
and Windows 11 pass on the current build. The Windows 10 storage contract also
passes before deployment and during execution. On the Intel Mac, CoreGraphics
confirms the real Avalonia configuration window at `1280x848` and the workspace
stays below `1 GiB`, but the SSH capture path does not yet yield a reviewable
window artifact: full-desktop capture omits the window layer and window-ID
capture lacks permission. CR-019 remains open until that Mac visual artifact is
reviewed; this is not waived by the compositor or operator observation.
