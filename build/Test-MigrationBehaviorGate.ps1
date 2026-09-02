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
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^CR-[0-9]{3}[A-Z]?$')]
    [string]$CrId,

    [Parameter(Mandatory = $true)]
    [ValidateSet('PreDevelopment', 'Closure')]
    [string]$Stage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$trackerPath = Join-Path $repoRoot 'docs/AUDIT_STATE.json'
$tracker = Get-Content -LiteralPath $trackerPath -Raw | ConvertFrom-Json
$cr = @($tracker.change_requests | Where-Object { $_.id -eq $CrId })
if ($cr.Count -ne 1) {
    throw "Migration behavior gate requires exactly one tracker entry for $CrId; found $($cr.Count)."
}

$inventory = $cr[0].upstream_behavior_inventory
if ($null -eq $inventory -or $inventory.status -ne 'complete') {
    throw "$CrId pre-development gate requires upstream_behavior_inventory.status=complete."
}
if ([string]::IsNullOrWhiteSpace([string]$inventory.document) -or
    [string]::IsNullOrWhiteSpace([string]$inventory.upstream_commit) -or
    @($inventory.source_files).Count -eq 0 -or
    $inventory.zero_known_gaps -ne $true) {
    throw "$CrId pre-development inventory is missing its document, upstream commit, source files, or zero-gap result."
}

$inventoryPath = Join-Path $repoRoot ([string]$inventory.document)
if (-not (Test-Path -LiteralPath $inventoryPath -PathType Leaf)) {
    throw "$CrId inventory document does not exist: $inventoryPath"
}

$inventoryDocument = Get-Content -LiteralPath $inventoryPath -Raw
$inventorySectionPattern = if ($inventory.document -eq 'docs/COMPLETED-CR-UPSTREAM-GATE-RETROFIT.md') {
    "(?m)^##\s+$([regex]::Escape($CrId))\b"
}
else {
    '(?m)^##\s+Functional Inventory\s*$'
}
$inventorySectionMatch = [regex]::Match($inventoryDocument, $inventorySectionPattern)
if (-not $inventorySectionMatch.Success) {
    throw "$CrId inventory document does not contain its dedicated functional-inventory section."
}

$inventorySectionRemainder = $inventoryDocument.Substring($inventorySectionMatch.Index)
$nextSectionMatch = [regex]::Match(
    $inventorySectionRemainder.Substring($inventorySectionMatch.Length),
    '(?m)^##\s+')
$inventorySection = if ($nextSectionMatch.Success) {
    $inventorySectionRemainder.Substring(
        0,
        $inventorySectionMatch.Length + $nextSectionMatch.Index)
}
else {
    $inventorySectionRemainder
}

$inventoryItemPattern = if ($inventory.document -eq 'docs/COMPLETED-CR-UPSTREAM-GATE-RETROFIT.md') {
    '(?m)^Inventory:'
}
else {
    '(?m)^\|\s*[A-Z]{2,3}-[0-9]{2}\s*\|'
}
if ($inventorySection -notmatch $inventoryItemPattern) {
    throw "$CrId inventory document does not list functional behavior items."
}

if ($Stage -eq 'PreDevelopment') {
    Write-Output "MIGRATION_BEHAVIOR_GATE=Passed;CR=$CrId;STAGE=$Stage;UPSTREAM=$($inventory.upstream_commit)"
    return
}

$audit = $cr[0].upstream_closure_audit
if ($null -eq $audit -or $audit.status -ne 'complete' -or
    [string]::IsNullOrWhiteSpace([string]$audit.upstream_commit) -or
    @($audit.source_files_rescanned).Count -eq 0 -or
    $audit.zero_unmapped_behaviors -ne $true -or
    [int]$audit.successive_zero_gap_scans -lt 2 -or
    @($audit.unresolved_gaps).Count -ne 0) {
    throw "$CrId closure gate requires a complete fresh upstream audit, zero unmapped behaviors, no unresolved gaps, and at least two successive zero-gap scans."
}

Write-Output "MIGRATION_BEHAVIOR_GATE=Passed;CR=$CrId;STAGE=$Stage;UPSTREAM=$($audit.upstream_commit);ZERO_GAP_SCANS=$($audit.successive_zero_gap_scans)"
