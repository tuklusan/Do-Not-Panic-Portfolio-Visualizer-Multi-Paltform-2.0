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

# CR-115: Investigate Hosted Render-Recovery Episodes

## Status

Open. Discovered in hosted run `34048608640`, where several macOS lanes
reported two render-recovery episodes during a ten-minute real-product soak.

## Objective

Determine whether the observed recovery episodes are the bounded, traceable
upstream recovery behavior expected under hosted desktop rendering or a real
2.0 render-loop regression. Preserve upstream recovery semantics while making
the decision evidence-based and actionable.

## Functional Inventory

| RCV-01 | Upstream render heartbeat detects missing callbacks and performs bounded, non-blocking recovery. | `docs/CR-011-UPSTREAM-BEHAVIOR-INVENTORY.md`, upstream render heartbeat and desktop lifecycle sources, and the 2.0 render heartbeat/recovery services. | Required |
| RCV-02 | Recovery is traceable, bounded, cancellable, and does not silently terminate the product scene. | Circular trace events, recovery policy tests, and settled product screenshots. | Required |
| RCV-03 | Clean and abnormal render-run markers select the correct recovery mode and reset after a clean run. | `DesktopRenderRecoveryPolicy` and its tests. | Required |
| RCV-04 | Hosted evidence must distinguish expected recovery from repeated sustained render stalls. | Per-lane trace timing, frames/heartbeat counters, screenshots, and reviewer disposition. | Required |

## Acceptance Criteria

1. Read the pinned upstream render-heartbeat, recovery-policy, desktop-shell,
   and corresponding test sources line by line.
2. Compare each hosted macOS recovery episode with the upstream contract and
   current 2.0 implementation; create a focused defect CR if behavior is
   genuinely divergent.
3. Add deterministic tests or evidence assertions for the classification.
4. Run the required local gates, NVIDIA review, and a fresh serialized proof
   without weakening the real-product evidence gate.

## Reverse Upstream Gap Scan

Pending implementation. The reverse scan must have two successive zero-gap
passes before closure.

