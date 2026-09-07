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
param([Parameter()][switch]$SelfTest)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $SelfTest) { throw 'Use -SelfTest for the local companion dispatch contract test.' }
$root = Join-Path ([IO.Path]::GetTempPath()) ('dnppv2-companion-test-' + [Guid]::NewGuid().ToString('N'))
try {
    $path = Join-Path $root 'dispatch.json'
    & (Join-Path $PSScriptRoot 'New-LocalCompanionDispatch.ps1') -RunId 'test-run' -CommitSha ('a' * 40) -OutputPath $path | Out-Null
    $m = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($m.schema -ne 'dnppv2-local-companion-dispatch/v1' -or $m.durationMinutes -ne 10 -or
        $m.hostedLaneCount -ne 20 -or $m.credentialsIncluded -ne $false -or
        @($m.requiredMachines).Count -ne 4 -or $m.availabilityRequiredAtCycleStart -ne $true) {
        throw 'Local companion dispatch manifest contract failed.'
    }
    if ((Get-Content -LiteralPath $path -Raw) -match '(?i)(password|api[_-]?key|authorization|bearer)') {
        throw 'Local companion dispatch manifest contains a credential-shaped field.'
    }
    Write-Output 'LOCAL_COMPANION_DISPATCH_SELFTEST=Passed'
}
finally { if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force } }
