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
    [Parameter(Mandatory = $true)][ValidateSet('CODE', 'DOCUMENTATION', 'TEST_ARTIFACT')][string]$ReviewType,
    [Parameter(Mandatory = $true)][string]$ReviewMaterialPath,
    [string]$OutputDirectory = 'build/nvidia-review',
    [int]$RequestTimeoutSeconds = 900,
    [string]$Model = 'nvidia/nemotron-3-super-120b-a12b'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$legacyPath = Join-Path $PSScriptRoot 'Invoke-NvidiaReviewHarness.ps1'
if (-not (Test-Path -LiteralPath $legacyPath -PathType Leaf)) {
    throw "Bootstrap reviewer engine is missing: $legacyPath"
}

& $legacyPath -ReviewType $ReviewType -ReviewMaterialPath $ReviewMaterialPath -Model $Model -OutputDirectory $OutputDirectory -RequestTimeoutSeconds $RequestTimeoutSeconds
