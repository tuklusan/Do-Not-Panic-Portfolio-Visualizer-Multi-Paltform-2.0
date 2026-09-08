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

# CR-107: Identify Runtime Architecture In The Product Footer

**Status:** Open
**Phase:** Phase 7
**Priority:** Low
**Depends on:** CR-106 only where footer layout changes overlap

## Objective

Extend the correctly migrated bottom-right `2.0` version indicator with a short,
stable runtime platform/architecture identifier so screenshots and physical
test evidence can be identified without knowing the runner or file name.

The preferred format is `2.0 | <os>-<arch>`, for example `2.0 | win-arm64`,
`2.0 | linux-x64`, or `2.0 | osx-arm64`. The final token mapping must be
centralized, deterministic, and based on the actual runtime OS and architecture,
not the CI runner label.

## Functional Inventory

| UP-01 | `src/PortfolioSaver.Presentation/Controls/VisualizerSceneControl.xaml` and `.xaml.cs` at upstream commit `65a53bbbf0cf9af1058363f8939d464ca03858f8` | The upstream product renders a bottom-right version watermark from `PortfolioVersion.Version`, and assigns stable accessibility metadata to it. | `ProductShellWindow.axaml` renders the v2 version indicator; this CR extends its visible text with a deterministic architecture token while preserving the version prefix. |
| UP-02 | `src/PortfolioSaver.Shared/PortfolioVersion.cs` at the same upstream commit | The upstream version value is centralized in `PortfolioVersion.Version`. | `DoNotPanicPortfolioVisualizer.Shared.PortfolioVersion.Version` remains the source of the product version. The new token is separate from version constants. |
| UP-03 | Complete upstream source scan at the same commit | No upstream architecture-token behavior exists; the watermark contains only `1.0`. | The `2.0 | <os>-<arch>` suffix is a migration-specific evidence and supportability extension, not a lost upstream behavior. It is centralized in `RuntimeArchitecture`. |

The forward inventory is complete. A reverse scan of the upstream watermark
sources and the migrated product-shell binding found no unmapped upstream
behavior. The only deliberate difference is the v2 architecture suffix in UP-03.

## Acceptance criteria

- The bottom-right footer retains the version and adds the normalized runtime
  architecture without clipping at supported window sizes or DPI scales.
- All supported targets produce the expected stable tokens for Windows x64 and
  arm64, Linux x64 and arm64, and macOS x64 and arm64.
- Local and hosted screenshot manifests record the same architecture token as
  the visible footer; runner names remain separate metadata.
- Unit tests cover every supported OS/architecture mapping and unknown-runtime
  fallback behavior.
- The upstream forward/reverse behavior gates, mandatory reviewer gate, full
  build/test, serialized 20-lane acceptance, and evidence inspection pass.
