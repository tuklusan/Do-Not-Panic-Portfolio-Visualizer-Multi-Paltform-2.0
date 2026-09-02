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

# CR-068 18-Plus-4 Soak Matrix

Create the executable matrix that runs the real product on every available
GitHub-hosted runner label and the four local lab machines. Availability probes
may report unavailable labels, but a lane that is available and required must
build, test, launch, capture, retrieve circular traces, pass DeepSeek review of
source and evidence, and clean up before acceptance.

At the beginning of every soak cycle, re-probe all four local lab endpoints.
Machines may be powered off or have changing addresses; a cycle uses the
reachable subset that passes its documented contract and records the others as
`UnavailableAtCycleStart`. Local-machine non-availability is not a blocker when
all 18 hosted runner labels have provably executed the real product; it remains
an explicit availability result and must not be reported as a local product
pass. A machine that was available at cycle start remains a required lane for
that cycle.

The matrix must use unique run/host/RID artifact names, Linux Xvfb where needed,
the documented Windows 10 `D:\SW_DEV\DO-NOT-PANIC-2.0` and `D:\TEMP` gates, and the
Intel Mac one-GiB ceiling. Hosted publish-only jobs are insufficient. AI-news
lanes receive the OpenRouter key from a protected CI/local secret and pass it
only as `DNPPV_OPENROUTER_API_KEY` or `OPENROUTER_API_KEY`; the value is never
committed or included in review evidence.

The matrix is not considered complete after one successful pass. Closure
requires two independent full four-hour cycles. Each cycle must execute all 18
hosted runner labels and every local machine available at that cycle's start;
unavailable local machines are recorded as unavailable, never as passing lanes.

**Depends on:** CR-066, CR-067  
**Status:** Open
