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

# CR-026 Lifecycle Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| LIF-01 | Upstream behavior | 2.0 mapping |
| --- | --- | --- |
| LIF-01 | The owned provider process is launched only when needed, duplicate ownership/port use is rejected, startup is bounded, and cancellation or failed startup terminates and disposes the owned process. | `src/DoNotPanicPortfolioVisualizer.Shared/Services/YFinanceServerProcessManager.cs`, `src/YFinance/YFinance.NET.Server/Hosting/YFinanceServerProgram.cs`, and `YFinanceInfrastructureTests` cover owned launch, duplicate prevention, probe, cancellation, kill, wait, and disposal. |
| LIF-02 | Shutdown is idempotent, queued shutdown reaches the owned manager, clean and abnormal process-exit markers are preserved, and no child process remains after validation. | `OwnedServerShutdownQueue`, `DesktopRenderRecoveryPolicy`, VM harness `finally` cleanup, and lifecycle/process tests cover shutdown and cleanup behavior. |

## Reverse Upstream Gap Scan

The pinned upstream startup coordinator, owned-server manager, shutdown queue,
server entry point, and lifecycle tests were manually rescanned against the
portable implementation and tests. Two successive scans found no unresolved
LIFE-01 or LIFE-02 behavior gaps.

## Exit Criteria

Require full lifecycle tests, reviewer self-test, three-machine process cleanup,
and fresh forward/reverse closure scans.
