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

# CR-092: Enforce local companion soaks for hosted matrices

## Objective

Whenever a hosted GitHub runner matrix is dispatched, discover the four local
lab machines at the start of that cycle and run the same-duration real-product
soak on every machine that is available. Every local lane must first terminate
residual project-owned product processes and prove cleanup before launch.

## Functional Inventory

| ID | Required behavior | Evidence |
| --- | --- | --- |
| LOCAL-SOAK-01 | Probe all four lab machines before each matrix cycle and record available/unavailable state. | Availability manifest with timestamp and machine identity. |
| LOCAL-SOAK-02 | Terminate residual project-owned product and YFinance processes before launch; failure hard-stops that local lane. | Cleanup result and zero-residual verification. |
| LOCAL-SOAK-03 | Run the real product, not a fixture, for the exact hosted soak duration with identical RSS, AI, trace, screenshot, settling, and cleanup goals. | Shared soak-result schema and two circular traces. |
| LOCAL-SOAK-04 | Unavailable local machines are explicit skips; all 18 hosted lanes remain mandatory. | Aggregated result distinguishes skipped local lanes from failures. |
| LOCAL-SOAK-05 | Local and hosted evidence is reviewed together and defects become JSON CRs before the cycle can count. | Combined artifact-review manifest and queue update. |

## Required Gates

The workflow must use a shared profile and result schema for hosted and local
lanes. The preflight must not silently convert an available-machine failure
into a skip. A guaranteed finalizer must terminate residual processes and clean
disposable artifacts. Run migration, license, syntax, NVIDIA review, build,
artifact, and commit/push gates before closure.

## Acceptance

- Every hosted matrix dispatch creates one availability manifest and one local
  result for each machine available at preflight.
- Local lanes use the exact hosted duration and real-product acceptance rules.
- Two independent complete cycles pass all 18 hosted lanes and all available
  local lanes with no new defects.
