<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-122: Hosted Review Uncertainty Normalization

## Scope

Normalize a semantically valid NVIDIA review receipt whose optional
`uncertainties` member is JSON `null` to the empty collection required by
hosted closure validation. Also allow the existing cross-platform matrix
workflow to carry one explicit, operator-authorized freeze approval for this
correction. The hosted review instruction explicitly classifies an
independently evidenced provider-quota HTTP 4xx as non-blocking degraded
behavior during the locked ten-minute soak, while unknown or unsubstantiated
AI/RSS failures remain blocking.

## Operator authorization

The operator explicitly authorized this frozen-harness correction and its
one-time hosted validation. The authorization is recorded in the implementing
commit message and is exposed only through the workflow-dispatch boolean
`harness_change_approved`; the default remains false.

## Safety boundary

This change does not convert an incomplete review into a pass. It accepts only
`verdict=PASS`, `reviewComplete=true`, and no blocking findings; `null`
uncertainties is treated as no uncertainty, while non-empty or malformed
uncertainties remain failures. The provider-quota disposition is accepted only
when both `news-evidence.json` and the circular trace independently prove it.
The upstream effective 30-minute minimum is an interval between refresh operations,
not a requirement that the locked ten-minute acceptance soak run for thirty
minutes; one initial refresh with no second refresh before the cadence floor is
compliant.
