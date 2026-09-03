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

# CR-072: Bound Linux Desktop Screenshot Capture

## Functional Inventory

The upstream-inspired physical validation workflow must capture the visible
real product on the logged-in Linux desktop, while never allowing a screenshot
utility to hold the validation session open indefinitely. The capture sequence
must prefer the installed X11 capture utility, apply a hard termination grace
period to every fallback, reject empty output, and record the selected tool in
the circular validation step log.

| ID | Upstream behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| CAP-01 | Capture the visible logged-in desktop and retain non-empty PNG evidence. | `build/vm/Invoke-ConfigWindowValidation.ps1` capture helper and artifact retrieval. | Implemented |
| CAP-02 | Bound external capture commands and terminate them when they exceed the capture budget. | `timeout --kill-after=5s 15` around Linux capture utilities. | Implemented |
| CAP-03 | Prefer the desktop-native capture path and retain a fallback for alternate X11 environments. | `gnome-screenshot`, `scrot`, then bounded ImageMagick `import`. | Implemented |
| CAP-04 | Record evidence provenance and diagnose capture failure without arbitrary product logs. | `step.log` capture-tool/failure entries plus the circular product trace. | Implemented |

## Required Gates

Before implementation, rescan the Linux capture behavior in the upstream
reference and the current product-scene driver. Before closure, repeat the
forward and reverse behavior scans and record two successive zero-gap results.

## Acceptance

- A fresh Linux real-product run captures a non-empty PNG without an unbounded
  `import` process.
- Every capture attempt has a hard timeout and kill-after interval.
- A failed capture fails the lane with a useful step-log reason and leaves no
  product, sidecar, screenshot, or SSH helper process behind.
- The focused harness rehearsal, full Release build/test, license/syntax gates,
  NVIDIA NIM source/evidence review, and local artifact inspection pass.
