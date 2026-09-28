<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-086: Windows 10 Interactive Soak Launch

## Functional Inventory

| CR-01 | Launch the real product interactively on Windows 10 and retrieve completion evidence | Windows validation driver | result manifest and cleanup |

This CR covers the Windows 10 local validation path after the 2026-09-04
five-minute cycle produced `DONE_FILE_MISSING` and no retrievable `step.log`.
The product must launch in the logged-in desktop session, show its window, and
complete its bounded cleanup before the harness records success.

## Upstream Inventory Gate

Before implementation, re-read the upstream Windows launch, desktop-session,
process-lifecycle, screenshot, and cleanup behavior line by line. Record every
relevant behavior and every current 2.0 mapping here. The reverse scan must
also identify any current 2.0 behavior absent from upstream or incorrectly
assumed by the harness.

## Acceptance

- Task registration, start, state, last-run result, and action diagnostics are
  captured without secrets.
- `done.txt` is written only after the real product and its child processes
  have closed and keyed RSS/AI evidence has passed.
- A logged-in Windows 10 user can see the product window.
- Two successive targeted runs pass and leave no task, product, or temporary
  project process behind.

## Current Evidence

The coordinator destination was corrected to reside below the already-created
target publish directory, which is `D:\SW_DEV\DO-NOT-PANIC-2.0` for Windows 10.
The fresh cycle `artifacts-win10-ai-retry` on 2026-09-05 completed with
`status=Passed`, and both configuration-window and real-product validation
passed. Its manifest recorded the required `D:\SW_DEV` and `D:\TEMP` storage
contract. A second targeted run is still required before this CR closes.
