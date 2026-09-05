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

# CR-093 Deterministic Atom Feed Fixture

## Functional Inventory

| Item | Upstream/current behavior | Required evidence |
| --- | --- | --- |
| CR-01 | Atom entries with `href` links are parsed as headlines when their publication date is within the freshness window. | Focused test with a fixed clock |
| CR-02 | Production freshness filtering remains time-based and is not weakened for tests. | Full Release test suite and hosted rerun |

The hosted 18-runner matrix exposed that the Atom parser test used a fixed
August 29, 2026 publication date but the live test clock. Once the current date
passed the seven-day freshness window, the product correctly filtered the
fixture and the test incorrectly expected a headline. The test now supplies a
fixed August 29 clock so it verifies Atom parsing independently of wall-clock
date while preserving the production freshness rule.

## Closure

**Status:** Open

**Dependencies:** None

**Validation:** Focused test, full Release build/test, hosted matrix rerun,
upstream forward/reverse gates, license gate, syntax gate, NVIDIA review, and
clean checkpoint.
