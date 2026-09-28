<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-080: Linux Artifact Retrieval Failure

## Functional Inventory

| CR-01 | Initialize artifact-retrieval failure state before copy error handling | `Invoke-ConfigWindowValidation.ps1` | Linux validation driver |
| CR-02 | Preserve original retrieval failure in the machine result and cycle manifest | `Invoke-LocalLabSoakCycle.ps1` | Local soak coordinator |
| CR-03 | Clean helper processes and retain a complete failure record | `Invoke-ConfigWindowValidation.ps1`, `Invoke-LocalLabSoakCycle.ps1` | Local validation tests |

## Upstream Comparison

The Linux lane must represent deployment, launch, artifact retrieval, and
cleanup failures explicitly. An artifact-copy failure must never be replaced by
an uninitialized-variable error or cause the coordinator to lose the machine
record. This inventory is tied to upstream commit
`65a53bbbf0cf9af1058363f8939d464ca03858f8` and is required by the migration
behavior gate before CR-080 closure.

## Status

Implementation is hardened with staged extraction, executable verification, and
bounded retries after transfer. Focused Linux validation and the fresh
four-machine ten-minute companion cycle
`dnppv2-local-cycle-10m-fourth-20260905-074500` passed, and the closure-gate
scans passed. CR-080 is closed.
