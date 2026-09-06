# Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
# Proprietary rights reserved except as expressly licensed herein.
# Based on original work by Supratim Sanyal of SANYALnet Labs.
#
# DO NOT PANIC PORTFOLIO VISUALIZER
# This file is governed by the SANYALnet Labs Non-Commercial License in the
# root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
# for AI/ML model training are prohibited unless separately authorized.
#
# Attribution is required: "Based on original work by Supratim Sanyal of
# SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
# patent, trademark, and governing-law provisions.
#
# Deterministically validates retained hosted soak lane evidence. This validator
# does not contact a reviewer or any other remote service.
# ============================================================================
[CmdletBinding()]
param(
    [string]$ArtifactRoot,
    [string]$ExpectedRunId,
    [string]$ExpectedCommitSha,
    [ValidateRange(1, 100)][int]$ExpectedLaneCount,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Read-JsonFile([string]$Path) {
    try { return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json) }
    catch { throw "Unreadable JSON file: $Path ($($_.Exception.Message))" }
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-RequiredString($Object, [string]$Name, [string]$Context) {
    $value = [string]$Object.$Name
    if ([string]::IsNullOrWhiteSpace($value)) { throw "Missing $Name in $Context." }
    return $value
}

function Test-Closure([string]$Root, [string]$RunId, [string]$CommitSha, [int]$LaneCount) {
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw "Artifact root is missing: $Root" }
    $manifests = @(Get-ChildItem -LiteralPath $Root -Recurse -Filter 'evidence-review-input.txt' -File)
    if ($manifests.Count -ne $LaneCount) { throw "Expected $LaneCount soak evidence manifests, found $($manifests.Count)." }

    $pairs = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $artifactKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $failures = [Collections.Generic.List[string]]::new()
    foreach ($manifest in $manifests | Sort-Object FullName) {
        $material = Get-Content -LiteralPath $manifest.FullName -Raw
        if ($material -match '(?i)(OPENROUTER_API_KEY|DNPPV_OPENROUTER_API_KEY|NVIDIA_API_KEY_CODING)\s*[:=]\s*(?!\[REDACTED\])[^\s,;&]+') { $failures.Add("Manifest contains an unredacted secret marker: $($manifest.FullName)") }
        $runMatch = [regex]::Match($material, '(?m)^run_id=(?<value>[^\r\n]+)$')
        $runnerMatch = [regex]::Match($material, '(?m)^runner=(?<value>[^\r\n]+)$')
        $ridMatch = [regex]::Match($material, '(?m)^rid=(?<value>[^\r\n]+)$')
        if (-not $runMatch.Success -or -not $runnerMatch.Success -or -not $ridMatch.Success) {
            $failures.Add("Manifest identity is incomplete: $($manifest.FullName)"); continue
        }
        $run = $runMatch.Groups['value'].Value
        $runner = $runnerMatch.Groups['value'].Value
        $rid = $ridMatch.Groups['value'].Value
        if ($run -ne $RunId) { $failures.Add("Manifest run identity mismatch: $($manifest.FullName)") }
        $pair = "$runner|$rid"
        if (-not $pairs.Add($pair)) { $failures.Add("Duplicate runner/RID pair: $pair") }
        $artifactRoot = $manifest.Directory
        $evidenceRoot = $manifest.Directory
        $candidate = $artifactRoot
        while ($null -ne $candidate) {
            if ($candidate.Name -eq 'artifacts') { $artifactRoot = $candidate.Parent; break }
            $candidate = $candidate.Parent
        }
        $textEvidencePaths = @(
            $manifest.FullName,
            (Join-Path $evidenceRoot 'soak-result.json'),
            (Join-Path $evidenceRoot 'news-evidence.json'),
            (Join-Path $evidenceRoot 'trace/trace.circular.log'),
            (Join-Path $evidenceRoot 'trace/yfinance.circular.log')
        )
        foreach ($textEvidencePath in $textEvidencePaths) {
            if (Test-Path -LiteralPath $textEvidencePath -PathType Leaf) {
                $textEvidence = Get-Content -LiteralPath $textEvidencePath -Raw
                if ($textEvidence -match '(?i)(OPENROUTER_API_KEY|DNPPV_OPENROUTER_API_KEY|NVIDIA_API_KEY_CODING)\s*[:=]\s*(?!\[REDACTED\])[^\s,;&]+') { $failures.Add("Retained evidence contains an unredacted secret marker: $textEvidencePath") }
                if ($textEvidence -match '(?i)(authorization\s*:\s*bearer\s+|bearer\s+)[^\s,;&]+') { $failures.Add("Retained evidence contains an unredacted bearer token: $textEvidencePath") }
            }
        }
        $artifactKey = $artifactRoot.Name
        if (-not $artifactKeys.Add($artifactKey)) { $failures.Add("Duplicate artifact directory: $artifactKey") }
        $closure = @(Get-ChildItem -LiteralPath $artifactRoot.FullName -Recurse -Filter 'lane-closure-record.json' -File)
        $review = @(Get-ChildItem -LiteralPath $artifactRoot.FullName -Recurse -Filter 'review-result.json' -File)
        if ($closure.Count -ne 1) { $failures.Add("Expected one lane closure record for $pair, found $($closure.Count)"); continue }
        if ($review.Count -ne 1) { $failures.Add("Expected one semantic review result for $pair, found $($review.Count)"); continue }
        try {
            $record = Read-JsonFile $closure[0].FullName
            $result = Read-JsonFile $review[0].FullName
            foreach ($retainedReceipt in @($closure[0].FullName, $review[0].FullName)) {
                $receiptText = Get-Content -LiteralPath $retainedReceipt -Raw
                if ($receiptText -match '(?i)(OPENROUTER_API_KEY|DNPPV_OPENROUTER_API_KEY|NVIDIA_API_KEY_CODING)\s*[:=]\s*(?!\[REDACTED\])[^\s,;&]+') { $failures.Add("Retained receipt contains an unredacted secret marker: $retainedReceipt") }
                if ($receiptText -match '(?i)(authorization\s*:\s*bearer\s+|bearer\s+)[^\s,;&]+') { $failures.Add("Retained receipt contains an unredacted bearer token: $retainedReceipt") }
            }
            if ($record.schema -ne 'dnppv2-lane-closure-record/v2') { $failures.Add("Lane closure is not v2: $pair") }
            if ($result.schema -ne 'dnppv2-test-artifact-review-result/v2') { $failures.Add("Review result is not v2: $pair") }
            if ($record.status -ne 'complete' -or $record.inspectedEvidenceRetained -ne $true) { $failures.Add("Lane closure is not complete and retained: $pair") }
            if ($record.runId -ne $RunId -or $record.commitSha -ne $CommitSha) { $failures.Add("Lane closure identity mismatch: $pair") }
            if ($record.runner -ne $runner -or $record.rid -ne $rid) { $failures.Add("Lane closure runner/RID mismatch: $pair") }
            if ($result.runId -ne $RunId -or $result.commitSha -ne $CommitSha -or $result.runner -ne $runner -or $result.rid -ne $rid) { $failures.Add("Review result identity mismatch: $pair") }
            if ($result.reviewType -ne 'TEST_ARTIFACT' -or $result.verdict -ne 'PASS' -or $result.reviewComplete -ne $true) { $failures.Add("Review result is not an authoritative PASS: $pair") }
            $blockingFindings = @()
            if ($null -ne $result.blockingFindings) {
                $blockingFindings = @($result.blockingFindings | Where-Object { $null -ne $_ })
            }
            if ($blockingFindings.Count -ne 0) { $failures.Add("Review result contains blocking findings: $pair") }
            $materialHash = Get-Sha256 $manifest.FullName
            if ($result.materialSha256 -ne $materialHash) { $failures.Add("Review material hash mismatch: $pair") }
            $expectedSnapshotId = ('{0}:{1}' -f $CommitSha.ToLowerInvariant(), $materialHash).ToLowerInvariant()
            if ([string]::IsNullOrWhiteSpace([string]$result.snapshotId) -or
                ([string]$result.snapshotId).ToLowerInvariant() -ne $expectedSnapshotId) {
                $failures.Add("Review snapshot reconstruction mismatch: $pair")
            }
            if ([string]::IsNullOrWhiteSpace([string]$record.snapshotId) -or
                ([string]$record.snapshotId).ToLowerInvariant() -ne $expectedSnapshotId) {
                $failures.Add("Closure snapshot reconstruction mismatch: $pair")
            }
            if ($null -ne $record.review -and $record.review.PSObject.Properties.Name -contains 'snapshotId' -and
                ([string]$record.review.snapshotId).ToLowerInvariant() -ne $expectedSnapshotId) {
                $failures.Add("Nested review snapshot reconstruction mismatch: $pair")
            }
            $reviewHash = Get-Sha256 $review[0].FullName
            if ($record.review.resultSha256 -ne $reviewHash) { $failures.Add("Review result hash mismatch: $pair") }
            if ($record.review.materialSha256 -ne $materialHash) { $failures.Add("Closure material hash mismatch: $pair") }
            if ($record.result.outcome -ne 'Passed' -or $record.result.processCleanedUp -ne $true) { $failures.Add("Soak result is not a cleaned-up PASS: $pair") }
            if (@($record.circularTraces).Count -ne 2 -or @($record.screenshots).Count -lt 1) { $failures.Add("Required retained visual/trace evidence is incomplete: $pair") }
            $aiQuotaDispositioned = $record.aiEvidence.aiRequestObserved -eq $true -and
                $record.aiEvidence.aiSuccessObserved -ne $true -and
                $record.aiEvidence.PSObject.Properties.Name -contains 'aiQuotaLimited' -and
                $record.aiEvidence.aiQuotaLimited -eq $true
            if ($record.aiEvidence.aiRequestObserved -ne $true -or
                ($record.aiEvidence.aiSuccessObserved -ne $true -and -not $aiQuotaDispositioned)) {
                $failures.Add("AI evidence is incomplete: $pair")
            }
            foreach ($evidence in @($record.circularTraces) + @($record.screenshots)) {
                $evidencePath = Join-Path $evidenceRoot ([string]$evidence.path)
                if (-not (Test-Path -LiteralPath $evidencePath -PathType Leaf) -or ([string]$evidence.sha256).ToLowerInvariant() -ne (Get-Sha256 $evidencePath)) { $failures.Add("Retained evidence hash/path mismatch: $pair/$($evidence.path)") }
            }
            foreach ($boundEvidence in @(@{ path = $record.result.path; hash = $record.result.sha256 }, @{ path = $record.rssAiEvidence.path; hash = $record.rssAiEvidence.sha256 })) {
                $boundPath = Join-Path $evidenceRoot ([string]$boundEvidence.path)
                if (-not (Test-Path -LiteralPath $boundPath -PathType Leaf) -or ([string]$boundEvidence.hash).ToLowerInvariant() -ne (Get-Sha256 $boundPath)) { $failures.Add("Bound evidence hash/path mismatch: $pair/$($boundEvidence.path)") }
            }
        } catch { $failures.Add("Unreadable or invalid v2 evidence for ${pair}: $($_.Exception.Message)") }
    }
    if ($pairs.Count -ne $LaneCount) { $failures.Add("Expected $LaneCount unique runner/RID pairs, found $($pairs.Count).") }
    if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
    return "HOSTED_SOAK_CLOSURE=Passed;RUN_ID=$RunId;LANES=$LaneCount;REMOTE_REVIEW_CALLS=0"
}

