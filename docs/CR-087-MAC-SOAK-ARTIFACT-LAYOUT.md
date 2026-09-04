<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-087: Intel Mac Soak Artifact Layout

## Functional Inventory

| CR-01 | Keep Intel Mac soak artifacts in the declared cycle root without unexpected nesting | Mac validation driver | artifact manifest |

This CR normalizes the local artifact copy returned by the slow Intel Mac
lane. The 2026-09-04 targeted run proved fresh RSS and successful AI
generation, but the remote `artifacts` directory was copied as a nested
directory below the machine artifact root instead of matching the Linux and
Windows layout.

## Upstream Inventory Gate

Before implementation, re-read the upstream Mac harness artifact, screenshot,
trace, cleanup, home-confinement, and size-budget behavior line by line.
Map every behavior to the current 2.0 harness and record reverse gaps before
editing.

## Acceptance

- `news-evidence.json`, screenshots, and both circular trace files are directly
  under the local machine artifact root in the documented layout.
- Two keyed targeted Mac runs prove fresh RSS and successful AI generation.
- The remote root remains under `~/SOFTWARE_DEV/DNPPV_20/`, never exceeds 1 GiB,
  and is removed after success or failure.
