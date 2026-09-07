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

# CR-104: Retire the Ubuntu Slim Hosted Lane

**Priority:** Low

**Status:** In progress; implementation complete, fresh 20-lane validation pending

**Phase:** Phase 7 / hosted validation maintenance

## Objective

Retire the `ubuntu-slim` hosted lane from DNPPV-2.0's required runner matrix after the in-flight run was consumed. The supported hosted platform set, publish and real-product soak workflows, runner-count gates, harnesses, evidence aggregation, workbooks, and documentation must agree on the same remaining 20-lane matrix. This change must not remove any required product target or weaken per-lane build/test, evidence, reviewer, cleanup, or aggregate gates.

## Reason for review

In hosted run `33988763870`, `ubuntu-slim` completed product build, publication, soak, and cleanup, but its lane reviewer step was cancelled. The lane is therefore a poor fit for the mandatory reviewed-evidence contract and should be evaluated for retirement rather than silently treated as successful. The current run must remain undisturbed while its artifacts are consumed.

## Required work

1. Inventory every reference to `ubuntu-slim` in workflow matrices, deterministic gates, scripts, test harnesses, workbooks, README-level documentation, CR records, and acceptance manifests.
2. Remove the lane from required publish and real-product soak matrices and update all expected counts and exact-matrix comparisons consistently. Do not leave a stale optional lane that can affect aggregate counts.
3. Update availability probes, artifact naming/aggregation, evidence expectations, local/hosted test documentation, and queue records so the remaining runner set is explicit and reproducible.
4. Re-run syntax, license, workflow, upstream-mutation, focused/full Release, reviewer, and hosted validation gates. Confirm all remaining lanes still receive build/test, manifests, screenshots where supported, both circular traces, reviewer output, inspected closure evidence, cleanup, and aggregate review.
5. Preserve the current matrix evidence as diagnostic input only; do not claim CR-104 closure from the cancelled lane or from a partial run.

## Functional Inventory

| INF-01 | The hosted workflow publishes each supported runner/RID pair and runs the real product soak for the same exact set. | The workflow retains the 20 supported pairs in both matrices; `ubuntu-slim` is removed from each. | Validation-only change; no product behavior is removed. |
| INF-02 | Every required hosted lane is covered by exact-count, duplicate, artifact, cleanup, screenshot, dual-circular-trace, reviewer, and aggregate gates. | `EXPECTED_LANE_COUNT`, matrix gate, and aggregate validator now operate on 20 lanes; per-lane evidence requirements are unchanged. | Fresh 20-lane run remains required. |
| INF-03 | Unsupported or unavailable validation infrastructure must fail closed rather than be silently counted as product success. | The retired lane is absent from required matrices; its cancelled historical run remains diagnostic context only. | No upstream product code is changed. |

## Acceptance and closure

Closure requires two successive repository scans with no stale `ubuntu-slim` operational references, a passing deterministic workflow gate with the new exact count, a fresh serialized hosted run in which every remaining lane reaches terminal state and its evidence is inspected, and a clean committed/pushed checkpoint. Historical run references may remain only as explicitly labeled diagnostic context. The migration product behavior and all other supported platforms must remain unchanged.

## Upstream gate

This is validation infrastructure, not an upstream product behavior change. Before implementation, inventory the upstream and current validation contracts; after implementation, perform the required reverse scan and record that no upstream product behavior was removed.
