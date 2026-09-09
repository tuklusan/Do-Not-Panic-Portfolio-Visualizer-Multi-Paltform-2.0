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
[CmdletBinding(DefaultParameterSetName = 'Review')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Review')][ValidateSet('CODE', 'DOCUMENTATION', 'TEST_ARTIFACT')][string]$ReviewType,
    [Parameter(Mandatory = $true, ParameterSetName = 'Review')][string]$ReviewMaterialPath,
    [Parameter(ParameterSetName = 'Review')][string]$OutputDirectory = 'build/dnppv2-nvidia-review',
    [ValidateRange(60, 14400)][int]$RequestTimeoutSeconds = 1800,
    [Parameter(Mandatory = $true, ParameterSetName = 'HealthCheck')][switch]$HealthCheck,
    [Parameter(Mandatory = $true, ParameterSetName = 'SelfTest')][switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$primary = 'nvidia/nemotron-3-super-120b-a12b'
$fallback = 'nvidia/nemotron-3.5-lightning-30b-a3b'
$reviewerIdentity = 'dnppv2-nvidia-review-gate-v1'
$configuredReviewerIdentity = [Environment]::GetEnvironmentVariable('DNPPV_REVIEWER_ID')
if (-not [string]::IsNullOrWhiteSpace($configuredReviewerIdentity) -and $configuredReviewerIdentity -ne $reviewerIdentity) {
    throw "Reviewer identity mismatch: expected '$reviewerIdentity', received '$configuredReviewerIdentity'."
}
$bootstrap = Join-Path $PSScriptRoot 'Invoke-BootstrapReviewer.ps1'

if (-not (Test-Path -LiteralPath $bootstrap -PathType Leaf)) { throw "Bootstrap reviewer is missing: $bootstrap" }

function Get-ValidatedResultText([object[]]$Output) {
    $lines = @(($Output | Out-String) -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($lines.Count -eq 0) { throw 'Reviewer returned no output.' }
    foreach ($line in $lines[($lines.Count - 1)..0]) {
        try {
            $candidate = $line | ConvertFrom-Json
            if ($candidate.verdict -in @('PASS', 'FAIL', 'INCONCLUSIVE', 'REVIEW_UNAVAILABLE', 'HUMAN_DECISION_REQUIRED')) {
                return $line
            }
        }
        catch {
            continue
        }
    }
    throw 'Reviewer returned no supported semantic JSON result.'
}

if ($SelfTest) {
    if ($primary -eq $fallback -or $primary -notmatch '^nvidia/' -or $fallback -notmatch '^nvidia/' -or $reviewerIdentity -ne 'dnppv2-nvidia-review-gate-v1') { throw 'Authorized reviewer policy is invalid.' }
    $parserProbe = Get-ValidatedResultText @('diagnostic output', '{"verdict":"PASS"}')
    if ($parserProbe -ne '{"verdict":"PASS"}') { throw 'Reviewer result parser self-test failed.' }
    Write-Output 'REVIEW_GATE_SELFTEST=Passed'
    exit 0
}

if ($HealthCheck) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('NVIDIA_API_KEY_CODING'))) { throw 'NVIDIA_API_KEY_CODING is required for reviewer health-check.' }
    foreach ($model in @($primary, $fallback)) {
        & $bootstrap -HealthCheck -Model $model -RequestTimeoutSeconds $RequestTimeoutSeconds | Out-Null
    }
    Write-Output 'REVIEW_GATE_HEALTHCHECK=Passed'
    exit 0
}

if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('NVIDIA_API_KEY_CODING'))) {
    throw 'NVIDIA_API_KEY_CODING is required for the reviewer gate.'
}

$primaryError = $null
try {
    $primaryOutput = @(& $bootstrap -ReviewType $ReviewType -ReviewMaterialPath $ReviewMaterialPath -Model $primary -OutputDirectory $OutputDirectory -RequestTimeoutSeconds $RequestTimeoutSeconds 2>&1)
    $primaryText = Get-ValidatedResultText $primaryOutput
    $primaryText
    exit 0
}
catch {
    $primaryError = $_.Exception.Message
}

try {
    $fallbackOutput = @(& $bootstrap -ReviewType $ReviewType -ReviewMaterialPath $ReviewMaterialPath -Model $fallback -OutputDirectory $OutputDirectory -RequestTimeoutSeconds $RequestTimeoutSeconds 2>&1)
    $fallbackText = Get-ValidatedResultText $fallbackOutput
    $fallbackText
    exit 0
}
catch {
    throw "Review gate unavailable after primary and fallback attempts. Primary: $primaryError; fallback: $($_.Exception.Message)"
}
