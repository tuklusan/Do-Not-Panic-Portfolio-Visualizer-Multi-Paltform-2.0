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

foreach ($freezePath in @('docs\TEST_HARNESS_MANIFEST.json', 'docs\TEST_HARNESS_FREEZE.md', 'build\Test-HarnessFreeze.ps1')) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $freezePath) -PathType Leaf)) {
        throw "Harness freeze control is missing: $freezePath"
    }
}

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
    if (@($entries).Count -ne 20) { throw "Workflow job '$JobName' must define exactly 20 runner/RID entries; found $(@($entries).Count)." }
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
$workflowRoot = [regex]::Match($workflow, '(?ms)^.*?(?=^jobs:)').Value
if ($workflowRoot -match 'OPENROUTER_API_KEY|DNPPV_OPENROUTER_API_KEY|NVIDIA_API_KEY_CODING') {
    throw 'Workflow-root environment must not define provider credentials.'
}
$validatorPath = Join-Path $repoRoot 'build/Test-HostedSoakClosure.ps1'
if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) { throw "Missing deterministic hosted soak validator: $validatorPath" }
$quarantineTestPath = Join-Path $repoRoot 'build/Test-HostedSoakQuarantine.ps1'
if (-not (Test-Path -LiteralPath $quarantineTestPath -PathType Leaf)) { throw "Missing hosted soak quarantine regression test: $quarantineTestPath" }
$reusePolicyPath = Join-Path $repoRoot 'build/Test-MatrixEvidenceReusePolicy.ps1'
if (-not (Test-Path -LiteralPath $reusePolicyPath -PathType Leaf)) { throw "Missing matrix evidence reuse policy: $reusePolicyPath" }
$validatorText = [IO.File]::ReadAllText($validatorPath)
foreach ($requiredParameter in @('ArtifactRoot', 'ExpectedRunId', 'ExpectedCommitSha', 'ExpectedLaneCount')) {
    if ($validatorText -notmatch ("\$" + [regex]::Escape($requiredParameter))) { throw "Hosted soak validator is missing required parameter '$requiredParameter'." }
}

