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

# CR-040 Configuration and Scene-State Parity Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| CFG-01 | Upstream `PortfolioSaver.Settings/Windows/MainWindow.xaml.cs` and `MainWindowViewModel` validation lifecycle | Avalonia `MainWindow.axaml.cs` and `MainViewModel.ValidateAsync`: validation is serialized, cancellable, visibly busy, and cannot save an unvalidated candidate. Tests: `SettingsPersistenceAndValidationTests`, `ConfigurationWindowContractTests`. |
| CFG-02 | Upstream close-request and close-during-validation paths | Avalonia `MainViewModel.Cancel`, `CancelValidation`, `RequestClose`, and `ProductShellWindow` configuration ownership preserve cancel-before-close and detach/dispose behavior. Tests: `StagedSceneStartupCoordinatorTests`, configuration contract tests. |
| CFG-03 | Upstream settings edit, revert, save, and reopen behavior | Avalonia `BuildCandidateSettings`, `Revert`, `Save`, `ApplyLoadedSettings`, and `SettingsFileService` preserve persisted state and discard uncommitted edits. Tests: `SettingsPersistenceAndValidationTests`. |
| CFG-04 | Upstream tab warm-up and first-frame layout preparation | Avalonia startup shield, bounded window sizing, centered placement, and scrollable content keep the configuration surface visible on small screens. Tests: `ConfigurationWindowContractTests`; physical acceptance remains required. |
| CFG-05 | Upstream scene-state handoff after successful configuration | Avalonia `ProductShellWindow` receives the saved settings and starts the real `ProductSceneViewModel`; no toy surface is used as acceptance evidence. Tests: `StagedSceneStartupCoordinatorTests` and scene runtime contracts. |
| CFG-06 | Upstream offline/configuration gating and retry path | Avalonia `IsNetworkAvailable`, `ShowNetworkLockOverlay`, and `RetryNetworkCommand` prevent invalid network-dependent validation while allowing recovery without stale state. Tests: configuration validation and connectivity contract coverage. |
| CFG-07 | Upstream visual readability and automation surface | Avalonia configuration labels, selected-tab contrast, action controls, status text, and automation IDs are present in `MainWindow.axaml`. Test: `ConfigurationWindowContractTests`; physical screenshot review required. |

## Reverse scan

The current Avalonia app, its configuration tests, startup coordinator, scene
window, and settings services were rescanned against the upstream files above.
The reverse question was applied explicitly: **IDENTIFY UPSTREAM LOGIC MISSING
FROM THE CURRENT MIGRATION**. Two successive scans found zero missing behaviors;
the only platform adaptation is WPF window/event plumbing replaced by Avalonia
window lifecycle APIs. Unresolved gaps: none.

## Closure evidence contract

Closure requires the pre-development and closure behavior gates, license and
reviewer gates, the complete Release test suite, and physical configuration
screen acceptance on the available local platforms with artifacts reviewed and
temporary processes removed.
