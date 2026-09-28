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
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $message = if ($output) { ($output | Out-String).Trim() } else { "git $($Arguments -join ' ') failed." }
        throw $message
    }

    return ($output | Out-String).Trim()
}

$repoRoot = Invoke-Git -Arguments @('rev-parse', '--show-toplevel')
if ([string]::IsNullOrWhiteSpace($repoRoot)) {
    throw 'Could not resolve repository root.'
}

$upstreamRef = Invoke-Git -Arguments @('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{u}')
if ([string]::IsNullOrWhiteSpace($upstreamRef)) {
    throw 'The current branch does not have a configured upstream.'
}

$status = Invoke-Git -Arguments @('status', '--porcelain=v1', '--untracked-files=normal')
if (-not [string]::IsNullOrWhiteSpace($status)) {
    throw "Validation checkpoint requires a clean worktree. Pending changes:`n$status"
}

$head = Invoke-Git -Arguments @('rev-parse', 'HEAD')
$upstreamHead = Invoke-Git -Arguments @('rev-parse', $upstreamRef)
if ($head -ne $upstreamHead) {
    throw "Validation checkpoint requires HEAD to match upstream. HEAD=$head UPSTREAM=$upstreamHead REF=$upstreamRef"
}

Write-Output "VALIDATION_CHECKPOINT=Passed;UPSTREAM=$upstreamRef;HEAD=$head"