if ($workflow -notmatch '(?ms)^concurrency:\s*\r?\n\s+group:\s+dnppv2-complete-matrix\s*\r?\n\s+cancel-in-progress:\s+false') {
    throw 'Hosted matrix workflow must serialize complete runs and wait for queued lanes and evidence review.'
}
foreach ($reuseToken in @('evidence_mode', 'reuse-completed-run', 'prior_run_id', 'prior_commit_sha', 'Test-MatrixEvidenceReusePolicy.ps1', 'EVIDENCE_MODE')) {
    if (-not $workflow.Contains($reuseToken)) { throw "Workflow is missing matrix evidence reuse policy token: $reuseToken" }
}
if ($workflow -notmatch 'Retrieve nominated completed run''s soak evidence' -or
    $workflow -notmatch 'ExpectedRunId \$expectedRunId' -or
    $workflow -notmatch 'ExpectedCommitSha \$expectedCommitSha') {
    throw 'Workflow does not validate nominated prior-run evidence without launching a new matrix.'
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
$postSoakJob = [regex]::Match($workflow, '(?ms)^  post-soak-review:.*?(?=^  [A-Za-z0-9_-]+:|\z)').Value
if ([string]::IsNullOrWhiteSpace($postSoakJob)) { throw 'Hosted post-soak review job is missing.' }
if ($postSoakJob -match 'OPENROUTER_API_KEY|DNPPV_OPENROUTER_API_KEY|NVIDIA_API_KEY_CODING|Write-Host\s+"::add-mask::') {
    throw 'Deterministic post-soak review must not receive provider credentials or aggregate-only secret masks.'
}
if ($postSoakJob -notmatch '(?m)^\s+timeout-minutes:\s*120\s*$') { throw 'Deterministic post-soak review must use the bounded 120-minute timeout.' }
if ($postSoakJob -match 'sleep\s+60|Wait one minute for artifact publication') { throw 'Blind artifact-publication sleep is prohibited in deterministic post-soak review.' }

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
if (@($publishEntries | Select-Object -Unique).Count -ne 20) { throw 'Hosted runner matrix contains duplicate runner/RID entries.' }
foreach ($requiredEntry in @('macos-latest|osx-arm64', 'xcode-27|osx-arm64')) {
    if ($publishEntries -cnotcontains $requiredEntry) { throw "Hosted runner matrix is missing required entry '$requiredEntry'." }
}
if ([regex]::Matches($workflow, 'Run-CodeReview\.ps1\s+-ReviewType\s+TEST_ARTIFACT').Count -ne 1) {
    throw 'Hosted soak workflow is missing the provider-neutral test-artifact review caller.'
}
$reviewStepStart = $workflow.IndexOf('      - name: Review soak evidence',[StringComparison]::Ordinal)
$reviewRunStart = if ($reviewStepStart -ge 0) { $workflow.IndexOf('        run: |',$reviewStepStart,[StringComparison]::Ordinal) } else { -1 }
$reviewNextStep = if ($reviewRunStart -ge 0) { $workflow.IndexOf('      - name: Inspect and retain lane closure evidence',$reviewRunStart,[StringComparison]::Ordinal) } else { -1 }
if ($reviewStepStart -lt 0 -or $reviewRunStart -lt 0 -or $reviewNextStep -lt 0) {
    throw 'Hosted review PowerShell block boundaries are missing.'
}
$reviewBlock = $workflow.Substring($reviewRunStart + '        run: |'.Length,$reviewNextStep - $reviewRunStart - '        run: |'.Length)
$reviewPowerShell = (@($reviewBlock -split "`r?`n" | ForEach-Object { $_ -replace '^ {11}','' }) -join "`n")
$reviewTokens = $null; $reviewParseErrors = $null
[void][Management.Automation.Language.Parser]::ParseInput($reviewPowerShell,[ref]$reviewTokens,[ref]$reviewParseErrors)
if ($reviewParseErrors.Count -ne 0) {
    throw ('Hosted review PowerShell does not parse: ' + ($reviewParseErrors[0].Message))
}
foreach ($requiredReviewShape in @('$review.review_complete -isnot [bool]','$review.fallback_used -isnot [bool]')) {
    if (-not $reviewPowerShell.Contains($requiredReviewShape)) { throw "Hosted review lost exact result-shape check: $requiredReviewShape" }
}
foreach ($requiredClosureShape in @('$reviewResult.reviewComplete -isnot [bool]',
    '$reviewResult.blockingFindings -isnot [array]','$reviewResult.uncertainties -isnot [array]')) {
    if (-not $workflow.Contains($requiredClosureShape)) { throw "Hosted closure lost exact result-shape check: $requiredClosureShape" }
}
if ($workflow -match 'Run-CodeReview\.ps1[^\r\n]*2>&1|CODE_REVIEWER_ENDPOINT|CODE_REVIEWER_MODEL|CODE_REVIEWER_REQUEST_OVERRIDES_JSON' -or
    $workflow -match 'DNPPV_REVIEW_CADENCE_SECONDS' -or
    $workflow -notmatch 'DNPPV_REVIEWER_ID:\s*dnppv2-nvidia-review-gate-v2') {
    throw 'Hosted review invocation retains a legacy or mixed-channel configuration.'
}
$reviewArchitectureGate = Join-Path $repoRoot 'build/Test-CodeReviewerWorkflowGate.ps1'
if (-not (Test-Path -LiteralPath $reviewArchitectureGate -PathType Leaf)) { throw 'Review architecture configuration gate is missing.' }
& $reviewArchitectureGate | Out-Null
$maintenanceGatePath = Join-Path $repoRoot 'build/Test-Phase1MaintenanceAdmission.ps1'
if (-not (Test-Path -LiteralPath $maintenanceGatePath -PathType Leaf) -or
    [regex]::Matches($workflow,'Test-Phase1MaintenanceAdmission\.ps1').Count -ne 1 -or
    $workflow -notmatch 'maintenance_admitted:\s*\$\{\{ steps\.phase1\.outputs\.admitted \}\}') {
    throw 'One-time Phase-1 maintenance admission is not wired into the cheap gate.'
}
$maintenanceGateText = [IO.File]::ReadAllText($maintenanceGatePath)
foreach ($admissionToken in @('4eaffea49a68c21c9b6707983773f21b28ea76c1','f5ec81a888dc66c26fa9e3a2fcaf30f9820d7009','Assert-AuthorizedPaths','--name-only','-z')) {
    if (-not $maintenanceGateText.Contains($admissionToken)) { throw "Maintenance admission lacks immutable/path proof: $admissionToken" }
}
foreach ($expensiveJob in @('publish','local-companion-dispatch','real-product-soak','post-soak-review')) {
    $jobBlock = [regex]::Match($workflow,"(?ms)^  $expensiveJob`:.*?(?=^  [A-Za-z0-9_-]+:|\z)").Value
    if ($jobBlock -notmatch "maintenance_admitted != 'true'") { throw "Product job $expensiveJob lacks one-time maintenance admission guard." }
}
$genericReviewPath = Join-Path $repoRoot 'build/Run-CodeReview.ps1'
if (-not (Test-Path -LiteralPath $genericReviewPath -PathType Leaf)) { throw 'Generic review caller is missing.' }
$reviewGatePath = Join-Path $repoRoot 'build/Invoke-ReviewGate.ps1'
if (-not (Test-Path -LiteralPath $reviewGatePath -PathType Leaf)) { throw 'Authoritative review gate is missing.' }
$companionDispatchPath = Join-Path $repoRoot 'build/New-LocalCompanionDispatch.ps1'
if (-not (Test-Path -LiteralPath $companionDispatchPath -PathType Leaf)) { throw 'Local companion dispatch producer is missing.' }
$companionWorkflowTokens = @('local-companion-dispatch', 'New-LocalCompanionDispatch.ps1', 'LOCAL_COMPANION_DISPATCH')
foreach ($token in $companionWorkflowTokens) { if (-not $workflow.Contains($token)) { throw "Hosted workflow is missing local companion dispatch token: $token" } }
$reviewGateText = [IO.File]::ReadAllText($reviewGatePath)
foreach ($reviewGateToken in @('nvidia/nemotron-3-super-120b-a12b', 'nvidia/nemotron-3.5-lightning-30b-a3b', 'Invoke-NvidiaReviewHarness.ps1', 'MODEL_PROTOCOL_FAILURE')) {
    if (-not $reviewGateText.Contains($reviewGateToken)) { throw "Authoritative review gate is missing policy token: $reviewGateToken" }
}
if ($workflow -notmatch '(?m)^\s+if:\s+always\(\)\s+&&\s+runner\.os\s+==\s+''Linux''') {
    throw 'Hosted soak workflow is missing unconditional Linux Xvfb cleanup.'
}
if ($workflow -notmatch '(?ms)^env:\s*\r?\n\s+EXPECTED_LANE_COUNT:\s*''20''\s*$' -or
    $workflow -notmatch 'ExpectedLaneCount \(\[int\]\$env:EXPECTED_LANE_COUNT\)') {
    throw 'Hosted post-soak review is missing the complete 20-runner evidence count gate.'
}
foreach ($pattern in @('B-001', 'B-002', 'NTP-(?:ALL-HOSTS-FAILED|TIME-SYNC-FAILURE|RECURRING-AT-SHUTDOWN)', 'AI-NEWS-SUMMARIZATION-', 'AI\s+summari[sz]', 'LocalClockFallback', 'hasBoundedRenderRecovery', 'newsEvidenceForDisposition', 'aiQuotaForDisposition', 'status_code=4[0-9][0-9]')) {
    if ($workflow -notmatch [regex]::Escape($pattern)) {
        throw "Workflow is missing the evidence-matched external-condition disposition guard: $pattern"
    }
}
if ($workflow -notmatch 'Inspect and retain lane closure evidence' -or
    $workflow -notmatch 'dnppv2-lane-closure-record/v2' -or
    $workflow -notmatch 'dnppv2-test-artifact-review-result/v2' -or
    $workflow -notmatch 'inspectedEvidenceRetained = \$failures\.Count -eq 0') {
    throw 'Hosted soak workflow is missing the mandatory per-lane closure evidence inspection gate.'
}
foreach ($contractToken in @('dnppv2-review-result/v3', 'review_complete', 'snapshot_sha256', 'blocking_findings', 'dnppv2-test-artifact-review-result/v2', 'reviewExitCode', 'review-stdout.json', 'review-stderr.json', 'reviewDiagnostic', 'cleanReview', '$reviewResult.uncertainties')) {
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
if (-not $workflow.Contains('(?:\||/)')) {
    throw 'Hosted workflow RSS evidence matching must recognize the canonical pipe-delimited circular trace format.'
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
foreach ($quarantineToken in @('Quarantine-ReviewerEvidence', 'RUNNER_TEMP', 'Invoke-ReviewerEvidenceQuarantine.ps1')) {
    if (-not $workflow.Contains($quarantineToken)) { throw "Hosted reviewer contamination quarantine is missing executable contract token: $quarantineToken" }
}
$quarantineHelperPath = Join-Path $repoRoot 'build/Invoke-ReviewerEvidenceQuarantine.ps1'
if (-not (Test-Path -LiteralPath $quarantineHelperPath -PathType Leaf)) { throw 'Shared reviewer evidence quarantine helper is missing.' }
$quarantineHelperText = [IO.File]::ReadAllText($quarantineHelperPath)
foreach ($quarantineHelperToken in @('Move-Item -LiteralPath $reviewFull', 'Is-UnderPath', 'RunnerTemp', 'review-evidence-failure/v1', 'review-evidence-contaminated')) {
    if (-not $quarantineHelperText.Contains($quarantineHelperToken)) { throw "Shared reviewer quarantine helper is missing contract token: $quarantineHelperToken" }
}
$reviewHarnessPath = Join-Path $repoRoot 'build/Invoke-NvidiaReviewHarness.ps1'
$reviewHarnessText = [IO.File]::ReadAllText($reviewHarnessPath)
foreach ($reviewPolicyToken in @('10-minute soak evidence', 'effective 30-minute minimum', 'without extra external calls', 'recovered AI 429', 'separately queued migration work')) {
    if (-not $workflow.Contains($reviewPolicyToken)) { throw "Hosted material-review requirement is missing acceptance context: $reviewPolicyToken" }
}
foreach ($snapshotToken in @('$expectedSnapshotId', 'reconstructed commit/material identity', "('{0}:{1}' -f ([string]`$env:GITHUB_SHA).ToLowerInvariant(), `$materialHash)")) {
    if (-not $workflow.Contains($snapshotToken)) { throw "Hosted lane finalization is missing snapshot reconstruction token: $snapshotToken" }
}
foreach ($validatorSnapshotToken in @('$expectedSnapshotId = (''{0}:{1}'' -f $CommitSha.ToLowerInvariant(), $materialHash).ToLowerInvariant()', 'Review snapshot reconstruction mismatch', 'Closure snapshot reconstruction mismatch')) {
    if (-not $validatorText.Contains($validatorSnapshotToken)) { throw "Hosted validator is missing reconstructed snapshot enforcement: $validatorSnapshotToken" }
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
    ($workflow -notmatch 'ExpectedCommitSha \$env:GITHUB_SHA' -and $workflow -notmatch 'ExpectedCommitSha \$expectedCommitSha') -or
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
if ($workflow -notmatch 'New-CodeReviewMaterialPacketV1' -or $workflow -notmatch 'reviewSnapshotSha256') {
    throw 'Hosted lane must bind the v3 review snapshot to reconstructed material identity.'
}
foreach ($harnessContractToken in @('https://integrate.api.nvidia.com/v1/chat/completions', 'New-NvidiaRequestBytes', 'Invoke-NvidiaReviewStage')) {
    if (-not $harnessText.Contains($harnessContractToken)) { throw "NVIDIA harness is missing its approved adapter contract: $harnessContractToken" }
}
if ($workflow -notmatch "github\.event_name == 'push'" -or
    $workflow -notmatch "github\.event_name == 'workflow_dispatch'") {
    throw 'Hosted post-soak review must cover both push and manual soak runs.'
}
if ($workflow -notmatch "dotnet-version: '10\.0\.x'") { throw 'Hosted workflow must pin the .NET 10 SDK line.' }
if ($workflow -notmatch '(?ms)review_wait_policy:.*?default:\s*''bounded-30m''') {
    throw 'Hosted workflow must default reviewer waits to the bounded 30-minute policy.'
}
if ($workflow -notmatch "options:\s*\['bounded-30m',\s*'one-time-slow-review'\]") {
    throw 'Hosted workflow must expose only the bounded default and explicit one-time slow-review policies.'
}
if ($workflow -notmatch '\$reviewTimeoutSeconds\s*=\s*if \(\$env:REVIEW_WAIT_POLICY -eq ''one-time-slow-review''\) \{ 14400 \} else \{ 1800 \}') {
    throw 'Hosted reviewer timeout must be 30 minutes by default with a separately selectable four-hour exception.'
}
if ($workflow -notmatch 'dnppv2-review-result/v3') {
    throw 'Hosted workflow must validate the project reviewer result namespace explicitly.'
}
if ($harnessText -notmatch 'Get-NvidiaRetryDelay' -or $harnessText -notmatch 'Wait-NvidiaDelay') {
    throw 'NVIDIA adapter must enforce the bounded explicit-response retry delay.'
}

Write-Output "WORKFLOW_GATE_CONFIGURATION=Passed;RUNNERS=$(@($publishEntries).Count)"
