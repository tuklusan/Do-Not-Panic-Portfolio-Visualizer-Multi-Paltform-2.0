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
    [ValidateSet('CODE', 'DOCUMENTATION', 'TEST_ARTIFACT')]
    [string]$ReviewType,

    [Parameter(Mandatory = $true)]
    [string]$ReviewMaterialPath,

    [string]$OutputDirectory = 'build/code-review',
    [ValidateRange(60, 14400)]
    [int]$RequestTimeoutSeconds = 1800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'Invoke-CodeReviewHarness.ps1') @PSBoundParameters
if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
