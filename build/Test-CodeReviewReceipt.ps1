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
if (-not $SelfTest) { throw 'Use -SelfTest.' }
$root = Join-Path $env:TEMP ("dnppv2-receipt-selftest-{0}" -f $PID)
New-Item -ItemType Directory -Force -Path $root | Out-Null
try {
    function Invoke-Git([string]$WorkingDirectory, [string[]]$Arguments) {
        $output = & git -C $WorkingDirectory @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) { throw "git failed: $($Arguments -join ' '): $output" }
        return (($output | Out-String).Trim())
    }

    $material = Join-Path $root 'material.txt'
    $result = Join-Path $root 'result.json'
    $receipt = Join-Path $root 'receipt.json'
    'reviewed committed candidate' | Set-Content -LiteralPath $material -Encoding utf8NoBOM
    '{"schema":"dnppv2-test-artifact-review-result/v2","review_complete":true,"verdict":"PASS","blocking_findings":[]}' | Set-Content -LiteralPath $result -Encoding utf8NoBOM
    $base = (& git rev-parse HEAD^).Trim()
    $head = (& git rev-parse HEAD).Trim()
    & (Join-Path $PSScriptRoot 'Assert-CodeReviewReceipt.ps1') -Create -ReceiptPath $receipt -BaseSha $base -NewSha $head -Scope 'CR-103' -MaterialPath $material -ResultPath $result
    & (Join-Path $PSScriptRoot 'Assert-CodeReviewReceipt.ps1') -ReceiptPath $receipt -RemoteOldSha $base -NewSha $head
    try {
        & (Join-Path $PSScriptRoot 'Assert-CodeReviewReceipt.ps1') -ReceiptPath $receipt -RemoteOldSha $head -NewSha $head
        throw 'Stale remote-base negative case was accepted.'
    }
    catch {
        if ($_.Exception.Message -notmatch 'remote old value') { throw }
    }

    $repo = Join-Path $root 'temporary-repository'
    New-Item -ItemType Directory -Force -Path $repo | Out-Null
    Invoke-Git $repo @('init', '--quiet') | Out-Null
    Invoke-Git $repo @('config', 'user.email', 'receipt-selftest@example.invalid') | Out-Null
    Invoke-Git $repo @('config', 'user.name', 'Receipt Self-Test') | Out-Null
    'A' | Set-Content -LiteralPath (Join-Path $repo 'state.txt') -Encoding utf8NoBOM
    Invoke-Git $repo @('add', 'state.txt') | Out-Null
    Invoke-Git $repo @('commit', '--quiet', '-m', 'A') | Out-Null
    $a = Invoke-Git $repo @('rev-parse', 'HEAD')
    'B' | Set-Content -LiteralPath (Join-Path $repo 'state.txt') -Encoding utf8NoBOM
    Invoke-Git $repo @('commit', '--quiet', '-am', 'B') | Out-Null
    $b = Invoke-Git $repo @('rev-parse', 'HEAD')
    'C' | Set-Content -LiteralPath (Join-Path $repo 'state.txt') -Encoding utf8NoBOM
    Invoke-Git $repo @('commit', '--quiet', '-am', 'C') | Out-Null
    $c = Invoke-Git $repo @('rev-parse', 'HEAD')
    'D' | Set-Content -LiteralPath (Join-Path $repo 'state.txt') -Encoding utf8NoBOM
    Invoke-Git $repo @('commit', '--quiet', '-am', 'D') | Out-Null
    $d = Invoke-Git $repo @('rev-parse', 'HEAD')
    $temporaryMaterial = Join-Path $root 'temporary-material.txt'
    $temporaryResult = Join-Path $root 'temporary-result.json'
    $temporaryReceipt = Join-Path $root 'temporary-receipt.json'
    'A-to-B reviewed candidate' | Set-Content -LiteralPath $temporaryMaterial -Encoding utf8NoBOM
    '{"schema":"dnppv2-test-artifact-review-result/v2","review_complete":true,"verdict":"PASS","blocking_findings":[]}' | Set-Content -LiteralPath $temporaryResult -Encoding utf8NoBOM
    Push-Location $repo
    try {
        & (Join-Path $PSScriptRoot 'Assert-CodeReviewReceipt.ps1') -Create -ReceiptPath $temporaryReceipt -BaseSha $a -NewSha $b -Scope 'CR-103' -MaterialPath $temporaryMaterial -ResultPath $temporaryResult
        & (Join-Path $PSScriptRoot 'Assert-CodeReviewReceipt.ps1') -ReceiptPath $temporaryReceipt -RemoteOldSha $a -NewSha $b
        foreach ($case in @(
            @{ RemoteOldSha = $c; NewSha = $b; Name = 'remote movement' },
            @{ RemoteOldSha = $a; NewSha = $c; Name = 'new head' },
            @{ RemoteOldSha = $a; NewSha = $d; Name = 'skipped reviewed range' }
        )) {
            try {
                & (Join-Path $PSScriptRoot 'Assert-CodeReviewReceipt.ps1') -ReceiptPath $temporaryReceipt -RemoteOldSha $case.RemoteOldSha -NewSha $case.NewSha
                throw "Temporary Git negative case was accepted: $($case.Name)."
            }
            catch {
                if ($_.Exception.Message -match 'Temporary Git negative case was accepted') { throw }
            }
        }
    }
    finally { Pop-Location }
    Write-Output 'CODE_REVIEW_RECEIPT_SELFTEST=Passed'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
