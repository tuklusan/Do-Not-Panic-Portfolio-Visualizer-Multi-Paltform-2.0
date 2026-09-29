<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# Physical acceptance checkpoint: 93fd5ac1

Fresh remote-lab evidence was collected from the canonical `93fd5ac1` build
without launching the application on the local machine. All temporary output
was retained under `D:\TEMP`.

| Machine | Result | Evidence |
| --- | --- | --- |
| Lubuntu Linux x64 | Passed on retry | `D:\TEMP\dnppv2-local-cycle-93fd5ac1-linux-retry` |
| Windows 10 x64 | Passed | `D:\TEMP\dnppv2-local-cycle-93fd5ac1-win10` |
| Windows 11 x64 | Passed | `D:\TEMP\dnppv2-local-cycle-93fd5ac1-win11` |
| Intel macOS Big Sur x64 | 10-minute soak passed through native PTY SSH | `D:\TEMP\dnppv2-local-cycle-93fd5ac1\macos-x64-intel-big-sur\artifacts` |

The Linux and Windows traces recorded RSS usable, an AI request, zero quote
server launch/request failures, and zero `FRAME_OVERDUE` events. The Mac soak
recorded RSS usable, 3,133 live quote responses, zero quote-server launch or
request failures, and zero overdue frames. The Mac AI-required rerun reached
the soak completion but did not return from its provider/grace path and was
terminated through the remote cleanup trap; it is not counted as a pass.

The checkpoint is therefore evidence of corrected launch, quote-server, and
frame-overdue behavior, not final four-machine release closure. A unified
four-machine receipt, repeat AI-required Mac evidence, hosted closure, and
trusted signing remain open.
