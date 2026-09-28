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
param(
    [ValidateSet('Primary','Fallback')][string]$ModelRole = 'Primary',
    [ValidateRange(60,14400)][int]$TimeoutSeconds = 1800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'DNPPV review health gate requires PowerShell 7 or later.' }
$repoRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..'))
$result = & ([IO.Path]::Combine($PSScriptRoot,'Invoke-CodeReviewHarness.ps1')) -Operation Health -RepositoryRoot $repoRoot -HealthModelRole $ModelRole -ReviewTimeoutSeconds $TimeoutSeconds
if ($null -eq $result -or $result.success -ne $true -or $result.stage -cne 'Health' -or
    $result.provider_post_attempt_delta -ne 1) {
    throw 'NVIDIA adapter health result is missing or invalid.'
}
Write-Output "NVIDIA_WORKFLOW_GATE=Passed;MODEL_ROLE=$ModelRole"
