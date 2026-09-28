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
    [Parameter()][string]$DispatchPath,
    [Parameter()][string]$CyclePath,
    [Parameter()][switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-Manifest([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Manifest is missing: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Assert-CompanionReceipt([object]$Dispatch, [object]$Cycle) {
    if ($Dispatch.schema -ne 'dnppv2-local-companion-dispatch/v1') { throw 'Unexpected dispatch schema.' }
    if ($Cycle.schema -ne 'dnppv2-local-lab-cycle/v1') { throw 'Unexpected local cycle schema.' }
    if ([string]::IsNullOrWhiteSpace([string]$Dispatch.runId) -or
        [string]$Dispatch.commitSha -notmatch '^[0-9a-fA-F]{40}$') { throw 'Dispatch identity is invalid.' }
    if ([int]$Dispatch.durationMinutes -ne [int]$Cycle.durationMinutes) { throw 'Duration mismatch.' }
    if ([string]$Cycle.cycleId -ne [string]$Dispatch.companionCycleId) { throw 'Companion cycle identity mismatch.' }
    if ([int]$Dispatch.hostedLaneCount -ne 20 -or $Dispatch.credentialsIncluded -ne $false) {
        throw 'Dispatch contract is invalid.'
    }

    $required = @($Dispatch.requiredMachines | ForEach-Object { [string]$_ })
    if ($required.Count -ne 4 -or @($required | Sort-Object -Unique).Count -ne 4) { throw 'Dispatch machine set is invalid.' }
    $machines = @($Cycle.machines)
    $names = @($machines | ForEach-Object { [string]$_.name })
    foreach ($name in $required) {
        $matches = @($machines | Where-Object { $_.name -eq $name })
        if ($matches.Count -ne 1) { throw "Local cycle is missing exactly one result for $name." }
        if ([string]$matches[0].status -notin @('Passed', 'UnavailableAtCycleStart')) {
            throw "Local cycle has a non-terminal result for ${name}: $($matches[0].status)"
        }
    }
    if (@($machines | Where-Object { $_.status -eq 'Passed' }).Count -eq 0) { throw 'No available local machine passed.' }
    if ($Dispatch.credentialsIncluded -ne $false) { throw 'Credentials are not permitted.' }
}

if ($SelfTest) {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('dnppv2-companion-receipt-test-' + [Guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $dispatchPath = Join-Path $root 'dispatch.json'
        $cyclePath = Join-Path $root 'cycle.json'
        & (Join-Path $PSScriptRoot 'New-LocalCompanionDispatch.ps1') -RunId 'test-run' -CommitSha ('a' * 40) -OutputPath $dispatchPath | Out-Null
        $machines = @('linux-x64-lxqt', 'windows-10-reference', 'windows-11-laptop', 'macos-x64-intel-big-sur') | ForEach-Object {
            [ordered]@{ name = $_; status = 'Passed' }
        }
        [ordered]@{ schema = 'dnppv2-local-lab-cycle/v1'; cycleId = 'dnppv2-local-cycle-test-run'; durationMinutes = 10; machines = $machines } |
            ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $cyclePath -Encoding utf8
        Assert-CompanionReceipt (Read-Manifest $dispatchPath) (Read-Manifest $cyclePath)
        $cycle = Read-Manifest $cyclePath
        $cycle.cycleId = 'wrong-cycle'
        try { Assert-CompanionReceipt (Read-Manifest $dispatchPath) $cycle; throw 'Mismatched cycle was accepted.' } catch { if ($_.Exception.Message -notmatch 'identity mismatch') { throw } }
        Write-Output 'LOCAL_COMPANION_RECEIPT_SELFTEST=Passed'
    }
    finally { if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force } }
    exit 0
}

if ([string]::IsNullOrWhiteSpace($DispatchPath) -or [string]::IsNullOrWhiteSpace($CyclePath)) {
    throw 'DispatchPath and CyclePath are required unless -SelfTest is used.'
}
Assert-CompanionReceipt (Read-Manifest $DispatchPath) (Read-Manifest $CyclePath)
Write-Output 'LOCAL_COMPANION_RECEIPT=Passed'
