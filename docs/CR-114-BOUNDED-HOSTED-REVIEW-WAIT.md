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

# CR-114: Bound Hosted Reviewer Wait Without Weakening Evidence Gates

## Status

Open. Discovered during serialized run `34040146263`, where the only remaining
lane (`macos-14`, job `101505317624`) remained in NVIDIA evidence review for
hours after its product soak had passed.

## Objective

Reconcile the emergency-review expectation with the hosted workflow's current
two-hour per-request timeout. A slow or unavailable reviewer must not hold a
serialized matrix indefinitely, but a timeout must also never become an
implicit PASS or discard required evidence.

## Acceptance Criteria

1. The hosted review path has one explicit, bounded reviewer-wait policy for
   each lane and for the aggregate review; the policy is shorter than the
   current unbounded-in-practice wait while remaining suitable for NIM's known
   slow responses.
2. A reviewer timeout or unavailable response creates a clearly marked,
   secret-free `REVIEW_UNAVAILABLE`/quarantine result and fails the lane or
   defers the matrix according to an explicit documented policy. It cannot
   create a semantic PASS receipt.
3. Build/test, screenshots, circular traces, RSS/AI evidence, manifests, and
   cleanup evidence remain retained and inspectable when review times out.
4. The pre-launch serialization rule remains intact; no overlapping matrix is
   started as a timeout workaround.
5. Forward/reverse migration scans, NVIDIA review, workflow/license/syntax
   gates, and a fresh serialized proof validate the change.

## Scope Boundary

CR-113's bounded retries for empty or malformed reviewer output remain valid.
This CR addresses the separate request-duration and hosted-lane policy; it
does not change product RSS/AI cadence or upstream product behavior.

