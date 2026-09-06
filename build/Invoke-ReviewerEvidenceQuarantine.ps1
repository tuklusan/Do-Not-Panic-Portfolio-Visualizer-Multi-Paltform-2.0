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

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ReviewRoot,
    [Parameter(Mandatory = $true)][string]$ArtifactRoot,
    [Parameter(Mandatory = $true)][string]$RunnerTemp,
    [Parameter(Mandatory = $true)][string]$RunId,
    [Parameter(Mandatory = $true)][string]$CommitSha,
    [Parameter(Mandatory = $true)][string]$Runner,
    [Parameter(Mandatory = $true)][string]$Rid,
    [Parameter(Mandatory = $true)][string]$Reason
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-FullPath([string]$Path) { return [IO.Path]::GetFullPath($Path) }
function Is-UnderPath([string]$Path, [string]$Parent) {
    $normalizedPath = (Get-FullPath $Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $normalizedParent = (Get-FullPath $Parent).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    return $normalizedPath.StartsWith($normalizedParent, [StringComparison]::OrdinalIgnoreCase)
}

$reviewFull = Get-FullPath $ReviewRoot
$artifactFull = Get-FullPath $ArtifactRoot
$runnerTempFull = Get-FullPath $RunnerTemp
$repoRoot = (& git rev-parse --show-toplevel 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot)) { throw 'Cannot resolve repository root for quarantine validation.' }
$quarantineRoot = Join-Path $runnerTempFull ('dnppv2-review-quarantine-' + [Guid]::NewGuid().ToString('N'))
if (Is-UnderPath $quarantineRoot $repoRoot -or Is-UnderPath $quarantineRoot $artifactFull) {
    throw 'Quarantine destination is inside the repository or artifact upload root.'
}

$originalFiles = @()
if (Test-Path -LiteralPath $reviewFull -PathType Container) {
    $originalFiles = @(Get-ChildItem -LiteralPath $reviewFull -Recurse -File -ErrorAction Stop | ForEach-Object {
        $_.FullName.Substring($reviewFull.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    })
}
New-Item -ItemType Directory -Force -Path $quarantineRoot | Out-Null
if (Test-Path -LiteralPath $reviewFull -PathType Container) { Move-Item -LiteralPath $reviewFull -Destination $quarantineRoot -Force }
$quarantinedReviewRoot = Join-Path $quarantineRoot ([IO.Path]::GetFileName($reviewFull))
if (Test-Path -LiteralPath $reviewFull) { throw 'Reviewer quarantine removal verification failed: original review path still exists.' }
foreach ($originalFile in $originalFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $quarantinedReviewRoot $originalFile) -PathType Leaf)) { throw 'Reviewer quarantine move verification failed.' }
}
New-Item -ItemType Directory -Force -Path $reviewFull | Out-Null
$failure = [ordered]@{
    schema = 'dnppv2-review-evidence-failure/v1'
    runId = $RunId
    commitSha = $CommitSha
    runner = $Runner
    rid = $Rid
    category = 'review-evidence-contaminated'
    status = 'review-incomplete'
    inspectedEvidenceRetained = $false
    authoritative = $false
    failure = 'Reviewer evidence quarantined because credential-shaped data was detected.'
}
$failure | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reviewFull 'review-evidence-failure.json') -Encoding utf8
throw "Reviewer evidence quarantined before artifact upload: $Reason"
