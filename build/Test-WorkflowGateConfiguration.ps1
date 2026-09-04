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
    [string]$WorkflowPath = 'publish-six-rids.yml',
    [string]$CleanupWorkflowPath = 'cleanup-generated-artifacts.yml'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) { throw 'Could not resolve repository root.' }

function Read-Workflow([string]$RelativePath) {
    $path = Join-Path $repoRoot (Join-Path '.github/workflows' $RelativePath)
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing workflow: $path" }
    return [IO.File]::ReadAllText($path)
}

function Get-MatrixEntries([string]$Workflow, [string]$JobName) {
    $jobPattern = '(?ms)^  ' + [regex]::Escape($JobName) + ':.*?(?=^  [A-Za-z0-9_-]+:|\z)'
    $job = [regex]::Match($Workflow, $jobPattern).Value
    if ([string]::IsNullOrWhiteSpace($job)) { throw "Workflow job '$JobName' is missing." }
    $entries = [regex]::Matches($job, '(?ms)^\s+- runner:\s*(?<runner>[^\r\n]+)\s*\r?\n\s+rid:\s*(?<rid>[^\r\n]+)') |
        ForEach-Object { '{0}|{1}' -f $_.Groups['runner'].Value.Trim(), $_.Groups['rid'].Value.Trim() }
    if (@($entries).Count -ne 18) { throw "Workflow job '$JobName' must define exactly 18 runner/RID entries; found $(@($entries).Count)." }
    return @($entries)
}

$workflow = Read-Workflow $WorkflowPath
$cleanupWorkflow = Read-Workflow $CleanupWorkflowPath

if ($workflow -match '(?m)^\s+schedule:') { throw 'Scheduled workflow execution is prohibited.' }
if ($cleanupWorkflow -match '(?m)^\s+(push|pull_request|workflow_call):') { throw 'Cleanup workflow must remain workflow_dispatch-only.' }
if ($cleanupWorkflow -notmatch '(?m)^\s+workflow_dispatch:') { throw 'Cleanup workflow must retain workflow_dispatch.' }
foreach ($workflowText in @($workflow, $cleanupWorkflow)) {
    if ($workflowText -notmatch '(?ms)^permissions:\s*\r?\n\s+contents:\s+read\s*$') { throw 'Every project workflow must declare contents: read-only permissions.' }
    if ($workflowText -match '(?m)^\s+[A-Za-z_-]+:\s+write(?:-all)?\s*$' -or $workflowText -match '(?m)^\s+permissions:\s+write-all\s*$') { throw 'Workflow permissions must not grant write access.' }
}
foreach ($trigger in @('push:', 'pull_request:', 'workflow_dispatch:')) {
    if ($workflow -notmatch ('(?m)^\s+' + [regex]::Escape($trigger))) { throw "Publish workflow is missing required trigger '$trigger'." }
}

$publishEntries = Get-MatrixEntries $workflow 'publish'
$soakEntries = Get-MatrixEntries $workflow 'real-product-soak'
if ((@($publishEntries | Sort-Object) -join "`n") -cne (@($soakEntries | Sort-Object) -join "`n")) {
    throw 'Publish and real-product-soak runner/RID matrices diverge.'
}
if (@($publishEntries | Select-Object -Unique).Count -ne 18) { throw 'Hosted runner matrix contains duplicate runner/RID entries.' }
if ($workflow -notmatch 'Invoke-NvidiaReviewHarness\.ps1\s+-ReviewType\s+TEST_ARTIFACT') {
    throw 'Hosted soak workflow is missing mandatory NVIDIA test-artifact review.'
}
if ($workflow -notmatch '(?m)^\s+if:\s+always\(\)\s+&&\s+runner\.os\s+==\s+''Linux''') {
    throw 'Hosted soak workflow is missing unconditional Linux Xvfb cleanup.'
}
if ($workflow -notmatch 'Expected 18 soak evidence manifests') {
    throw 'Hosted post-soak review is missing the complete 18-runner evidence count gate.'
}
if ($workflow -notmatch "dotnet-version: '10\.0\.x'") { throw 'Hosted workflow must pin the .NET 10 SDK line.' }

Write-Output "WORKFLOW_GATE_CONFIGURATION=Passed;RUNNERS=$(@($publishEntries).Count)"
