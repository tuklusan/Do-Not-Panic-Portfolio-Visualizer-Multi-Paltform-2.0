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

# CR-016 Upstream Test Behavior Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

The pinned `tests/PortfolioSaver.Tests` project contains 62 artifacts. Each
artifact is a review unit and must be opened and read line by line. Every
assertion, fixture, test-only business rule, and harness side effect must map
to a current 2.0 test or become an actionable follow-up CR.

| ID | Upstream test area | Required 2.0 parity |
| --- | --- | --- |
| TP-01 | `Providers/*Tests.cs` | Quote-provider request, parsing, error, timing, and lifecycle assertions map to portable provider tests. |
| TP-02 | `Services/*Settings*Tests.cs`, `*Validation*Tests.cs` | Defaults, normalization, persistence, validation, feed verification, and configuration workflow assertions map to Avalonia/core tests. |
| TP-03 | `Services/*News*Tests.cs`, `*Startup*Tests.cs` | Multi-source news, AI fallback, startup ordering, cancellation, and degraded-state assertions map to current service tests. |
| TP-04 | `Services/*Render*Tests.cs`, `*Visualizer*Tests.cs`, `FloatingClockBuilderTests.cs` | Scene layout, motion, graph/ticker selection, clocks, and render heartbeat assertions map to the cross-platform render tests. |
| TP-05 | `Services/*YFinance*Tests.cs`, `HistoricalCacheServiceTests.cs` | Portable server/client protocol, cache, bounded work, trace, and cleanup assertions map to current YFinance tests. |
| TP-06 | `Services/*Policy*Tests.cs`, `RetryPolicyServiceTests.cs`, `ProviderHealthServiceTests.cs` | Retry, rate-limit, recovery, health, degraded, and retained-state rules map to current resilience tests. |
| TP-07 | `Services/NVIDIA NIMCodeReviewGateTests.cs`, `VmHarnessScriptTests.cs`, `DesktopWerLocalDumpsScriptTests.cs` | Review-gate and cross-platform harness behavior maps to build/test verification. |
| TP-08 | `Services/InnoInstallerScriptTests.cs`, `Services/ItchPublishWorkflowTests.cs`, `Services/VirusTotalReleaseReportScriptTests.cs` | Inno installer assertions are retired because 2.0 has no installer; any still-applicable publishing/security behavior is mapped to the current portable release workflow. |
| TP-09 | `Properties/AssemblyInfo.cs`, `EnvironmentSerialCollection.cs`, project file | Test assembly metadata, serialization, dependencies, and test isolation map to the current test project and workflow. |

## Audit Exit

Two successive fresh line-by-line scans of all 62 upstream test artifacts are
required. Missing assertions or environments must be added to the JSON tracker
as actionable CRs. Toy-application evidence cannot satisfy product test parity;
fixtures are test inputs only.
