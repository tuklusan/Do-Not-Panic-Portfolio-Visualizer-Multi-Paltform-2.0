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

**Status:** Deferred

**Phase:** Phase 7 / hosted validation maintenance

## Objective

Retire the `ubuntu-slim` hosted lane from DNPPV-2.0's required runner matrix after the current in-flight run has been consumed. The supported hosted platform set, publish and real-product soak workflows, runner-count gates, harnesses, evidence aggregation, workbooks, and documentation must agree on the same remaining matrix. This change must not remove any required product target or weaken per-lane build/test, evidence, reviewer, cleanup, or aggregate gates.

## Reason for review

In hosted run `33988763870`, `ubuntu-slim` completed product build, publication, soak, and cleanup, but its lane reviewer step was cancelled. The lane is therefore a poor fit for the mandatory reviewed-evidence contract and should be evaluated for retirement rather than silently treated as successful. The current run must remain undisturbed while its artifacts are consumed.

## Required work

1. Inventory every reference to `ubuntu-slim` in workflow matrices, deterministic gates, scripts, test harnesses, workbooks, README-level documentation, CR records, and acceptance manifests.
2. Remove the lane from required publish and real-product soak matrices and update all expected counts and exact-matrix comparisons consistently. Do not leave a stale optional lane that can affect aggregate counts.
3. Update availability probes, artifact naming/aggregation, evidence expectations, local/hosted test documentation, and queue records so the remaining runner set is explicit and reproducible.
4. Re-run syntax, license, workflow, upstream-mutation, focused/full Release, reviewer, and hosted validation gates. Confirm all remaining lanes still receive build/test, manifests, screenshots where supported, both circular traces, reviewer output, inspected closure evidence, cleanup, and aggregate review.
5. Preserve the current matrix evidence as diagnostic input only; do not claim CR-104 closure from the cancelled lane or from a partial run.

## Acceptance and closure

Closure requires two successive repository scans with no stale `ubuntu-slim` operational references, a passing deterministic workflow gate with the new exact count, a fresh serialized hosted run in which every remaining lane reaches terminal state and its evidence is inspected, and a clean committed/pushed checkpoint. The migration product behavior and all other supported platforms must remain unchanged.

## Upstream gate

This is validation infrastructure, not an upstream product behavior change. Before implementation, inventory the upstream and current validation contracts; after implementation, perform the required reverse scan and record that no upstream product behavior was removed.

