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
    [Parameter(Mandatory = $false)]
    [ValidateSet('fresh-matrix', 'reuse-completed-run')]
    [string]$Mode = 'fresh-matrix',

    [string]$PriorRunId = '',
    [string]$PriorCommitSha = '',
    [string[]]$ChangedPath = @(),
    [string]$CandidateSha = '',
    [string]$EvidenceCandidateSha = '',
    [string]$EvidencePath = '',
    [switch]$PathOnly,
    [switch]$AdmissionSelfTest,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-ReuseInputs([string]$RunId, [string]$CommitSha) {
    if ($RunId -notmatch '^[1-9][0-9]*$') { throw 'Evidence reuse requires a completed numeric prior run ID.' }
    if ($CommitSha -notmatch '^[0-9a-fA-F]{40}$') { throw 'Evidence reuse requires the prior run commit SHA.' }
}

function Test-Sha([string]$Sha, [string]$Name) {
    if ($Sha -notmatch '^[0-9a-fA-F]{40}$') { throw "$Name must be a 40-character Git SHA." }
}

function Test-MatrixNeutralPath([string]$Path) {
    $normalized = $Path.Replace('\', '/').TrimStart('./')
    return $normalized -eq 'docs/AUDIT_STATE.json' -or $normalized -match '^docs/CR-[0-9]{3}(-[A-Z0-9-]+)?\.md$'
}

function Test-ReusableAdmission([string[]]$Paths, [string]$Candidate, [string]$EvidenceCandidate, [string]$EvidenceFile) {
    Test-Sha $Candidate 'CandidateSha'
    Test-Sha $EvidenceCandidate 'EvidenceCandidateSha'
    if ($Candidate -cne $EvidenceCandidate) { throw 'MATRIX REQUIRED: candidate identity does not match evidence identity.' }
    if (@($Paths).Count -eq 0) { throw 'MATRIX REQUIRED: changed-path classification is missing.' }
    foreach ($path in $Paths) {
        if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-MatrixNeutralPath $path)) {
            throw "MATRIX REQUIRED: non-neutral or unknown changed path: $path"
        }
    }
    if ([string]::IsNullOrWhiteSpace($EvidenceFile) -or -not (Test-Path -LiteralPath $EvidenceFile -PathType Leaf)) {
        throw 'MATRIX REQUIRED: completed exact evidence is missing.'
    }
    $evidence = Get-Content -LiteralPath $EvidenceFile -Raw | ConvertFrom-Json
    if ($evidence.valid -ne $true -or $evidence.completed -ne $true -or $evidence.candidateSha -cne $EvidenceCandidate -or $evidence.aggregate -ne 'Passed') {
        throw 'MATRIX REQUIRED: completed exact evidence is corrupt, incomplete, or mismatched.'
    }
    return $true
}

if ($AdmissionSelfTest) {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('dnppv2-matrix-policy-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    try {
        $sha = 'a' * 40
        $evidence = Join-Path $root 'evidence.json'
        '{"valid":true,"completed":true,"candidateSha":"' + $sha + '","aggregate":"Passed"}' | Set-Content -LiteralPath $evidence -Encoding utf8NoBOM
        $neutral = @('docs/AUDIT_STATE.json', 'docs/CR-118-REUSE-COMPLETED-MATRIX-EVIDENCE.md')
        Test-ReusableAdmission $neutral $sha $sha $evidence | Out-Null
        $required = @(
            @('src/Product.cs'), @('tests/ProductTests.cs'), @('src/App.csproj'), @('build/Test-Harness.ps1'),
            @('.github/workflows/publish-six-rids.yml'), @('unknown/path.txt')
        )
        foreach ($paths in $required) {
            try { Test-ReusableAdmission $paths $sha $sha $evidence; throw 'MATRIX_REQUIRED case was accepted.' } catch { if ($_.Exception.Message -eq 'MATRIX_REQUIRED case was accepted.') { throw } }
        }
        try { Test-ReusableAdmission $neutral $sha ('b' * 40) $evidence; throw 'Candidate mismatch was accepted.' } catch { if ($_.Exception.Message -eq 'Candidate mismatch was accepted.') { throw } }
        $missing = Join-Path $root 'missing.json'
        try { Test-ReusableAdmission $neutral $sha $sha $missing; throw 'Missing evidence was accepted.' } catch { if ($_.Exception.Message -eq 'Missing evidence was accepted.') { throw } }
        '{"valid":false,"completed":true,"candidateSha":"' + $sha + '","aggregate":"Passed"}' | Set-Content -LiteralPath $evidence -Encoding utf8NoBOM
        try { Test-ReusableAdmission $neutral $sha $sha $evidence; throw 'Corrupt evidence was accepted.' } catch { if ($_.Exception.Message -eq 'Corrupt evidence was accepted.') { throw } }
        Write-Output 'MATRIX_ADMISSION_SELFTEST=Passed'
    }
    finally {
        if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
    }
    exit 0
}

if ($ChangedPath.Count -gt 0 -or $CandidateSha -or $EvidenceCandidateSha -or $EvidencePath) {
    if ($Mode -ne 'reuse-completed-run') { throw 'MATRIX REQUIRED: admission validation is only valid for reuse-completed-run.' }
    if ($PathOnly) {
        Test-Sha $CandidateSha 'CandidateSha'
        Test-Sha $EvidenceCandidateSha 'EvidenceCandidateSha'
        if ($CandidateSha -cne $EvidenceCandidateSha) { throw 'MATRIX REQUIRED: candidate identity does not match evidence identity.' }
        if (@($ChangedPath).Count -eq 0) { throw 'MATRIX REQUIRED: changed-path classification is missing.' }
        foreach ($path in $ChangedPath) {
            if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-MatrixNeutralPath $path)) {
                throw "MATRIX REQUIRED: non-neutral or unknown changed path: $path"
            }
        }
        Write-Output 'MATRIX_ADMISSION=PathOnlyReusable'
        exit 0
    }
    Test-ReusableAdmission $ChangedPath $CandidateSha $EvidenceCandidateSha $EvidencePath | Out-Null
    Write-Output 'MATRIX_ADMISSION=Reusable'
    exit 0
}

if ($SelfTest) {
    Test-ReuseInputs '34129921207' ('a' * 40)
    try { Test-ReuseInputs '' ('a' * 40); throw 'Missing run ID was accepted.' } catch { if ($_.Exception.Message -eq 'Missing run ID was accepted.') { throw } }
    try { Test-ReuseInputs '34129921207' 'bad'; throw 'Malformed SHA was accepted.' } catch { if ($_.Exception.Message -eq 'Malformed SHA was accepted.') { throw } }
    Write-Output 'MATRIX_EVIDENCE_REUSE_POLICY_SELFTEST=Passed'
    exit 0
}

if ($Mode -eq 'reuse-completed-run') {
    Test-ReuseInputs $PriorRunId $PriorCommitSha
    Write-Output "MATRIX_EVIDENCE_MODE=ReuseCompletedRun;RUN_ID=$PriorRunId;COMMIT_SHA=$PriorCommitSha"
    exit 0
}

if (-not [string]::IsNullOrWhiteSpace($PriorRunId) -or -not [string]::IsNullOrWhiteSpace($PriorCommitSha)) {
    throw 'Fresh matrix mode must not carry prior evidence identity.'
}
Write-Output 'MATRIX_EVIDENCE_MODE=FreshMatrix'
