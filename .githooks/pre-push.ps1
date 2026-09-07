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
param(
    [string]$RemoteName,
    [string]$RemoteUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    throw 'Could not resolve the repository root for the DNPPV pre-push hook.'
}

$upstreamGuard = Join-Path $repoRoot 'build\Assert-NoUpstreamMutation.ps1'
$licenseGate = Join-Path $repoRoot 'build\Test-LicenseHeaders.ps1'
$syntaxGate = Join-Path $repoRoot 'build\Test-PowerShellSyntax.ps1'
$workflowGate = Join-Path $repoRoot 'build\Test-WorkflowGateConfiguration.ps1'
$harnessFreeze = Join-Path $repoRoot 'build\Test-HarnessFreeze.ps1'
$receiptValidator = Join-Path $repoRoot 'build\Assert-CodeReviewReceipt.ps1'

foreach ($requiredPath in @($upstreamGuard, $licenseGate, $syntaxGate, $workflowGate, $harnessFreeze, $receiptValidator)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Missing required pre-push gate: $requiredPath"
    }
}

& $upstreamGuard -RemoteName $RemoteName -RemoteUrl $RemoteUrl
& $licenseGate
& $syntaxGate
& $workflowGate
& $harnessFreeze -BaseRef $(if ($RemoteName) { "$RemoteName/main" } else { 'HEAD^' })

$configuredHooksPath = (& git config --local core.hooksPath).Trim()
if ($configuredHooksPath -notin @('.githooks', '.githooks/')) {
    throw "Repository-local core.hooksPath is not active: $configuredHooksPath"
}

$updates = @($input | ForEach-Object { [string]$_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
foreach ($update in $updates) {
    $parts = $update -split '\s+', 4
    if ($parts.Count -ne 4) { throw "Malformed pre-push update tuple: $update" }
    $newSha = $parts[1]
    $remoteRef = $parts[2]
    $remoteOld = $parts[3]
    if ($remoteRef -ne 'refs/heads/main') { continue }
    if ($newSha -match '^0{40}$') { throw 'Protected main deletion is not permitted.' }
    $receipt = Join-Path $repoRoot ("build/code-review/receipts/{0}.json" -f $newSha)
    & $receiptValidator -ReceiptPath $receipt -RemoteOldSha $remoteOld -NewSha $newSha -RemoteRef $remoteRef
    if ($LASTEXITCODE -ne 0) { throw "Committed-candidate receipt rejected protected update: $remoteRef" }
}

Write-Output 'PRE_PUSH_GATES=Passed'
