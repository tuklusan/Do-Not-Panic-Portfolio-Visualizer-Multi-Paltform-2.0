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
    Write-Output 'CODE_REVIEW_RECEIPT_SELFTEST=Passed'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
