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

Closed. Discovered during serialized run `34040146263`, where the only remaining
lane (`macos-14`, job `101505317624`) remained in NVIDIA evidence review for
hours after its product soak had passed.

## Objective

Reconcile the emergency-review expectation with the hosted workflow's current
two-hour per-request timeout. A slow or unavailable reviewer must not hold a
serialized matrix indefinitely, but a timeout must also never become an
implicit PASS or discard required evidence.

## Acceptance Criteria

1. The hosted review path has one explicit, bounded reviewer-wait policy for
   each lane and for the aggregate review; the normal policy is 15 minutes and
   the separately selectable `one-time-slow-review` policy is capped at two
   hours for an explicitly authorized exceptional run.
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

## Operational Policy

Pushes and ordinary manual dispatches use `bounded-15m`. The
`one-time-slow-review` choice is an operator-authorized exception for one
specific run and must be recorded with its run ID before use. It is not the
default and must not be used to keep CR-114 or routine queue processing
waiting for hours. A request timeout is still unavailable reviewer output, not
PASS; the lane's retained evidence is quarantined or failed closed.

## Closure Evidence

Commit `d0e001f` passed the local workflow, license, syntax, migration, NVIDIA
workflow self-test, and pre-push gates. Hosted run `34048608640` used
`REVIEW_WAIT_POLICY=bounded-15m`; all 21 publish lanes completed and the
terminal run contained no old two-hour reviewer wait. Its product/evidence
failures were dispositioned into CR-108, CR-112, and CR-115; expected NTP
fallback findings matched CR-039. No semantic PASS was created for failed or
unavailable review evidence.

## Scope Boundary

CR-113's bounded retries for empty or malformed reviewer output remain valid.
This CR addresses the separate request-duration and hosted-lane policy; it
does not change product RSS/AI cadence or upstream product behavior.

## Functional Inventory

| OPS-01 | Ordinary push and manual runs use a bounded reviewer request timeout rather than the current two-hour default. | `publish-six-rids.yml` selects `bounded-15m` and passes 900 seconds. | Required |
| OPS-02 | A specifically authorized slow-review exception remains possible without silently changing the normal policy. | `review_wait_policy=one-time-slow-review` selects 7200 seconds only for that dispatch. | Required |
| OPS-03 | Reviewer timeout or unavailable output cannot become PASS. | NVIDIA harness emits `REVIEW_UNAVAILABLE`; lane inspection and quarantine remain fail-closed. | Required |
| OPS-04 | Matrix serialization and retained product evidence remain intact during timeout handling. | Root concurrency group, closure receipt, artifact upload, and post-soak validation remain unchanged. | Required |

## Reverse Upstream Gap Scan

The upstream migration workflow and current project workflow were rescanned for
trigger, serialization, timeout, retry, quarantine, evidence-retention, and
closure semantics. The timeout policy is an operational 2.0 gate addition; it
does not remove upstream product behavior. Two successive committed-disk scans
found no unmapped behavior within this CR's scope.
