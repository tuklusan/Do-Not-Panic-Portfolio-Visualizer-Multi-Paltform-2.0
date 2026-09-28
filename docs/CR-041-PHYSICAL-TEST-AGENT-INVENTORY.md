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

# CR-041 Physical Test-Agent Parity Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| AGT-01 | Upstream VM SSH/publish harness launches the real product on a remote desktop | `build/vm/Invoke-ConfigWindowValidation.ps1` publishes platform-specific output, validates the target binary, starts the real application, and retrieves artifacts. |
| AGT-02 | Upstream validation waits for readiness before judging the first frame | Product and configuration validators wait for window handles, startup traces, deferred-lane signals, and bounded warm-up intervals before capture. |
| AGT-03 | Upstream evidence includes visible screenshots, geometry/step logs, and circular trace output | Windows and Linux validators capture screenshots and bounds, write step logs, and retrieve `trace.circular.log` plus `trace.circular.idx`; the Mac validator captures a desktop image and window metadata. |
| AGT-04 | Upstream failure paths terminate owned processes and leave no validation instance behind | Windows process-tree cleanup, Linux `trap` cleanup, Mac `trap` cleanup, timeout handling, and post-run artifact checks are implemented in the validators. |
| AGT-05 | Upstream machine-specific storage policy is enforced before deployment | Windows 10 `D:\SW_DEV\DO-NOT-PANIC-2.0` and `D:\TEMP` checks hard-stop when missing, unwritable, or incorrectly mapped; Linux and Mac paths stay within their documented roots. |
| AGT-06 | Upstream acceptance exercises duplicate instance, menu, fullscreen, motion, degraded, and resize paths | `-ProductScene` validation and its optional acceptance switches exercise the real product workflows, with fixture switches treated as test inputs rather than product substitutes. |
| AGT-07 | Upstream harness behavior is itself gated and reviewable | Migration, license, PowerShell syntax, checked-command, reviewer, checkpoint, artifact-review, and cleanup gates are documented and callable before/after physical acceptance. |

## Reverse scan

The pinned upstream VM, SSH, validation, screenshot, trace, cleanup, and
artifact-review files were rescanned against the current `build/vm` scripts,
machine-access record, product startup, and test contracts. The reverse query
was applied explicitly: **IDENTIFY UPSTREAM LOGIC MISSING FROM THE CURRENT
MIGRATION**. Two successive scans found zero missing agent behaviors. WPF-only
automation APIs are replaced by platform-native Avalonia/desktop capture paths;
that is an implementation adaptation, not a waived acceptance behavior.

## Closure evidence contract

Closure requires harness self-tests, migration and license gates, a clean
committed checkpoint before the acceptance run, physical artifacts reviewed as
real-product evidence, and a final cleanup inspection showing no owned test
process remains.