if ($SelfTest) {
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('dnppv2-hosted-closure-selftest-' + [Guid]::NewGuid().ToString('N'))
    try {
        $lane = Join-Path $temp 'artifacts/soak/1-test-linux-x64'
        $reviewRoot = Join-Path $temp 'build/review'
        New-Item -ItemType Directory -Force -Path $lane, (Join-Path $lane 'screenshots'), (Join-Path $lane 'trace'), $reviewRoot | Out-Null
        $manifest = Join-Path $lane 'evidence-review-input.txt'
        Set-Content -LiteralPath $manifest -Value "run_id=1`ncommit_sha=abc`nrunner=test`nrid=linux-x64`ntrace_file=trace.circular.log;`ntrace_file=yfinance.circular.log;`nAiSummarySucceeded`n" -Encoding utf8
        Set-Content -LiteralPath (Join-Path $lane 'soak-result.json') -Value '{"outcome":"Passed","processCleanedUp":true}' -Encoding utf8
        Set-Content -LiteralPath (Join-Path $lane 'news-evidence.json') -Value '{"aiRequestObserved":true,"aiSuccessObserved":true}' -Encoding utf8
        Set-Content -LiteralPath (Join-Path $lane 'screenshots/settled.png') -Value 'screenshot' -Encoding utf8
        Set-Content -LiteralPath (Join-Path $lane 'trace/trace.circular.log') -Value 'trace' -Encoding utf8
        Set-Content -LiteralPath (Join-Path $lane 'trace/yfinance.circular.log') -Value 'yfinance' -Encoding utf8
        $manifestHash = Get-Sha256 $manifest
        $expectedSnapshot = ('abc:{0}' -f $manifestHash).ToLowerInvariant()
        $review = [ordered]@{ schema='dnppv2-test-artifact-review-result/v2'; reviewType='TEST_ARTIFACT'; runId='1'; commitSha='abc'; runner='test'; rid='linux-x64'; snapshotId=$expectedSnapshot; verdict='PASS'; reviewComplete=$true; blockingFindings=@(); materialSha256=$manifestHash }
        $reviewPath = Join-Path $reviewRoot 'review-result.json'
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        $record = [ordered]@{
            schema='dnppv2-lane-closure-record/v2'; runId='1'; commitSha='abc'; runner='test'; rid='linux-x64'; status='complete'; inspectedEvidenceRetained=$true; snapshotId=$expectedSnapshot
            result=[ordered]@{ path='soak-result.json'; sha256=(Get-Sha256 (Join-Path $lane 'soak-result.json')); outcome='Passed'; processCleanedUp=$true }
            rssAiEvidence=[ordered]@{ path='news-evidence.json'; sha256=(Get-Sha256 (Join-Path $lane 'news-evidence.json')) }
            circularTraces=@(@{ path='trace/trace.circular.log'; sha256=(Get-Sha256 (Join-Path $lane 'trace/trace.circular.log')) }, @{ path='trace/yfinance.circular.log'; sha256=(Get-Sha256 (Join-Path $lane 'trace/yfinance.circular.log')) }); screenshots=@(@{ path='screenshots/settled.png'; sha256=(Get-Sha256 (Join-Path $lane 'screenshots/settled.png')) })
            aiEvidence=[ordered]@{ aiRequestObserved=$true; aiSuccessObserved=$true }
            review=[ordered]@{ snapshotId=$expectedSnapshot; materialSha256=$manifestHash; resultSha256=(Get-Sha256 $reviewPath) }
        }
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        $pass = Test-Closure $temp '1' 'abc' 1
        if ($pass -notmatch 'HOSTED_SOAK_CLOSURE=Passed') { throw 'Self-test positive v2 closure did not pass.' }
        $review.blockingFindings = $null
        $nullReviewJson = $review | ConvertTo-Json -Depth 8
        $nullReviewJson = $nullReviewJson -replace '("reviewComplete"\s*:\s*true,)', ('$1' + [Environment]::NewLine + '  "blockingFindings": null,')
        if ($nullReviewJson -notmatch '"blockingFindings"\s*:\s*null') { throw 'Self-test did not serialize explicit null blockingFindings JSON.' }
        Set-Content -LiteralPath $reviewPath -Value $nullReviewJson -Encoding utf8
        $record.review.resultSha256 = Get-Sha256 $reviewPath
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        $pass = Test-Closure $temp '1' 'abc' 1
        if ($pass -notmatch 'HOSTED_SOAK_CLOSURE=Passed') { throw 'Self-test null blockingFindings receipt did not pass.' }
        $record.aiEvidence.aiSuccessObserved = $false
        $record.aiEvidence.aiQuotaLimited = $true
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        $pass = Test-Closure $temp '1' 'abc' 1
        if ($pass -notmatch 'HOSTED_SOAK_CLOSURE=Passed') { throw 'Self-test quota-dispositioned AI evidence did not pass.' }
        $record.aiEvidence.aiQuotaLimited = $false
        $record.aiEvidence.aiSuccessObserved = $true
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        $review.blockingFindings = @()
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        $record.review.resultSha256 = Get-Sha256 $reviewPath
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        $record.schema = 'dnppv2-lane-closure-record/v1'
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test v1 lane closure was accepted.' } catch { if ($_.Exception.Message -notmatch 'Lane closure is not v2') { throw } }
        $record.schema = 'dnppv2-lane-closure-record/v2'
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        $review.schema = 'dnppv2-test-artifact-review-result/v1'
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test v1 review result was accepted.' } catch { if ($_.Exception.Message -notmatch 'Review result is not v2') { throw } }
        $review.schema = 'dnppv2-test-artifact-review-result/v2'
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        $review.snapshotId = 'wrongcommit:wronghash'
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test snapshot mismatch was accepted.' } catch { if ($_.Exception.Message -notmatch 'snapshot reconstruction mismatch') { throw } }
        $review.snapshotId = ('abc:wrong{0}' -f $manifestHash.Substring(5))
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test wrong material snapshot was accepted.' } catch { if ($_.Exception.Message -notmatch 'snapshot reconstruction mismatch') { throw } }
        $review.snapshotId = ('wrongcommit:{0}' -f $manifestHash)
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test wrong commit snapshot was accepted.' } catch { if ($_.Exception.Message -notmatch 'snapshot reconstruction mismatch') { throw } }
        $review.snapshotId = $expectedSnapshot
        $record.snapshotId = 'wrongcommit:wronghash'
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test closure snapshot disagreement was accepted.' } catch { if ($_.Exception.Message -notmatch 'Closure snapshot reconstruction mismatch') { throw } }
        $record.snapshotId = 'wrongcommit:wronghash'
        $review.snapshotId = 'wrongcommit:wronghash'
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test mutually consistent wrong snapshot was accepted.' } catch { if ($_.Exception.Message -notmatch 'snapshot reconstruction mismatch') { throw } }
        $record.snapshotId = $expectedSnapshot
        $review.snapshotId = $expectedSnapshot
        $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $reviewRoot 'lane-closure-record.json') -Encoding utf8
        $review.snapshotId = $expectedSnapshot
        $review.runId = 'wrong-run'
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test identity mismatch was accepted.' } catch { if ($_.Exception.Message -notmatch 'Review result identity mismatch') { throw } }
        $review.runId = '1'
        $review.blockingFindings = @(@{ id = 'BLOCKER' })
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test blocking finding was accepted.' } catch { if ($_.Exception.Message -notmatch 'blocking findings') { throw } }
        $review.blockingFindings = @()
        $review.extra = 'result-hash-change'
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test result hash mismatch was accepted.' } catch { if ($_.Exception.Message -notmatch 'Review result hash mismatch') { throw } }
        $review.PSObject.Properties.Remove('extra')
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        $review.verdict = 'FAIL'
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test non-PASS case was accepted.' } catch { if ($_.Exception.Message -notmatch 'authoritative PASS') { throw } }
        $review.verdict = 'PASS'
        $review | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reviewPath -Encoding utf8
        Add-Content -LiteralPath $manifest -Value 'tampered=true'
        try { Test-Closure $temp '1' 'abc' 1; throw 'Self-test material hash case was accepted.' } catch { if ($_.Exception.Message -notmatch 'material hash') { throw } }
        try { Test-Closure $temp '1' 'abc' 2; throw 'Self-test missing-lane case was accepted.' } catch { if ($_.Exception.Message -notmatch 'Expected 2 soak evidence manifests') { throw } }
        'HOSTED_SOAK_CLOSURE_SELFTEST=Passed'
    } finally { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
    exit 0
}

foreach ($required in @('ArtifactRoot', 'ExpectedRunId', 'ExpectedCommitSha')) {
    if ([string]::IsNullOrWhiteSpace([string](Get-Variable -Name $required -ValueOnly))) { throw "Missing required parameter: $required" }
}
if ($ExpectedLaneCount -lt 1) { throw 'ExpectedLaneCount must be positive.' }
Test-Closure $ArtifactRoot $ExpectedRunId $ExpectedCommitSha $ExpectedLaneCount
