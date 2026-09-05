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

# CR-099: Preserve Ubuntu Slim Soak Closure Evidence

## Objective

Ensure an Ubuntu Slim lane that completes product execution cannot be cancelled
before its required evidence review and closure record are retained.

## Functional Inventory

| ID | Required behavior | 2.0 counterpart | Status |
| --- | --- | --- | --- |
| CE-01 | A completed Ubuntu Slim product soak emits a closure record. | Real-product soak evidence and lane closure-record step. | Open |
| CE-02 | Cancellation or reviewer timeout is represented as a failed lane, never silently omitted from aggregate review. | Aggregate 21-record count and failure reporting. | Open |
| CE-03 | Aggregate review consumes every available lane artifact and identifies the missing record. | `post-soak-review` manifest and closure-record validation. | Open |

## Upstream and Reverse Gates

This is migration workflow infrastructure with no upstream product equivalent.
Before implementation, inspect the complete soak/evidence dependency graph and
the runner cancellation behavior. Before closure, reverse-scan every evidence
path and prove that cancellation cannot produce a false successful matrix.

## Evidence

Hosted run `33979739957` produced the Ubuntu Slim soak artifact but cancelled
the lane before its closure record; aggregate review found 20 instead of 21
manifests/records and failed closed.

## Acceptance

The Ubuntu Slim lane either reaches a complete inspected closure record or
produces an explicit, attributable failure record, and aggregate review remains
fail-closed with no lane silently omitted.
