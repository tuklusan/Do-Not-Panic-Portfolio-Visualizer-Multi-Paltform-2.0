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
    [int]$RequestTimeoutSeconds = 1800,

    [string]$Endpoint = [Environment]::GetEnvironmentVariable('CODE_REVIEWER_ENDPOINT'),
    [string]$Model = [Environment]::GetEnvironmentVariable('CODE_REVIEWER_MODEL'),
    [string]$ApiKey = [Environment]::GetEnvironmentVariable('CODE_REVIEWER_API_KEY'),
    [string]$RequestOverridesJson = [Environment]::GetEnvironmentVariable('CODE_REVIEWER_REQUEST_OVERRIDES_JSON')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$configuredEngine = [Environment]::GetEnvironmentVariable('DNPPV_REVIEW_ENGINE')
$enginePath = if ([string]::IsNullOrWhiteSpace($configuredEngine)) {
    Join-Path $PSScriptRoot 'Invoke-ReviewGate.ps1'
}
elseif ([IO.Path]::IsPathRooted($configuredEngine)) {
    $configuredEngine
}
else {
    Join-Path $repoRoot $configuredEngine
}

if (-not (Test-Path -LiteralPath $enginePath -PathType Leaf)) {
    throw "Configured review engine does not exist: $enginePath"
}

if (-not [string]::IsNullOrWhiteSpace($RequestOverridesJson)) {
    try {
        $overrides = $RequestOverridesJson | ConvertFrom-Json
        if ($null -eq $overrides -or $overrides -is [array] -or $overrides -isnot [pscustomobject]) {
            throw 'must be a JSON object'
        }
        foreach ($protectedField in @('model', 'messages', 'response_format', 'stream')) {
            if ($overrides.PSObject.Properties.Name -contains $protectedField -and $null -eq $overrides.$protectedField) {
                throw "protected field '$protectedField' cannot be null"
            }
        }
    }
    catch {
        throw "CODE_REVIEWER_REQUEST_OVERRIDES_JSON is invalid: $($_.Exception.Message)"
    }
}

$previousEndpoint = $env:CODE_REVIEWER_ENDPOINT
$previousModel = $env:CODE_REVIEWER_MODEL
$previousApiKey = $env:CODE_REVIEWER_API_KEY
$previousOverrides = $env:CODE_REVIEWER_REQUEST_OVERRIDES_JSON
try {
    if ($null -ne $Endpoint) { $env:CODE_REVIEWER_ENDPOINT = $Endpoint }
    if ($null -ne $Model) { $env:CODE_REVIEWER_MODEL = $Model }
    if ($null -ne $ApiKey) { $env:CODE_REVIEWER_API_KEY = $ApiKey }
    if ($null -ne $RequestOverridesJson) { $env:CODE_REVIEWER_REQUEST_OVERRIDES_JSON = $RequestOverridesJson }

    $engineOutput = @(& $enginePath -ReviewType $ReviewType -ReviewMaterialPath $ReviewMaterialPath -OutputDirectory $OutputDirectory -RequestTimeoutSeconds $RequestTimeoutSeconds 2>&1)
    $engineExitCode = if (Get-Variable -Name LASTEXITCODE -ErrorAction SilentlyContinue) { [int]$LASTEXITCODE } else { 0 }
    if ($engineExitCode -ne 0) { exit $engineExitCode }
    $result = $null
    foreach ($line in (($engineOutput | Out-String) -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -Last 30)) {
        try {
            $candidate = $line.Trim() | ConvertFrom-Json
            if ($candidate.PSObject.Properties.Name -contains 'verdict') { $result = $candidate }
        }
        catch { }
    }
    if ($null -eq $result) { throw 'Configured reviewer returned no semantic JSON result.' }
    if ($result.verdict -notin @('PASS', 'FAIL', 'INCONCLUSIVE', 'REVIEW_UNAVAILABLE')) {
        throw "Configured reviewer returned unsupported verdict '$($result.verdict)'."
    }
    if ($result.review_complete -isnot [bool]) { throw 'Configured reviewer returned a non-boolean review_complete.' }
    if ($result.PSObject.Properties.Name -notcontains 'blocking_findings' -or $result.blocking_findings -is [string]) {
        throw 'Configured reviewer returned no structured blocking_findings array.'
    }
    $result | ConvertTo-Json -Depth 30 -Compress
}
finally {
    $env:CODE_REVIEWER_ENDPOINT = $previousEndpoint
    $env:CODE_REVIEWER_MODEL = $previousModel
    $env:CODE_REVIEWER_API_KEY = $previousApiKey
    $env:CODE_REVIEWER_REQUEST_OVERRIDES_JSON = $previousOverrides
}
