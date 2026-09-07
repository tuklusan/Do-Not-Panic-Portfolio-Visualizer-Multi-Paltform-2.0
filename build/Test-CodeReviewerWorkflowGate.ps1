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
    (Join-Path $PSScriptRoot 'Invoke-ReviewGate.ps1'),
    (Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/FRESH-PROJECT-CODE-REVIEW-GATE.md')
)
foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing configured review entry point: $path"
    }
}

$adapter = Get-Content -LiteralPath $required[0] -Raw
$runner = Get-Content -LiteralPath $required[1] -Raw
$standard = Get-Content -LiteralPath $required[3] -Raw
if ($adapter -match '(?i)deepseek|nvidia|openrouter' -or
    $runner -match '(?i)deepseek|nvidia|openrouter') {
    throw 'Provider-specific coupling leaked into the generic review entry points.'
}
if ($adapter -notmatch 'DNPPV_REVIEW_ENGINE' -or
    $adapter -notmatch 'ReviewType' -or
    $adapter -notmatch 'LASTEXITCODE' -or
    $adapter -notmatch 'CODE_REVIEWER_ENDPOINT' -or
    $adapter -notmatch 'CODE_REVIEWER_MODEL' -or
    $adapter -notmatch 'CODE_REVIEWER_API_KEY' -or
    $adapter -notmatch 'CODE_REVIEWER_REQUEST_OVERRIDES_JSON' -or
    $adapter -notmatch 'blocking_findings') {
    throw 'Generic review adapter does not preserve configuration and semantic exit behavior.'
}
foreach ($standardToken in @('CODE_REVIEWER_ENDPOINT', 'CODE_REVIEWER_MODEL', 'CODE_REVIEWER_API_KEY', 'REVIEW_UNAVAILABLE', 'blocking_findings', 'fail closed')) {
    if ($standard -notmatch [regex]::Escape($standardToken)) { throw "Generic review standard is missing contract token: $standardToken" }
}

if ($SelfTest) {
    $probeRoot = Join-Path ([IO.Path]::GetTempPath()) ('dnppv2-generic-review-probe-' + [guid]::NewGuid().ToString('N'))
    $probeEngine = Join-Path $probeRoot 'engine.ps1'
    New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null
    try {
        @'
param([string]$ReviewType, [string]$ReviewMaterialPath, [string]$OutputDirectory, [int]$RequestTimeoutSeconds)
if ($env:CODE_REVIEWER_ENDPOINT -ne 'https://review.example.test' -or
    $env:CODE_REVIEWER_MODEL -ne 'model/test' -or
    $env:CODE_REVIEWER_API_KEY -ne 'probe-secret' -or
    $env:CODE_REVIEWER_REQUEST_OVERRIDES_JSON -ne '{"stream":false}') { exit 31 }
Write-Output '{"verdict":"PASS","review_complete":true,"blocking_findings":[]}'
'@ | Set-Content -LiteralPath $probeEngine -Encoding utf8
        $env:DNPPV_REVIEW_ENGINE = $probeEngine
        $probeOutput = @(& (Join-Path $PSScriptRoot 'Invoke-CodeReviewHarness.ps1') -ReviewType CODE -ReviewMaterialPath $probeEngine -Endpoint 'https://review.example.test' -Model 'model/test' -ApiKey 'probe-secret' -RequestOverridesJson '{"stream":false}')
        if (($probeOutput | Out-String) -notmatch '"verdict":"PASS"') { throw 'Generic adapter configuration/result self-test failed.' }
    }
    finally {
        Remove-Item -LiteralPath $probeRoot -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item Env:DNPPV_REVIEW_ENGINE -ErrorAction SilentlyContinue
    }
    Write-Output 'CODE_REVIEWER_WORKFLOW_GATE_SELFTEST=Passed'
    exit 0
}

Write-Output 'CODE_REVIEWER_WORKFLOW_GATE=Passed'
