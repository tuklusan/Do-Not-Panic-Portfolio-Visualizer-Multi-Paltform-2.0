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

# DNPPV 2.0 release runbook

This runbook is the reproducible packaging contract for the six supported
self-contained .NET 10 bundles. A public release must not be announced until
the migration tracker, hosted matrix, physical acceptance, review receipts,
and signing records are all closed.

## Supported downloads

Publish exactly these runtime identifiers as separate bundles:

| Platform | RID | Application executable |
| --- | --- | --- |
| Windows Intel/AMD | `win-x64` | `DoNotPanicPortfolioVisualizer.App.exe` |
| Windows ARM64 | `win-arm64` | `DoNotPanicPortfolioVisualizer.App.exe` |
| Linux Intel/AMD | `linux-x64` | `DoNotPanicPortfolioVisualizer.App` |
| Linux ARM64 | `linux-arm64` | `DoNotPanicPortfolioVisualizer.App` |
| macOS Intel | `osx-x64` | `DoNotPanicPortfolioVisualizer.App` |
| macOS Apple Silicon | `osx-arm64` | `DoNotPanicPortfolioVisualizer.App` |

Every bundle must also contain `YFinanceServer/`, the generated
`release-manifest.json`, and the project `LICENSE`/attribution material.

## Reproducible package contract

From the repository root, publish one RID at a time to avoid shared-filesystem
contention during local reproduction:

```powershell
dotnet publish src/DoNotPanicPortfolioVisualizer.App/DoNotPanicPortfolioVisualizer.App.csproj `
  --configuration Release --runtime <rid> --self-contained true `
  --output artifacts/<rid>
./build/generate-release-manifest.ps1 -ReleaseDirectory artifacts/<rid> -ProductVersion 2.0
```

The hosted workflow currently publishes the RID directory. The release
operator must run the manifest and packaging commands above on the exact
validated candidate before public publication; integrating those commands
into the frozen hosted harness requires an explicitly approved harness
change. The manifest is the authoritative per-file size and SHA-256
inventory; the application also validates it at non-Debug startup.

## Pre-publication evidence

The release operator must retain, keyed to the exact commit and bundle hashes:

1. Release tests and migration behavior-gate receipts.
2. All hosted matrix lane manifests, screenshots, traces, cleanup records, and
   certified NVIDIA review receipts.
3. Two complete four-machine physical acceptance cycles, including degraded
   RSS, AI, network, time, and YFinance behavior.
4. Trusted signing output and the public certificate/fingerprint needed by
   users to verify each platform bundle.
5. Release notes containing attribution, license terms, platform download
   instructions, known limitations, and the exact manifest/checksum files.

Missing, stale, unsigned, or hash-mismatched evidence is a release blocker.
GitHub branch-protection rules are not a release prerequisite; the exact
candidate commit, review receipts, local pre-push gates, and published hashes
are the authoritative integrity chain.
