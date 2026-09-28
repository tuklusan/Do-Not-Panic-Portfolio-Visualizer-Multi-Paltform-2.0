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

# CR-078 Local Soak Timeout Envelope

## Objective

Permit the local four-hour real-product soak coordinator to pass its complete
native-command timeout envelope to the validation engine.

## Gap

The four-hour profile adds the soak duration and cleanup margin to the native
command timeout. That produces 15,300 seconds, but the validation engine
rejected values above 3,600 seconds before any local machine was launched.

## Acceptance

- The internal native-command timeout validator accepts the maximum supported
  240-minute soak plus the documented startup and cleanup margin.
- A fresh availability-probed local cycle reaches each machine available at
  cycle start and records per-machine results.
- The full Release test suite, syntax gate, license gate, reviewer gate, and
  cleanup requirements remain satisfied.

**Status:** Open
