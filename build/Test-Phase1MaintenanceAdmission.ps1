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
    [string]$BeforeSha,
    [string]$HeadSha,
    [string]$OutputPath,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Maintenance admission requires PowerShell 7 or later.' }
. ([IO.Path]::Combine($PSScriptRoot,'CodeReviewerCommon.ps1'))
$repoRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..'))
$authorizedRemoteBase = '4eaffea49a68c21c9b6707983773f21b28ea76c1'
$authorizedPreMaintenanceHead = 'f5ec81a888dc66c26fa9e3a2fcaf30f9820d7009'
$authorizedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($path in @(
    '.githooks/pre-push','.githooks/pre-push.ps1','.github/workflows/publish-six-rids.yml','AGENTS.md',
    'build/Assert-CodeReviewReceipt.ps1','build/CodeReviewerCommon.ps1','build/Invoke-BootstrapReviewer.ps1',
    'build/Invoke-CodeReviewHarness.ps1','build/Invoke-NvidiaReviewHarness.ps1','build/Invoke-ReviewGate.ps1',
    'build/Run-CodeReview.ps1','build/Run-NvidiaCodeReview.ps1','build/Test-CodeReviewGitPacket.ps1',
    'build/Test-CodeReviewReceipt.ps1','build/Test-CodeReviewTransaction.ps1',
    'build/Test-CodeReviewerWorkflowGate.ps1','build/Test-HarnessFreeze.ps1','build/Test-LicenseHeaders.ps1',
    'build/Test-NvidiaWorkflowGate.ps1','build/Test-PowerShellSyntax.ps1','build/Test-PrePushHookProtocol.ps1',
    'build/Test-ReviewPolicyRouting.ps1','build/Test-WorkflowGateConfiguration.ps1',
    'build/Test-Phase1MaintenanceAdmission.ps1',
    'docs/AUDIT_STATE.json','docs/CR-097-VENDOR-NEUTRAL-CODE-REVIEW.md',
    'docs/CR-102-VENDOR-NEUTRAL-REVIEWER-CLEANUP.md','docs/CR-120-MINIMAL-REVIEW-MATERIAL-CONTRACT.md',
    'docs/CR-121-NVIDIA-REVIEW-GATE-THINKING-TRANSPORT-CLEANUP.md',
    'docs/FRESH-PROJECT-CODE-REVIEW-GATE.md','docs/FRESH-PROJECT-NVIDIA-REVIEW-GATE.md',
    'docs/TEST_HARNESS_FREEZE.md','docs/TEST_HARNESS_MANIFEST.json'
)) { [void]$authorizedPaths.Add($path) }

function Assert-AuthorizedPaths([string[]]$Paths) {
    if ($Paths.Count -eq 0) { throw 'Maintenance candidate has no changed paths.' }
    foreach ($path in $Paths) {
        if (-not $authorizedPaths.Contains($path)) { throw "Phase-1 maintenance candidate contains unauthorized path: $path" }
    }
}

if ($SelfTest) {
    $nulFixture = [Text.UTF8Encoding]::new($false,$true).GetString([Text.UTF8Encoding]::new($false).GetBytes("build/Invoke-ReviewGate.ps1`0docs/TEST_HARNESS_MANIFEST.json`0"))
    $parsedFixture = @($nulFixture.TrimEnd([char]0) -split [char]0)
    if ($parsedFixture.Count -ne 2) { throw 'NUL-delimited maintenance path parsing failed.' }
    Assert-AuthorizedPaths -Paths $parsedFixture
    Assert-AuthorizedPaths -Paths @('build/Invoke-ReviewGate.ps1','docs/TEST_HARNESS_MANIFEST.json')
    $unknownRejected = $false
    try { Assert-AuthorizedPaths -Paths @('src/App/Program.cs') }
    catch { $unknownRejected = $_.Exception.Message -match 'unauthorized path' }
    if (-not $unknownRejected) { throw 'Product-path negative admission test was accepted.' }
    $extraRejected = $false
    try { Assert-AuthorizedPaths -Paths @('build/Invoke-ReviewGate.ps1','build/unlisted-helper.ps1') }
    catch { $extraRejected = $_.Exception.Message -match 'unauthorized path' }
    if (-not $extraRejected) { throw 'Unexpected-helper negative admission test was accepted.' }
    Write-Output 'PHASE1_MAINTENANCE_ADMISSION_SELFTEST=Passed'
    exit 0
}

$admitted = $false
if ($BeforeSha -ceq $authorizedRemoteBase) {
    if ($HeadSha -notmatch '^[0-9a-f]{40}$') { throw 'Maintenance head SHA is invalid.' }
    $actualHead = (& git -C $repoRoot rev-parse HEAD).Trim()
    $actualParent = (& git -C $repoRoot rev-parse HEAD^).Trim()
    if ($LASTEXITCODE -ne 0 -or $actualHead -cne $HeadSha -or $actualParent -cne $authorizedPreMaintenanceHead) {
        throw 'Maintenance candidate is not the one child of the authorized pre-maintenance HEAD.'
    }
    & git -C $repoRoot merge-base --is-ancestor $authorizedRemoteBase $HeadSha
    if ($LASTEXITCODE -ne 0) { throw 'Maintenance remote base is not an ancestor of the candidate.' }
    $commitCount = (& git -C $repoRoot rev-list --count "$authorizedRemoteBase..$HeadSha").Trim()
    if ($LASTEXITCODE -ne 0 -or $commitCount -cne '3') { throw 'Maintenance transition contains an unexpected commit count.' }
    $diffBytes = Invoke-CodeReviewRawGit -RepositoryRoot $repoRoot -Arguments @('diff','--name-only','-z',"$authorizedRemoteBase..$HeadSha",'--')
    if ($diffBytes.Length -lt 2 -or $diffBytes[-1] -ne 0) { throw 'Maintenance changed-path enumeration is malformed.' }
    $pathText = [Text.UTF8Encoding]::new($false,$true).GetString($diffBytes)
    $paths = @($pathText.TrimEnd([char]0) -split [char]0)
    if (@($paths | Where-Object { [string]::IsNullOrEmpty($_) }).Count -ne 0) { throw 'Maintenance changed-path enumeration contains an empty record.' }
    Assert-AuthorizedPaths -Paths $paths
    $admitted = $true
}
if ($OutputPath) {
    $resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
    if (-not [IO.File]::Exists($resolvedOutput)) { throw 'GitHub output file is unavailable.' }
    [IO.File]::AppendAllText($resolvedOutput,"admitted=$($admitted.ToString().ToLowerInvariant())`n",[Text.UTF8Encoding]::new($false))
}
Write-Output "PHASE1_MAINTENANCE_ADMITTED=$($admitted.ToString().ToLowerInvariant())"
