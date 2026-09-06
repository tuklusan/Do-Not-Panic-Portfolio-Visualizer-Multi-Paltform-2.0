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
    if (@($entries).Count -ne 21) { throw "Workflow job '$JobName' must define exactly 21 runner/RID entries; found $(@($entries).Count)." }
    return @($entries)
}

function Assert-JobTimeout([string]$Workflow, [string]$JobName) {
    $jobPattern = '(?ms)^  ' + [regex]::Escape($JobName) + ':.*?(?=^  [A-Za-z0-9_-]+:|\z)'
    $job = [regex]::Match($Workflow, $jobPattern).Value
    if ([string]::IsNullOrWhiteSpace($job) -or $job -notmatch '(?m)^\s+timeout-minutes:\s*[1-9][0-9]*\s*$') {
        throw "Workflow job '$JobName' must declare a positive timeout-minutes value."
    }
}

$workflow = Read-Workflow $WorkflowPath
$cleanupWorkflow = Read-Workflow $CleanupWorkflowPath
$validatorPath = Join-Path $repoRoot 'build/Test-HostedSoakClosure.ps1'
if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) { throw "Missing deterministic hosted soak validator: $validatorPath" }
$validatorText = [IO.File]::ReadAllText($validatorPath)
foreach ($requiredParameter in @('ArtifactRoot', 'ExpectedRunId', 'ExpectedCommitSha', 'ExpectedLaneCount')) {
    if ($validatorText -notmatch ("\$" + [regex]::Escape($requiredParameter))) { throw "Hosted soak validator is missing required parameter '$requiredParameter'." }
}

if ($workflow -notmatch '(?ms)^concurrency:\s*\r?\n\s+group:\s+dnppv2-complete-matrix\s*\r?\n\s+cancel-in-progress:\s+false') {
    throw 'Hosted matrix workflow must serialize complete runs and wait for queued lanes and evidence review.'
}
$concurrencyIndex = $workflow.IndexOf("`nconcurrency:", [StringComparison]::Ordinal)
$jobsIndex = $workflow.IndexOf("`njobs:", [StringComparison]::Ordinal)
if ($concurrencyIndex -lt 0 -or $jobsIndex -lt 0 -or $concurrencyIndex -gt $jobsIndex) {
    throw 'Hosted matrix concurrency must be a workflow-root block before jobs.'
}
if ($workflow -match '(?s)post-soak-review.*artifactDirectory.*safeMessage') {
    throw 'Hosted aggregate review contains obsolete remote-review error interpolation.'
}

