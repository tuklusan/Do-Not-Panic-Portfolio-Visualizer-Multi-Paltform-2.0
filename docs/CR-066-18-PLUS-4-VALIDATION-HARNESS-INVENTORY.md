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
| CI-06 | Run progressively longer soak profiles, beginning with smoke and increasing to sustained runs without overlapping runs on a machine. |
| CI-07 | Retrieve circular trace files, screenshots, environment manifests, test results, and cleanup reports for every completed run. |
| CI-08 | Pass source changes and generated test-result evidence through the mandatory DeepSeek review harness before acceptance. A missing, incomplete, or failed review blocks the lane. |
| CI-09 | Analyze reviewed evidence, create one JSON CR per actionable defect, and repeat development, review, test, validation, and cleanup until closure. |
| CI-10 | Keep required validation failures blocking; reserve non-blocking behavior for the explicit availability-probe job only. |
| CI-11 | Enforce Windows 10 `D:\SW_DEV\DO-NOT-PANIC-2.0` and `D:\TEMP` hard gates and the Intel Mac one-GiB project-usage limit. |
| CI-12 | Use unique run, machine, RID, and runner artifact names so evidence cannot collide or overwrite. |
| CI-13 | Leave every machine clean after each run and terminate every application, server, display server, and helper process started by the harness. |
| TEST-01 | Verify that a successful run has a real-product screenshot, passing test result, circular trace retrieval, review result, and cleanup result. |

## Reference implementation findings

The Ludo-Arena workflows demonstrate hosted runner matrices, a separate runner
probe, real Avalonia application execution, Linux Xvfb execution, self-hosted
labels, Windows `cmd` execution, and a shell-only macOS fallback for old hosts.
DNPPV must retain those useful patterns while making required acceptance lanes
blocking and routing both source and evidence through DeepSeek.

## Status

Open. This inventory defines the required behavior before implementation.
