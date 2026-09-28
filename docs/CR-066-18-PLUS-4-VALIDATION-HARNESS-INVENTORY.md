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

# CR-066 18-Plus-4 Validation Harness Inventory

## Functional Inventory

| CI-01 | Enumerate and probe all 18 configured GitHub-hosted runner labels, recording OS, architecture, SDK, display capability, and availability. |
| CI-02 | Exercise the real DNPPV Avalonia application on every available hosted runner, not a toy or visual fixture. |
| CI-03 | Exercise the four local lab machines: Lubuntu/LXQt, Windows 10, Windows 11, and Intel Mac Big Sur. |
| CI-04 | Apply platform-specific display handling: Xvfb for headless Linux and bounded physical-display capture for local desktops. |
| CI-05 | Wait for application settling before screenshots and behavioral assertions. |
| CI-06 | While the migration CR queue is open, run the locked 10-minute real-product profile without overlapping runs on a machine; longer profiles are paused by policy. |
| CI-07 | Retrieve both circular trace families (`trace.circular.*` and `yfinance.circular.*`), screenshots, environment manifests, test results, and cleanup reports for every completed run. |
| CI-08 | Pass source changes and generated test-result evidence through the mandatory NVIDIA NIM review harness before acceptance. A missing, incomplete, or failed review blocks the lane. |
| CI-09 | Analyze reviewed evidence, create one JSON CR per actionable defect, and repeat development, review, test, validation, and cleanup until closure. |
| CI-10 | Keep required validation failures blocking; reserve non-blocking behavior for the explicit availability-probe job only. |
| CI-11 | Enforce Windows 10 `D:\SW_DEV\DO-NOT-PANIC-2.0` and `D:\TEMP` hard gates and the Intel Mac one-GiB project-usage limit. |
| CI-12 | Use unique run, machine, RID, and runner artifact names so evidence cannot collide or overwrite. |
| CI-13 | Leave every machine clean after each run and terminate every application, server, display server, and helper process started by the harness. |
| CI-14 | At the start of every soak cycle, probe all four local lab machines and use only currently reachable machines whose storage/display contracts pass. | Per-cycle availability manifest; unavailable is distinct from product failure |
| CI-15 | While open CRs remain, do not launch four-hour soaks. Close the active 10-minute validation family only after two independent complete cycles show no new actionable defects across all 18 hosted runners and every local machine available at each cycle start. | Two reviewed cycle manifests, dual trace sets, and defect ledgers |
| TEST-01 | Verify that a successful run has a real-product screenshot, passing test result, circular trace retrieval, review result, and cleanup result. |

## Reference implementation findings

The Ludo-Arena workflows demonstrate hosted runner matrices, a separate runner
probe, real Avalonia application execution, Linux Xvfb execution, self-hosted
labels, Windows `cmd` execution, and a shell-only macOS fallback for old hosts.
DNPPV must retain those useful patterns while making required acceptance lanes
blocking and routing both source and evidence through NVIDIA NIM.

## Status

The hosted workflow now runs the locked 10-minute real-product soak and
post-soak evidence review on every push to `main` as well as on an explicit
10-minute dispatch. Pull requests remain publish/test-only because protected
AI credentials are unavailable in that event. Per-runner concurrency is
serialized with `cancel-in-progress: false`, so a slow or queued runner is
waited on rather than treated as a failure or overlapped by a duplicate.

Implementation remains in progress; two complete 18-runner cycles and the
available-local-machine companion evidence remain required for closure.
