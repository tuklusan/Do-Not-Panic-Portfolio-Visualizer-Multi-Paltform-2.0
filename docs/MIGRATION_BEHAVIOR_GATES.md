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

# Migration Behavior Gates

Every migration CR must treat upstream DNPPV-1.0 behavior as the implementation
authority. Similar appearance, a representative happy path, or an existing
DNPPV-2.0 abstraction is not sufficient evidence of parity.

## Pre-Development Gate

Before changing product code for a CR:

1. read the upstream entry points and follow their callers, callees, view models,
   controls, services, settings, failure paths, timers, and lifecycle hooks;
2. list every related functional behavior, including geometry, timing, state,
   persistence, degraded behavior, recovery, logging, and cleanup;
3. cite the exact upstream files and symbols supporting each inventory item;
4. map each item to planned implementation and validation evidence; and
5. record the inventory document and scanned upstream commit in
   `docs/AUDIT_STATE.json`.

Run:

```powershell
./build/Test-MigrationBehaviorGate.ps1 -CrId CR-NNN -Stage PreDevelopment
```

The command must pass before implementation proceeds. Discovery of a missing
behavior invalidates the pass: update the inventory, restart its upstream scan,
and rerun the gate.

## Closure Gate

After implementation and acceptance, perform a new upstream scan without using
the pre-development inventory as a completeness shortcut. Reconcile every
behavior to implemented code and reviewed evidence, or to an explicit
user-approved exception. A future CR is not an acceptable silent disposition.

The closure audit must record:

- the exact upstream commit and source files rescanned;
- every discovered gap and its final disposition;
- zero unmapped behaviors; and
- at least two successive complete scans that found zero gaps.

Run:

```powershell
./build/Test-MigrationBehaviorGate.ps1 -CrId CR-NNN -Stage Closure
```

Any failure hard-stops CR closure. Fix the gap and restart the complete closure
scan until the required successive zero-gap result is reached.
