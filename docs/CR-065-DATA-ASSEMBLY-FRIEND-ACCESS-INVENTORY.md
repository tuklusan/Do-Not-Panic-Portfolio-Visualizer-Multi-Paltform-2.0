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

# CR-065 Data Assembly Friend-Access Inventory

## Scope

The forensic code review found that
`src/DoNotPanicPortfolioVisualizer.Data/Properties/AssemblyInfo.cs` still names
the retired `PortfolioSaver.Presentation` and `PortfolioSaver.Tests` assemblies
in `InternalsVisibleTo` declarations. The active assemblies are
`DoNotPanicPortfolioVisualizer.Presentation` and
`DoNotPanicPortfolioVisualizer.Tests`.

## Functional Inventory

| LOG-01 | The data assembly exposes its intended internal implementation surface to the active 2.0 presentation assembly. |
| LOG-02 | The data assembly exposes its test-only internal surface to the active 2.0 test assembly. |
| LOG-03 | Retired upstream assembly identities are absent from active friend-access metadata. |
| TEST-01 | The regression test verifies all three identity rules from the built repository. |

## Required disposition

Replace the retired friend identities with the active 2.0 identities, then
compile the data assembly and run the complete Release test suite. Confirm that
all intended internal consumers remain covered and that no retired friend
assembly declaration remains in active source.

## Status

Closed on 2026-09-02. The active 2.0 friend identities are implemented and
covered by the regression test and full Release suite.
