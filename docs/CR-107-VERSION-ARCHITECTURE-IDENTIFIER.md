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
  build/test, serialized 21-lane acceptance, and evidence inspection pass.
