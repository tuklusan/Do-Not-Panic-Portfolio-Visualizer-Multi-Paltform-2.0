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

# DO NOT PANIC PORTFOLIO VISUALIZER 2.0 (UNDER DEVELOPMENT)

Based on original work by Supratim Sanyal of SANYALnet Labs.

This repository is the under-development migration workspace for **DO NOT
PANIC PORTFOLIO VISUALIZER 2.0**. The goal is to migrate the public upstream
1.0 release to a uniform **.NET 10 + Avalonia** desktop product line.

## Upstream Source

- Upstream 1.0 public repository:
  [tuklusan/DO-NOT-PANIC-PORTFOLIO-VISUALIZER](https://github.com/tuklusan/DO-NOT-PANIC-PORTFOLIO-VISUALIZER)
- GitHub homepage for this repository points to that upstream source as the
  migration origin.

## Development Status

- This repository is **under active development**.
- It is a fresh migration baseline, not a packaged public release.
- No DNPPV-2.0 product binaries, installers, or release artifacts are
  published from this repository yet.

## Repository Baseline

The current baseline intentionally keeps only the migration-start artifacts we
need:

- the migration design source of truth at
  `docs/DO-NOT-PANIC-Avalonia-Cross-Platform-Migration-Design-Rev-01.md`
- the DeepSeek review gate under `build/`
- the DeepSeek gate reference document at
  `docs/FRESH-PROJECT-DEEPSEEK-REVIEW-GATE.md`
- the machine-access workbook at `docs/TEST_MACHINE_ACCESS.md`
- the empty migration issue tracker at `docs/AUDIT_STATE.json`

All inherited application binaries, validation bundles, and stale issue history
were removed before the new migration repo was created.

## Workflow Guards

- license headers are mandatory for project artifacts
- pushes to the upstream 1.0 repository are blocked locally
- the local migration issue tracker starts empty and is maintained in JSON
- nontrivial generated PowerShell commands run through
  `build/Invoke-CheckedPowerShell.ps1`
- the Avalonia migration design document is the architecture source of truth

## License

This repository uses the same root [LICENSE](LICENSE) text as the upstream
project.
