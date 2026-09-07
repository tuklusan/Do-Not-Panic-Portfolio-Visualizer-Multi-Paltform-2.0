# ============================================================================
# Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
# Proprietary rights reserved except as expressly licensed herein.
#
# DO NOT PANIC PORTFOLIO VISUALIZER
# This file is governed by the SANYALnet Labs Non-Commercial License in the
# root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
# for AI/ML model training are prohibited unless separately authorized.
#
# Attribution is required: "Based on original work by Supratim Sanyal of
# SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
# patent, trademark, and governing-law provisions.
# ============================================================================
[CmdletBinding()]
param([string]$BaseRef)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) { throw 'Could not resolve repository root.' }
$manifestPath = Join-Path $repoRoot 'docs\TEST_HARNESS_MANIFEST.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Missing harness manifest: $manifestPath" }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifestPaths = @($manifest.tracked_harnesses | ForEach-Object { [string]$_ })
if ($manifestPaths.Count -eq 0) { throw 'Harness manifest is empty.' }
foreach ($relativePath in $manifestPaths) {
    $absolutePath = Join-Path $repoRoot ($relativePath -replace '/', '\')
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) { throw "Manifest harness is missing: $relativePath" }
    & git ls-files --error-unmatch -- "$relativePath" *> $null
    if ($LASTEXITCODE -ne 0) { throw "Manifest harness is not tracked: $relativePath" }
}
$approval = [Environment]::GetEnvironmentVariable('DNPPV_HARNESS_CHANGE_APPROVED')
$approved = $approval -match '^(?i:Y|1)$'
$changed = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
if ($BaseRef) {
    foreach ($path in @( & git diff --name-only "$BaseRef..HEAD" )) { [void]$changed.Add(([string]$path).Replace('\', '/')) }
}
foreach ($path in @( & git diff --name-only; & git diff --cached --name-only )) { [void]$changed.Add(([string]$path).Replace('\', '/')) }
$harnessChanges = @($changed | Where-Object { $manifestPaths -contains $_ })
if ($harnessChanges.Count -gt 0 -and -not $approved) {
    throw "HARNESS_FREEZE=Failed; approval required for: $($harnessChanges -join ', '). Set DNPPV_HARNESS_CHANGE_APPROVED=Y or 1 only with operator authorization."
}
Write-Output "HARNESS_FREEZE=Passed;TRACKED=$($manifestPaths.Count);CHANGED=$($harnessChanges.Count);APPROVED=$approved"
