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
    [Parameter(Mandatory)][string]$ReceiptPath,
    [switch]$Create,
    [string]$BaseSha,
    [string]$Scope,
    [string]$MaterialPath,
    [string]$ResultPath,
    [string]$RemoteOldSha,
    [string]$NewSha,
    [string]$RemoteRef = 'refs/heads/main'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CodeReviewerCommon.ps1')

if ($Create) {
    foreach ($required in @($BaseSha, $Scope, $MaterialPath, $ResultPath, $NewSha)) {
        if ([string]::IsNullOrWhiteSpace($required)) { throw 'Receipt creation requires base, head, scope, material, and result.' }
    }
    if (-not (Test-Path -LiteralPath $MaterialPath -PathType Leaf) -or -not (Test-Path -LiteralPath $ResultPath -PathType Leaf)) { throw 'Receipt input is missing.' }
    $result = Get-Content -LiteralPath $ResultPath -Raw | ConvertFrom-Json
    $findings = @($result.blocking_findings)
    if ($result.review_complete -ne $true -or $result.verdict -ne 'PASS' -or $findings.Count -ne 0) { throw 'Only an exact clean PASS result can create a receipt.' }
    $descriptor = New-CodeReviewSnapshotDescriptor -BaseSha $BaseSha -HeadSha $NewSha -Scope $Scope
    $baseTree = (& git rev-parse "$BaseSha^{tree}").Trim()
    $headTree = (& git rev-parse "$NewSha^{tree}").Trim()
    $receipt = [ordered]@{
        schema = 'dnppv2-code-review-receipt/v1'
        baseSha = $BaseSha
        headSha = $NewSha
        baseTree = $baseTree
        headTree = $headTree
        scope = $Scope
        snapshotSha256 = Get-CodeReviewSha256 $descriptor
        materialSha256 = (Get-FileHash -LiteralPath $MaterialPath -Algorithm SHA256).Hash.ToLowerInvariant()
        resultSha256 = (Get-FileHash -LiteralPath $ResultPath -Algorithm SHA256).Hash.ToLowerInvariant()
        reviewComplete = $true
        verdict = 'PASS'
        blockingFindingCount = 0
    }
    $parent = Split-Path -Parent $ReceiptPath
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    $temporary = "$ReceiptPath.$PID.tmp"
    $receipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temporary -Encoding utf8NoBOM
    Move-Item -LiteralPath $temporary -Destination $ReceiptPath -Force
    Write-Output "CODE_REVIEW_RECEIPT_CREATED=$ReceiptPath"
    return
}

if (-not (Test-Path -LiteralPath $ReceiptPath -PathType Leaf)) { throw "Receipt is missing: $ReceiptPath" }
$receipt = Get-Content -LiteralPath $ReceiptPath -Raw | ConvertFrom-Json
if ($receipt.schema -ne 'dnppv2-code-review-receipt/v1') { throw 'Unsupported receipt schema.' }
foreach ($field in @('baseSha','headSha','baseTree','headTree','scope','snapshotSha256','materialSha256','resultSha256','reviewComplete','verdict','blockingFindingCount')) {
    if ($receipt.PSObject.Properties.Name -notcontains $field) { throw "Receipt field is missing: $field" }
}
if ($receipt.reviewComplete -ne $true -or $receipt.verdict -ne 'PASS' -or [int]$receipt.blockingFindingCount -ne 0) { throw 'Receipt is not an exact clean PASS.' }
if ($receipt.baseSha -notmatch '^[0-9a-f]{40}$' -or $receipt.headSha -notmatch '^[0-9a-f]{40}$') { throw 'Receipt commit identity is invalid.' }
if ($receipt.baseSha -eq $receipt.headSha) { throw 'Receipt base and head must differ.' }
& git cat-file -e "$($receipt.baseSha)^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) { throw 'Receipt base is not a Git commit.' }
& git cat-file -e "$($receipt.headSha)^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) { throw 'Receipt head is not a Git commit.' }
$baseTree = (& git rev-parse "$($receipt.baseSha)^{tree}").Trim()
$headTree = (& git rev-parse "$($receipt.headSha)^{tree}").Trim()
if ($baseTree -ne $receipt.baseTree -or $headTree -ne $receipt.headTree) { throw 'Receipt tree binding does not match Git.' }
$descriptor = New-CodeReviewSnapshotDescriptor -BaseSha $receipt.baseSha -HeadSha $receipt.headSha -Scope ([string]$receipt.scope)
if ((Get-CodeReviewSha256 $descriptor) -ne ([string]$receipt.snapshotSha256).ToLowerInvariant()) { throw 'Receipt snapshot binding does not match Git.' }
if ($RemoteOldSha -and $receipt.baseSha -ne $RemoteOldSha) { throw "Receipt base does not match remote old value for $RemoteRef." }
if ($NewSha -and $receipt.headSha -ne $NewSha) { throw "Receipt head does not match pushed value for $RemoteRef." }
if ($RemoteRef -match '^refs/heads/main$') {
    & git merge-base --is-ancestor $receipt.baseSha $receipt.headSha 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Receipt head is not descended from receipt base.' }
}
Write-Output "CODE_REVIEW_RECEIPT=Passed;REF=$RemoteRef;HEAD=$($receipt.headSha)"
