<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
-->

# DNPPV-2.0 Autonomous Migration Goal

## Goal Text For `/goal`

Autonomously take the DNPPV-2.0 migration from its current repository state to
the production-ready cross-platform product defined by the upstream DNPPV-1.0
reference and the current migration design. Use the JSON issue tracker in
`docs/AUDIT_STATE.json` as the sole work queue: reconcile it against the
tracked CR documents, create a narrowly scoped CR for every discovered gap,
and process CRs one at a time through verified closure without prompting.

The primary success objective is zero loss of upstream functionality, logic,
behavior, or UI. Before each CR, manually inspect the relevant upstream source,
tests, workflows, and documentation from disk line by line; record every
functional behavior, business rule, failure path, lifecycle rule, timing rule,
layout rule, accessibility rule, and test obligation; and map each item to a
2.0 implementation and proof. Before closure, independently repeat the
upstream scan in reverse from the 2.0 implementation and prove two successive
complete zero-gap scans. A filename or framework difference is acceptable only
when the behavior is preserved or an explicit architecture disposition is
recorded. Installer-only, WPF-only, and historical upstream artifacts remain
retired because 2.0 is Avalonia-only on every supported platform.

The second primary success objective is complete cross-platform validation.
Maintain CI coverage across all 21 configured GitHub-hosted runner lanes and
the four local lab machines documented in `docs/TEST_MACHINE_ACCESS.md`.

Local-machine soak launches are enabled again after the hosted-only checkpoint.
At the start of each cycle, probe all four retained lab machines and use every
available contract-compliant machine alongside the GitHub-hosted matrix.

At the start of every local validation or soak cycle, probe all four local
machines and use only those currently reachable and contract-compliant;
temporary local unavailability is not a product failure when the complete
hosted matrix provides proof. Wait for queued or slow hosted runners to finish
before classifying a lane as failed. Validate the real production application,
never a toy or visual fixture, on every available target and every hosted RID.

For every validation cycle, build from a committed checkpoint, run the
required tests, exercise real product behavior including RSS and AI news when
credentials are supplied, capture settled screenshots where the platform
supports them, retrieve both bounded circular trace files, inspect the result
manifests and screenshots, verify process and artifact cleanup, and pass the
test evidence through the NVIDIA NIM reviewer harness. While CRs remain open,
use the project's locked 10-minute real-product soak profile; do not start
longer soak profiles until the tracker and project policy explicitly permit
them. A failed lane produces a defect CR with evidence; fix it, rerun the
required reviewer and validation gates, and repeat until the acceptance
criteria are genuinely proven.

For every code or workflow change, run the checked PowerShell wrapper for
nontrivial generated PowerShell, pass license, syntax, upstream-mutation,
workflow-configuration, migration-behavior, and NVIDIA review gates as
applicable, commit the immutable candidate, and push it before protected local
or hosted validation. Any code correction invalidates the prior code review
and requires a complete fresh review. Never expose API keys, passwords, or
private review material in source, command arguments, traces, screenshots,
manifests, or commits. Explicitly terminate every process, test instance, or
development server started by the workflow before declaring a CR closed.

Continue creating, implementing, testing, debugging, reviewing, committing,
and pushing CRs autonomously until all currently applicable migration work is
closed, all required upstream behaviors have an implemented or explicitly
 approved disposition, the 21-runner and available-local-machine evidence is
complete, and the project is ready for the next phase boundary. Escalate only
when a hard safety/security issue, unavailable required authority, or
operator-only environment intervention makes further progress impossible.

## Completion Conditions

- `docs/AUDIT_STATE.json` and all CR documents agree on the open/closed queue.
- Every applicable upstream behavior has an exact 2.0 mapping or an explicit,
  approved retirement/replacement disposition.
- Each closed CR has pre-development and closure migration gates, including
  two successive zero-gap scans.
- The active NVIDIA review harness passes required code and test-artifact
  reviews, with no stale review result authorizing a changed snapshot.
 - CI demonstrates the complete 21-runner matrix without treating queue delay as
  failure.
- Each validation cycle checks available local lab machines dynamically and
  preserves the documented storage, display, secret, trace, and cleanup rules.
- Real-product screenshots, both circular traces, test output, and cleanup
  evidence pass artifact review with no unresolved actionable defects.
- The final implementation is committed, pushed, and the working tree is
  clean.
