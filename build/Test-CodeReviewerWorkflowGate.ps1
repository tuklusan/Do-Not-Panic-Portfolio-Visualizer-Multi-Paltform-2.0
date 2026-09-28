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
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'DNPPV review configuration requires PowerShell 7 or later.' }
$repoRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..'))
$runnerPath = [IO.Path]::Combine($PSScriptRoot,'Run-CodeReview.ps1')
$orchestratorPath = [IO.Path]::Combine($PSScriptRoot,'Invoke-CodeReviewHarness.ps1')
$policyPath = [IO.Path]::Combine($PSScriptRoot,'Invoke-ReviewGate.ps1')
$adapterPath = [IO.Path]::Combine($PSScriptRoot,'Invoke-NvidiaReviewHarness.ps1')
$standardPath = [IO.Path]::Combine($repoRoot,'docs','FRESH-PROJECT-CODE-REVIEW-GATE.md')
$manifestPath = [IO.Path]::Combine($repoRoot,'docs','TEST_HARNESS_MANIFEST.json')
foreach ($path in @($runnerPath,$orchestratorPath,$policyPath,$adapterPath,$standardPath)) {
    if (-not [IO.File]::Exists($path)) { throw "Missing active review component: $path" }
}
if (-not [IO.File]::Exists($manifestPath)) { throw 'Harness manifest is missing.' }
$requiredReviewHarnesses = @(
    '.githooks/pre-push','.githooks/pre-push.ps1',
    'build/Run-CodeReview.ps1','build/Invoke-CodeReviewHarness.ps1','build/Invoke-ReviewGate.ps1',
    'build/Invoke-NvidiaReviewHarness.ps1','build/CodeReviewerCommon.ps1','build/NvidiaWorkflowCommon.ps1',
    'build/Assert-CodeReviewReceipt.ps1','build/Test-CodeReviewReceipt.ps1',
    'build/Test-CodeReviewGitPacket.ps1','build/Test-CodeReviewTransaction.ps1',
    'build/Test-PrePushHookProtocol.ps1','build/Test-ReviewPolicyRouting.ps1',
    'build/Test-CodeReviewerWorkflowGate.ps1','build/Test-NvidiaWorkflowGate.ps1',
    'build/Test-WorkflowGateConfiguration.ps1','build/Test-HarnessFreeze.ps1',
    'build/Test-Phase1MaintenanceAdmission.ps1',
    'build/Test-LicenseHeaders.ps1','build/Test-PowerShellSyntax.ps1',
    'docs/TEST_HARNESS_MANIFEST.json','docs/TEST_HARNESS_FREEZE.md'
)
function Assert-ReviewHarnessManifest([string[]]$ManifestPaths) {
    if (@($ManifestPaths | Sort-Object -Unique).Count -ne $ManifestPaths.Count) { throw 'Harness manifest contains duplicate paths.' }
    foreach ($requiredPath in $requiredReviewHarnesses) {
        if ($ManifestPaths -cnotcontains $requiredPath) { throw "Harness manifest omits active review helper: $requiredPath" }
    }
    foreach ($retiredPath in @('build/Invoke-BootstrapReviewer.ps1','build/Run-NvidiaCodeReview.ps1')) {
        if ($ManifestPaths -ccontains $retiredPath) { throw "Harness manifest retains retired helper: $retiredPath" }
    }
}
$manifest = [IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -ErrorAction Stop
Assert-ReviewHarnessManifest -ManifestPaths @($manifest.tracked_harnesses)
foreach ($retired in @('Invoke-BootstrapReviewer.ps1','Run-NvidiaCodeReview.ps1')) {
    if ([IO.File]::Exists([IO.Path]::Combine($PSScriptRoot,$retired))) { throw "Retired review wrapper is still active: $retired" }
}
$runner = [IO.File]::ReadAllText($runnerPath)
$orchestrator = [IO.File]::ReadAllText($orchestratorPath)
$policy = [IO.File]::ReadAllText($policyPath)
$adapter = [IO.File]::ReadAllText($adapterPath)
$standard = [IO.File]::ReadAllText($standardPath)
if ($runner -notmatch 'Invoke-CodeReviewHarness\.ps1' -or
    $orchestrator -notmatch 'Invoke-ReviewGate\.ps1' -or
    $policy -notmatch 'Invoke-NvidiaReviewHarness\.ps1') {
    throw 'The fixed production review call graph is incomplete.'
}
if ($runner -match '(?i)nemotron|nvidia/' -or $orchestrator -match '(?i)nemotron|nvidia/') {
    throw 'Provider-specific policy leaked into the generic runner or orchestrator.'
}
foreach ($productionText in @($runner,$orchestrator,$policy,$adapter)) {
    if ($productionText -match 'DNPPV_REVIEW_ENGINE|CODE_REVIEWER_ENGINE|TestImplementationRoot|TestPolicyPath|TestAdapterPath|TestHttpClient|TestApiKey|TestSkipDelays|TestBuildOnly') {
        throw 'Production review code exposes a retired engine or test override.'
    }
}
if ($adapter -match 'Invoke-RestMethod|Invoke-WebRequest' -or
    $adapter -notmatch 'Net\.Http\.HttpClient' -or
    $policy -notmatch 'MODEL_PROTOCOL_FAILURE' -or
    $policy -notmatch 'Invoke-ReviewProtocolPacketAudit') {
    throw 'Provider transport or malformed-response audit is not configured as required.'
}
foreach ($standardToken in @('dnppv2-review-result/v3','zero-context diff','OutputError','typed JSON error')) {
    if (-not $standard.Contains($standardToken)) { throw "Review standard is missing: $standardToken" }
}
if ($SelfTest) {
    $shed = @($manifest.tracked_harnesses | Where-Object { $_ -cne 'build/Invoke-ReviewGate.ps1' })
    $shedRejected = $false
    try { Assert-ReviewHarnessManifest -ManifestPaths $shed }
    catch { $shedRejected = $_.Exception.Message -match 'omits active review helper' }
    if (-not $shedRejected) { throw 'Manifest-shed negative self-test was accepted.' }
    $probeRoot = [IO.Path]::Combine([IO.Path]::GetTempPath(),'dnppv2-review-cli-probe-' + [guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($probeRoot) | Out-Null
    try {
        $stdoutPath = [IO.Path]::Combine($probeRoot,'stdout.txt')
        $stderrPath = [IO.Path]::Combine($probeRoot,'stderr.txt')
        & pwsh -NoProfile -File $runnerPath -ReviewType CODE -RepositoryRoot $repoRoot -BaseSha ('0' * 40) -HeadSha ('0' * 40) -Scope 'selftest' -Requirement 'Reject an invalid candidate before provider dispatch.' 1> $stdoutPath 2> $stderrPath
        $code = $LASTEXITCODE
        $stdout = [IO.File]::ReadAllText($stdoutPath)
        $stderr = [IO.File]::ReadAllText($stderrPath)
        if ($code -eq 0 -or -not [string]::IsNullOrWhiteSpace($stdout)) { throw 'Invalid candidate did not fail on stderr-only channel.' }
        $errorResult = $stderr | ConvertFrom-Json -ErrorAction Stop
        if ($errorResult.schema -cne 'dnppv2-review-error/v1') { throw 'CLI error channel is not typed JSON.' }
    }
    finally {
        if ([IO.Directory]::Exists($probeRoot)) { Remove-Item -LiteralPath $probeRoot -Recurse -Force }
    }
    Write-Output 'CODE_REVIEWER_WORKFLOW_GATE_SELFTEST=Passed'
    exit 0
}
Write-Output 'CODE_REVIEWER_WORKFLOW_GATE=Passed'