foreach ($jobName in @('gate', 'publish', 'real-product-soak', 'post-soak-review')) { Assert-JobTimeout $workflow $jobName }
Assert-JobTimeout $cleanupWorkflow 'cleanup'

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
$expectedLaneCountMatch = [regex]::Match($workflow, "EXPECTED_LANE_COUNT:\s*'(?<count>[0-9]+)'")
if (-not $expectedLaneCountMatch.Success) { throw 'Workflow is missing EXPECTED_LANE_COUNT.' }
$expectedLaneCount = [int]$expectedLaneCountMatch.Groups['count'].Value
if (@($publishEntries).Count -ne $expectedLaneCount -or @($soakEntries).Count -ne $expectedLaneCount) {
    throw 'Declared EXPECTED_LANE_COUNT does not match the actual matrix entry count.'
}
if ((@($publishEntries | Sort-Object) -join "`n") -cne (@($soakEntries | Sort-Object) -join "`n")) {
    throw 'Publish and real-product-soak runner/RID matrices diverge.'
}
if (@($publishEntries | Select-Object -Unique).Count -ne 21) { throw 'Hosted runner matrix contains duplicate runner/RID entries.' }
foreach ($requiredEntry in @('ubuntu-slim|linux-x64', 'macos-latest|osx-arm64', 'xcode-27|osx-arm64')) {
    if ($publishEntries -cnotcontains $requiredEntry) { throw "Hosted runner matrix is missing required entry '$requiredEntry'." }
}
if ([regex]::Matches($workflow, 'Invoke-NvidiaReviewHarness\.ps1\s+-ReviewType\s+TEST_ARTIFACT').Count -ne 1) {
    throw 'Hosted soak workflow is missing mandatory NVIDIA test-artifact review.'
}
if ($workflow -notmatch '(?m)^\s+if:\s+always\(\)\s+&&\s+runner\.os\s+==\s+''Linux''') {
    throw 'Hosted soak workflow is missing unconditional Linux Xvfb cleanup.'
}
if ($workflow -notmatch '(?ms)^env:\s*\r?\n\s+EXPECTED_LANE_COUNT:\s*''21''\s*$' -or
    $workflow -notmatch 'ExpectedLaneCount \(\[int\]\$env:EXPECTED_LANE_COUNT\)') {
    throw 'Hosted post-soak review is missing the complete 21-runner evidence count gate.'
}
if ($workflow -notmatch 'Inspect and retain lane closure evidence' -or
    $workflow -notmatch 'dnppv2-lane-closure-record/v2' -or
    $workflow -notmatch 'dnppv2-test-artifact-review-result/v2' -or
    $workflow -notmatch 'inspectedEvidenceRetained = \$failures\.Count -eq 0') {
    throw 'Hosted soak workflow is missing the mandatory per-lane closure evidence inspection gate.'
}
foreach ($contractToken in @('$review = $safeOutput | ConvertFrom-Json', 'review_complete', 'snapshot_id', 'blocking_findings', 'dnppv2-test-artifact-review-result/v2')) {
    if (-not $workflow.Contains($contractToken)) { throw "Workflow is missing defensive reviewer contract token: $contractToken" }
}
foreach ($packetToken in @('Read-CircularTraceText', 'ReadAllBytes($tracePath)', '120000', '[TRACE_EXCERPT_OMITTED]')) {
    if (-not $workflow.Contains($packetToken)) { throw "Hosted reviewer packet is missing binary-safe trace handling or the bounded trace excerpt contract: $packetToken" }
}
if ($workflow.Contains('Set-Content -LiteralPath $tracePath')) {
    throw 'Hosted workflow must never overwrite original circular trace files.'
}
if ($workflow -notmatch '\$trace\s*=.*normalizedTraceByPath' -or
    $workflow -notmatch '\$rssUsable\s*=\s*\$trace' -or
    $workflow -notmatch '\$content\s*=\s*\$normalizedTraceByName\[\$traceFile.Name\]' -or
    $workflow -notmatch '\$tracePaths\s*\|\s*ForEach-Object') {
    throw 'Hosted workflow must read complete binary circular traces before evidence extraction and manifest excerpting.'
}
$traceReadIndex = $workflow.IndexOf('$trace =', [StringComparison]::Ordinal)
$rssEvidenceIndex = $workflow.IndexOf('$rssUsable = $trace', [StringComparison]::Ordinal)
$manifestContentIndex = $workflow.IndexOf('$content = $normalizedTraceByName[$traceFile.Name]', [StringComparison]::Ordinal)
$excerptIndex = $workflow.IndexOf('$excerpt = if ($content.Length -le 120000)', [StringComparison]::Ordinal)
if ($traceReadIndex -lt 0 -or $rssEvidenceIndex -le $traceReadIndex -or
    $manifestContentIndex -lt 0 -or $excerptIndex -le $manifestContentIndex) {
    throw 'Hosted workflow trace normalization must precede RSS/AI evidence matching and bounded manifest excerpting.'
}
if (-not $workflow.Contains('if: always()') -or
    -not $workflow.Contains('Write-Host "::add-mask::$env:NVIDIA_API_KEY_CODING"') -or
    -not $workflow.Contains('Write-Host "::add-mask::$env:OPENROUTER_API_KEY"') -or
    -not $workflow.Contains('Write-Host "::add-mask::$env:DNPPV_OPENROUTER_API_KEY"')) {
    throw 'Hosted soak workflow is missing pre-execution secret masking or unconditional evidence finalization.'
}
if ($workflow -notmatch 'Initialize lane closure receipt after soak' -or
    $workflow -notmatch 'if: always\(\)' -or
    $workflow -notmatch 'soak-failed-or-cancelled' -or
    $workflow -notmatch 'soak-result-missing-after-cancellation' -or
    -not $workflow.Contains('aiRequestObserved = $null -ne $newsEvidence -and [bool]$newsEvidence.aiRequestObserved') -or
    -not $workflow.Contains('aiSuccessObserved = $null -ne $newsEvidence -and [bool]$newsEvidence.aiSuccessObserved') -or
    -not $workflow.Contains('news-evidence.json malformed') -or
    -not $workflow.Contains('news-evidence.json missing')) {
    throw 'Hosted soak workflow is missing the post-soak attributable lane receipt.'
}
if ($workflow -notmatch 'Secret redaction verification failed' -or
    -not $workflow.Contains('authorization\s*:\s*bearer') -or
    $workflow -notmatch 'DNPPV_OPENROUTER_API_KEY' -or
    $workflow -notmatch 'Write-Host "::add-mask::\$env:OPENROUTER_API_KEY"') {
    throw 'Hosted soak workflow is missing verified secret redaction for retained evidence.'
}
foreach ($binaryToken in @('ReadAllBytes($tracePath)', 'Test-ByteSequence', 'Test-ByteSequenceIgnoringNul', 'normalizedTraceByPath', 'quarantine', 'Move-Item -LiteralPath $tracePath')) {
    if (-not $workflow.Contains($binaryToken)) { throw "Hosted workflow is missing binary-safe trace secret handling: $binaryToken" }
}
if (-not $workflow.Contains('normalizedTraceByName') -or -not $workflow.Contains('Normalized circular trace content is missing')) { throw 'Hosted workflow is missing filename-keyed normalized trace packaging.' }
if (-not $workflow.Contains('NUL_SECRET_SCAN_SELF_TEST=Passed')) { throw 'Hosted workflow is missing the binary secret scanner NUL-boundary self-test.' }
if ($workflow -notmatch 'ProductShellWindow\.axaml\.cs:153-181' -or $workflow -notmatch 'Invoke-ProductSoak\.ps1') { throw 'Hosted screenshot timing gate is missing source references.' }
$soakJob = [regex]::Match($workflow, '(?ms)^  real-product-soak:.*?(?=^  [A-Za-z0-9_-]+:|\z)').Value
$checkoutIndex = $soakJob.IndexOf('- uses: actions/checkout@v4', [StringComparison]::Ordinal)
$maskIndex = $soakJob.IndexOf('- name: Validate and mask soak credentials', [StringComparison]::Ordinal)
$setupIndex = $soakJob.IndexOf('- uses: actions/setup-dotnet@v4', [StringComparison]::Ordinal)
if ($checkoutIndex -lt 0 -or $maskIndex -le $checkoutIndex -or $setupIndex -le $maskIndex) {
    throw 'Hosted soak credentials must be masked immediately after checkout and before setup-dotnet.'
}
if ($workflow -notmatch 'Test-HostedSoakClosure\.ps1' -or
    $workflow -notmatch 'ExpectedCommitSha \$env:GITHUB_SHA' -or
    $workflow -match '(?s)post-soak-review.*Invoke-NvidiaReviewHarness\.ps1') {
    throw 'Hosted aggregate must use the deterministic validator and contain no remote reviewer invocation.'
}
if (-not $workflow.Contains('-OutputDirectory $reviewRoot') -or $workflow.Contains('-OutputDirectory $harnessRoot')) {
    throw 'Lane reviewer output must remain under the ignored repository-root review artifact directory.'
}
$reviewDirectoryProbe = Join-Path $repoRoot 'artifacts/soak/gate-probe/review'
$null = & git check-ignore -q --no-index $reviewDirectoryProbe
$ignoredProbe = $LASTEXITCODE -eq 0
if (-not $ignoredProbe) { throw 'The per-lane reviewer output directory is not covered by the repository artifact ignore rule.' }
$probeRunId = [DateTime]::UtcNow.Ticks.ToString([Globalization.CultureInfo]::InvariantCulture)
$dynamicReviewProbe = Join-Path $repoRoot ('artifacts/soak/{0}-{1}-{2}/review' -f $probeRunId, 'windows-2025', 'win-x64')
$null = & git check-ignore -q --no-index $dynamicReviewProbe
if ($LASTEXITCODE -ne 0) { throw 'Dynamic hosted lane reviewer output is not covered by the repository artifact ignore rule.' }
$harnessPath = Join-Path $repoRoot 'build/Invoke-NvidiaReviewHarness.ps1'
if (-not (Test-Path -LiteralPath $harnessPath -PathType Leaf)) { throw "Missing NVIDIA review harness: $harnessPath" }
$harnessText = [IO.File]::ReadAllText($harnessPath)
foreach ($harnessContractToken in @('OutputDirectory must resolve under the repository root.', 'Assert-GitIgnoredPath $relativeOutputDirectory', 'Assert-GitIgnoredPath $relativeTelemetryPath')) {
    if (-not $harnessText.Contains($harnessContractToken)) { throw "NVIDIA harness output contract is missing: $harnessContractToken" }
}
if ($workflow -notmatch "github\.event_name == 'push'" -or
    $workflow -notmatch "github\.event_name == 'workflow_dispatch'") {
    throw 'Hosted post-soak review must cover both push and manual soak runs.'
}
if ($workflow -notmatch "dotnet-version: '10\.0\.x'") { throw 'Hosted workflow must pin the .NET 10 SDK line.' }

Write-Output "WORKFLOW_GATE_CONFIGURATION=Passed;RUNNERS=$(@($publishEntries).Count)"
