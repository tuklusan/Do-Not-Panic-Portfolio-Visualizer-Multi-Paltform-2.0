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
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-ReuseInputs([string]$RunId, [string]$CommitSha) {
    if ($RunId -notmatch '^[1-9][0-9]*$') { throw 'Evidence reuse requires a completed numeric prior run ID.' }
    if ($CommitSha -notmatch '^[0-9a-fA-F]{40}$') { throw 'Evidence reuse requires the prior run commit SHA.' }
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
