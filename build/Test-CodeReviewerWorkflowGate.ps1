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
param([switch]$SelfTest)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$required = @(
    (Join-Path $PSScriptRoot 'Invoke-CodeReviewHarness.ps1'),
    (Join-Path $PSScriptRoot 'Run-CodeReview.ps1'),
    (Join-Path $PSScriptRoot 'Invoke-ReviewGate.ps1')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing configured review entry point: $path"
    }
}

$adapter = Get-Content -LiteralPath $required[0] -Raw
$runner = Get-Content -LiteralPath $required[1] -Raw
if ($adapter -match '(?i)deepseek|nvidia|openrouter|api[_-]?key' -or
    $runner -match '(?i)deepseek|nvidia|openrouter|api[_-]?key') {
    throw 'Provider-specific coupling leaked into the generic review entry points.'
}
if ($adapter -notmatch 'DNPPV_REVIEW_ENGINE' -or
    $adapter -notmatch 'ReviewType' -or
    $adapter -notmatch 'LASTEXITCODE') {
    throw 'Generic review adapter does not preserve configuration and semantic exit behavior.'
}

if ($SelfTest) {
    Write-Output 'CODE_REVIEWER_WORKFLOW_GATE_SELFTEST=Passed'
    exit 0
}

Write-Output 'CODE_REVIEWER_WORKFLOW_GATE=Passed'
