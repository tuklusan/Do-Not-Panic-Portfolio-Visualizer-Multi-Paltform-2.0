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

function Get-CodeReviewSha256 {
    param([Parameter(Mandatory)][string]$Text)
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    return ([Security.Cryptography.SHA256]::HashData($bytes) | ForEach-Object ToString x2) -join ''
}

function Get-CodeReviewGitValue {
    param([Parameter(Mandatory)][string]$ObjectId, [Parameter(Mandatory)][string]$Suffix)
    $value = (& git show "$ObjectId$Suffix" 2>$null | Out-String).TrimEnd("`r", "`n")
    if ($LASTEXITCODE -ne 0) { throw "Git object lookup failed: $ObjectId$Suffix" }
    return $value
}

function New-CodeReviewSnapshotDescriptor {
    param(
        [Parameter(Mandatory)][string]$BaseSha,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$Scope
    )
    $baseTree = (& git rev-parse "$BaseSha^{tree}").Trim()
    $headTree = (& git rev-parse "$HeadSha^{tree}").Trim()
    if ($LASTEXITCODE -ne 0 -or $baseTree -notmatch '^[0-9a-f]{40}$' -or $headTree -notmatch '^[0-9a-f]{40}$') {
        throw 'Cannot resolve candidate Git trees.'
    }
    $paths = @(& git diff --name-only --diff-filter=ACDMRTUXB $BaseSha $HeadSha)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate candidate changed files.' }
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('schema=dnppv2-committed-candidate/v1')
    $lines.Add("base=$BaseSha")
    $lines.Add("head=$HeadSha")
    $lines.Add("base_tree=$baseTree")
    $lines.Add("head_tree=$headTree")
    $lines.Add("scope=$Scope")
    foreach ($path in ($paths | Sort-Object)) {
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$path))
        $blob = (& git rev-parse "$HeadSha`:$path" 2>$null).Trim()
        if ($LASTEXITCODE -ne 0) { $blob = 'DELETED' }
        $lines.Add("path=$encoded;blob=$blob")
    }
    return (($lines -join "`n") + "`n")
}
