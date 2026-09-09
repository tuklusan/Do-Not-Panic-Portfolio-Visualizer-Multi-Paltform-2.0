<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-121: NVIDIA Review Gate Thinking and Transport Cleanup

## Purpose

Improve the active PowerShell NVIDIA review gate without weakening its minimal
packet, immutable-snapshot, fail-closed, receipt, or secret-handling contracts.

## Approved Scope

- Enable documented, model-specific thinking controls for substantive review
  calls and preserve private-reasoning exclusion from results and telemetry.
- Handle NVIDIA asynchronous HTTP 202 responses by polling the request status
  within the same logical request deadline and retry budget.
- Serialize complete host-local reviews with a global named mutex, separately
  from the 30-second response-spacing mutex.
- Replace the full-review health probe with a small exact-JSON probe against
  both authorized models.
- Use documented phase-specific token ceilings with explicit reasoning budget
  and JSON headroom.

The attachment that prompted this work referenced a Python reviewer absent from
the active workspace. Only findings reproduced or adapted to the active
PowerShell entry points are in scope. The attachment's Python-specific prompt
deduplication and coverage-state recommendations are therefore deferred.

## Operator Authorization

The operator explicitly authorized implementation of the recommended NVIDIA
review-gate cleanup and requested thinking to be enabled across substantive
review calls. This approval authorizes the frozen-harness change for CR-121;
the approval marker and reason must also appear in the implementation commit
message.

## Acceptance

- Harness, bootstrap, review-gate, syntax, license, and workflow self-tests
  pass.
- Thinking payloads are explicit and model-specific for Super and Lightning.
- HTTP 202 polling cannot create a duplicate logical inference attempt and is
  bounded by the original deadline.
- Health checks use the small exact-JSON path for both authorized models.
- Review packets remain minimal and receipt/snapshot validation is unchanged.
- No review, freeze, cleanup, or upstream-mutation gate is bypassed.
