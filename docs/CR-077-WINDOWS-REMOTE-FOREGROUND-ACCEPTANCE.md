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

# CR-077 Windows Remote Foreground Acceptance

## Functional Inventory

| ID | Required behavior | Acceptance evidence |
| --- | --- | --- |
| WIN-FOCUS-01 | A Windows physical acceptance run launched through SSH must execute in the logged-in interactive desktop session used for screenshots. | Session identity, window-station identity, product PID, and foreground-window evidence from Win10 and Win11. |
| WIN-FOCUS-02 | The real product window must become foreground before geometry, menu, and screenshot assertions. | Settled product-scene run records successful foreground acquisition before every required capture. |
| WIN-FOCUS-03 | A focus failure must report the underlying session/window condition and must not be masked by a later missing-artifact SCP error. | Deliberate focus failure produces a diagnostic step-log reason and bounded cleanup. |
| WIN-FOCUS-04 | Successful retrieval must preserve all required screenshots, circular traces, and cleanup evidence from both Windows machines. | Five-minute and progressive physical cycles pass with non-empty artifact pairs and zero residual product processes. |

## Required investigation

Read the upstream Windows harness and the current `build/vm/Invoke-ConfigWindowValidation.ps1`
line by line. Compare SSH-launched PowerShell, scheduled-task, interactive-session,
WinSta0, desktop, and foreground-window behavior. Identify the mechanism used by
the upstream harness to run GUI acceptance in the logged-in desktop. Keep the
product implementation unchanged unless the upstream comparison proves a product
defect.

## Closure gates

Closure requires focused and full Release tests, two successive upstream and
reverse-gap scans, NVIDIA NIM source/evidence review, a deliberate failure-path
check proving missing screenshots do not mask the original error, successful
physical acceptance on each available Windows machine, and remote cleanup.

**Status:** Open
