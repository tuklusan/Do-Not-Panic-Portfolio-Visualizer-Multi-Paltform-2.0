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

# CR-069 Progressive Soak Profiles

Run and review the real-product profiles in this order on the 18-plus-4 matrix:

1. 5 minutes
2. 10 minutes
3. 30 minutes
4. 2 hours
5. 4 hours

Each profile starts only after the prior profile's artifacts are reviewed and
the machine is clean. Failures create a JSON CR with the exact run identity,
trace pair, screenshot/evidence manifest, and reviewed diagnosis. After a fix,
the affected profile and every shorter profile are relaunched. The final
profile closes only when no product process remains and successive artifact
reviews show zero unresolved failures.

At the start of each profile cycle, the harness rechecks all four local lab
machines and runs on the currently reachable contract-compliant subset. The
cycle manifest records every unavailable machine and its reason; availability
changes between cycles do not invalidate the evidence from a completed cycle.
If local machines are unavailable, the profile may still close when all 18
hosted lanes have provably executed and passed the real-product gates; local
availability is recorded, never silently converted into a pass.

**Depends on:** CR-066, CR-067, CR-068  
**Status:** Open
