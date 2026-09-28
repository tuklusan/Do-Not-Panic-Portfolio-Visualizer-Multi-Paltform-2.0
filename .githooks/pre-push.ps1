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
param([string]$RemoteName,[string]$RemoteUrl)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$rawUpdates = [Collections.Generic.List[string]]::new()
while ($null -ne ($line = [Console]::In.ReadLine())) {
    if (-not [string]::IsNullOrWhiteSpace($line)) { $rawUpdates.Add($line) }
}
$updates = [Collections.Generic.List[object]]::new()
foreach ($line in $rawUpdates) {
    $parts = @($line -split '\s+')
    if ($parts.Count -ne 4 -or $parts[0] -notmatch '^refs/[^\s]+$' -or
        $parts[1] -notmatch '^[0-9a-fA-F]{40}$' -or $parts[2] -notmatch '^refs/[^\s]+$' -or
        $parts[3] -notmatch '^[0-9a-fA-F]{40}$') { throw 'Malformed pre-push update tuple.' }
    if ($parts[2] -ceq 'refs/heads/main' -and $parts[1] -eq ('0' * 40)) { throw 'Protected main deletion is forbidden.' }
    if ($parts[2] -ceq 'refs/heads/main' -and $parts[3] -eq ('0' * 40)) { throw 'Protected main creation lacks a reviewed base.' }
    $updates.Add([pscustomobject]@{
        LocalRef=$parts[0]; LocalSha=$parts[1].ToLowerInvariant()
        RemoteRef=$parts[2]; RemoteSha=$parts[3].ToLowerInvariant()
    })
}
$repoRoot = (& git rev-parse --show-toplevel 2>$null | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or -not $repoRoot) { throw 'Pre-push repository root is unavailable.' }
$repoRoot = [IO.Path]::GetFullPath($repoRoot)
$build = [IO.Path]::Combine($repoRoot,'build')
$upstreamGuard = [IO.Path]::Combine($build,'Assert-NoUpstreamMutation.ps1')
$licenseGate = [IO.Path]::Combine($build,'Test-LicenseHeaders.ps1')
$syntaxGate = [IO.Path]::Combine($build,'Test-PowerShellSyntax.ps1')
$workflowGate = [IO.Path]::Combine($build,'Test-WorkflowGateConfiguration.ps1')
$harnessFreeze = [IO.Path]::Combine($build,'Test-HarnessFreeze.ps1')
$receiptValidator = [IO.Path]::Combine($build,'Assert-CodeReviewReceipt.ps1')
foreach ($path in @($upstreamGuard,$licenseGate,$syntaxGate,$workflowGate,$harnessFreeze,$receiptValidator)) {
    if (-not [IO.File]::Exists($path)) { throw 'A required pre-push gate is missing.' }
}
& $upstreamGuard -RemoteName $RemoteName -RemoteUrl $RemoteUrl
& $licenseGate
& $syntaxGate
& $workflowGate
& $harnessFreeze -BaseRef $(if ($RemoteName) { "$RemoteName/main" } else { 'HEAD^' })
$configuredHooksPath = (& git -C $repoRoot config --local core.hooksPath | Out-String).Trim()
if ($configuredHooksPath -notin @('.githooks','.githooks/')) { throw 'Repository-local hooksPath is not active.' }
foreach ($update in $updates) {
    if ($update.RemoteRef -cne 'refs/heads/main') { continue }
    & $receiptValidator -Operation Validate -RepositoryRoot $repoRoot -BaseSha $update.RemoteSha -NewSha $update.LocalSha -RemoteOldSha $update.RemoteSha -RemoteRef $update.RemoteRef
}
Write-Output 'PRE_PUSH_GATES=Passed'
