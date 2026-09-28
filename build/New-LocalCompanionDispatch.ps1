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
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$RunId,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$CommitSha,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$OutputPath,
    [Parameter()][ValidateSet(10)][int]$DurationMinutes = 10,
    [Parameter()][ValidateSet(20)][int]$HostedLaneCount = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$manifest = [ordered]@{
    schema = 'dnppv2-local-companion-dispatch/v1'
    runId = $RunId
    commitSha = $CommitSha
    companionCycleId = "dnppv2-local-cycle-$RunId"
    durationMinutes = $DurationMinutes
    hostedLaneCount = $HostedLaneCount
    requiredMachines = @('linux-x64-lxqt', 'windows-10-reference', 'windows-11-laptop', 'macos-x64-intel-big-sur')
    localCoordinator = 'build/Invoke-LocalLabSoakCycle.ps1'
    availabilityRequiredAtCycleStart = $true
    credentialsIncluded = $false
    status = 'requested'
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $output -Encoding utf8
Write-Output "LOCAL_COMPANION_DISPATCH=Recorded;RUN=$RunId;DURATION=$DurationMinutes;OUTPUT=$output"
