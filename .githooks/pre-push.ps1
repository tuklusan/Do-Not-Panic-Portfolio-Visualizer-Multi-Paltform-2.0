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
    [string]$RemoteName,
    [string]$RemoteUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    throw 'Could not resolve the repository root for the DNPPV pre-push hook.'
}

$upstreamGuard = Join-Path $repoRoot 'build\Assert-NoUpstreamMutation.ps1'
$licenseGate = Join-Path $repoRoot 'build\Test-LicenseHeaders.ps1'
$syntaxGate = Join-Path $repoRoot 'build\Test-PowerShellSyntax.ps1'

foreach ($requiredPath in @($upstreamGuard, $licenseGate, $syntaxGate)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Missing required pre-push gate: $requiredPath"
    }
}

& $upstreamGuard -RemoteName $RemoteName -RemoteUrl $RemoteUrl
& $licenseGate
& $syntaxGate

Write-Output 'PRE_PUSH_GATES=Passed'
