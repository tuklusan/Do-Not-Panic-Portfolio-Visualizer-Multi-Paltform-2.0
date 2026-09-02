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

# CR-012 Upstream Behavior Inventory

## Scope

CR-012 proves that the migrated Avalonia/.NET 10 product can be packaged and
validated across the complete supported runtime matrix. The product remains a
desktop application; publish artifacts are self-contained bundles, not an
installer and not a substitute for physical product acceptance.

Upstream baseline: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream source and behavior | DNPPV-2.0 mapping and required evidence |
| --- | --- | --- |
| PB-01 | The upstream `README.md` and upstream `docs/RELEASE_1_0_BASELINE.md` describe a desktop product that must run as a packaged release on its supported operating systems. | Produce self-contained Avalonia/.NET 10 bundles for every supported RID and verify the expected executable/manifest is present. |
| PB-02 | `src/PortfolioSaver.Desktop/PortfolioSaver.Desktop.csproj` and the upstream release layout define the desktop runtime boundary. | Keep the active solution Avalonia-only, publish the app host and owned YFinance sidecar together, and do not introduce WPF or an installer. |
| PB-03 | The upstream manual UI suite requires the real visualizer scene, window controls, fullscreen behavior, data lanes, and cleanup to be exercised on physical machines. | Run the maintained real-product acceptance harness on Lubuntu/LXQt, Windows 10, Windows 11, and Intel macOS Big Sur, with screenshots, traces, and process-cleanup evidence. |
| PB-04 | `src/PortfolioSaver.Presentation/Services/StartupCoordinator.cs` owns startup/shutdown of runtime dependencies and the product scene. | Verify the physical runs use the pushed product bundle, settle before capture, and leave no product or sidecar process behind. |
| PB-05 | The migrated release must cover the complete target architecture set rather than silently validating only the local x64 machines. | Hosted publish jobs must pass for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`, with one uploaded artifact per RID. |

## Exit Gate

Before product or workflow changes, run:

```powershell
./build/Test-MigrationBehaviorGate.ps1 -CrId CR-012 -Stage PreDevelopment
```

Closure requires two successive fresh upstream scans, mandatory CODE and
TEST_ARTIFACT review, six-target publish success, physical acceptance on all
four local machines, artifact inspection, and explicit process cleanup.
