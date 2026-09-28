# Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
# Proprietary rights reserved except as expressly licensed herein.
# Based on original work by Supratim Sanyal of SANYALnet Labs.
#
# DO NOT PANIC PORTFOLIO VISUALIZER
# This file is governed by the SANYALnet Labs Non-Commercial License in the
# root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
# for AI/ML model training are prohibited unless separately authorized.
#
# Attribution is required: "Based on original work by Supratim Sanyal of
# SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
# patent, trademark, and governing-law provisions.

[CmdletBinding()]
param([switch]$SelfTest)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-NoSecretInTree([string]$Root, [string]$Secret) {
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -Recurse -File)) {
        $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($file.FullName))
        if ($text.Contains($Secret)) { throw "Synthetic secret survived in upload tree: $($file.FullName)" }
    }
}

function Invoke-QuarantineCase([string]$Payload, [string]$Secret) {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('dnppv2-quarantine-test-' + [Guid]::NewGuid().ToString('N'))
    $uploadRoot = Join-Path $root 'artifacts/soak/lane'
    $reviewRoot = Join-Path $uploadRoot 'review'
    $runnerTemp = Join-Path $root 'runner-temp'
    $helper = Join-Path $PSScriptRoot 'Invoke-ReviewerEvidenceQuarantine.ps1'
    try {
        New-Item -ItemType Directory -Force -Path (Join-Path $reviewRoot 'nested') | Out-Null
        New-Item -ItemType Directory -Force -Path $runnerTemp | Out-Null
        Set-Content -LiteralPath (Join-Path $reviewRoot 'nested/telemetry.log') -Value $Payload -Encoding utf8
        try {
            & $helper -ReviewRoot $reviewRoot -ArtifactRoot $uploadRoot -RunnerTemp $runnerTemp -RunId 'self-test-run' -CommitSha ('a' * 40) -Runner 'self-test' -Rid 'self-test-rid' -Reason 'synthetic contamination'
            throw 'Shared quarantine helper unexpectedly returned successfully.'
        } catch {
            if ($_.Exception.Message -notmatch 'Reviewer evidence quarantined') { throw }
        }
        Assert-NoSecretInTree $uploadRoot $Secret
        $receipt = Get-Content -LiteralPath (Join-Path $reviewRoot 'review-evidence-failure.json') -Raw
        if ($receipt.Contains($Secret) -or $receipt -match '(?i)Bearer\s+TESTSECRET') { throw 'Sanitized failure receipt contains secret material.' }
        if (Test-Path -LiteralPath (Join-Path $reviewRoot 'nested/telemetry.log')) { throw 'Original contaminated review file still exists in upload tree.' }
        $quarantineDirectories = @(Get-ChildItem -LiteralPath $runnerTemp -Directory -Filter 'dnppv2-review-quarantine-*')
        if ($quarantineDirectories.Count -ne 1) { throw 'Shared quarantine destination is missing or ambiguous.' }
        if (([IO.Path]::GetFullPath($quarantineDirectories[0].FullName)).StartsWith([IO.Path]::GetFullPath($uploadRoot), [StringComparison]::OrdinalIgnoreCase)) { throw 'Quarantine destination is inside upload root.' }
    }
    finally {
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($SelfTest) {
    Invoke-QuarantineCase 'TEST_OPENROUTER_SECRET_123' 'TEST_OPENROUTER_SECRET_123'
    Invoke-QuarantineCase 'Authorization: Bearer TESTSECRET' 'TESTSECRET'
    Invoke-QuarantineCase 'nested telemetry password=TEST_NVIDIA_SECRET_456' 'TEST_NVIDIA_SECRET_456'
    'HOSTED_SOAK_QUARANTINE_SELFTEST=Passed'
    exit 0
}

throw 'Use -SelfTest; this script is a deterministic security regression test.'
