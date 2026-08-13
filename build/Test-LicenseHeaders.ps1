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
[CmdletBinding(DefaultParameterSetName = 'Repository')]
param(
    [Parameter(ParameterSetName = 'Path', Mandatory = $true)]
    [string[]]$Path,

    [Parameter(ParameterSetName = 'Repository')]
    [switch]$Repository
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$requiredHeaderFragments = @(
    'Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.',
    'This file is governed by the SANYALnet Labs Non-Commercial License in the',
    'Attribution is required: "Based on original work by Supratim Sanyal of',
    'SANYALnet Labs."'
)

$jsonLicenseNotice = 'Based on original work by Supratim Sanyal of SANYALnet Labs.'
$jsonCopyright = 'Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.'

function Get-RepoRoot {
    $root = & git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
        throw 'git repository root could not be resolved.'
    }

    return $root.Trim()
}

function Test-IsCandidateArtifact {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $normalized = $RelativePath.Replace('\', '/').TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($normalized)) {
        return $false
    }

    if ($normalized.StartsWith('.git/', [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith('build/deepseek-review/', [StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    if ($normalized.Equals('.gitignore', [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith('.githooks/', [StringComparison]::OrdinalIgnoreCase)) {
        return $true
    }

    $extension = [IO.Path]::GetExtension($normalized).ToLowerInvariant()
    return $extension -in @('.md', '.ps1', '.json')
}

function Assert-CommentHeader {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$PathLabel,
        [Parameter(Mandatory = $true)][string]$OpeningMarker
    )

    $prefix = $Text.Substring(0, [Math]::Min(2000, $Text.Length))
    if (-not $prefix.TrimStart().StartsWith($OpeningMarker, [StringComparison]::Ordinal)) {
        throw "$PathLabel is missing the required leading license header marker '$OpeningMarker'."
    }

    foreach ($fragment in $requiredHeaderFragments) {
        if ($prefix.IndexOf($fragment, [StringComparison]::Ordinal) -lt 0) {
            throw "$PathLabel is missing a required license header fragment: $fragment"
        }
    }
}

function Assert-JsonHeader {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$PathLabel
    )

    try {
        $parsed = $Text | ConvertFrom-Json -AsHashtable -ErrorAction Stop
    }
    catch {
        throw "$PathLabel is not valid JSON."
    }

    if ($parsed.Keys -notcontains 'license_notice' -or
        [string]$parsed['license_notice'] -ne $jsonLicenseNotice) {
        throw "$PathLabel is missing the required JSON license_notice field."
    }

    if ($parsed.Keys -notcontains 'copyright' -or
        [string]$parsed['copyright'] -ne $jsonCopyright) {
        throw "$PathLabel is missing the required JSON copyright field."
    }
}

$repoRoot = Get-RepoRoot

if ($PSCmdlet.ParameterSetName -eq 'Path') {
    $candidates = @($Path)
}
else {
    $candidates = @(
        & git ls-files
        & git ls-files --others --exclude-standard
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique
}

$validatedCount = 0
foreach ($candidate in $candidates) {
    $relativePath = ($candidate -replace '\\', '/').Trim()
    if (-not (Test-IsCandidateArtifact -RelativePath $relativePath)) {
        continue
    }

    $literalPath = if ([IO.Path]::IsPathRooted($candidate)) { $candidate } else { Join-Path $repoRoot $candidate }
    if (-not (Test-Path -LiteralPath $literalPath -PathType Leaf)) {
        throw "License-header validation target is not a file: $candidate"
    }

    $text = Get-Content -Raw -LiteralPath $literalPath
    $extension = [IO.Path]::GetExtension($literalPath).ToLowerInvariant()

    switch ($extension) {
        '.json' { Assert-JsonHeader -Text $text -PathLabel $relativePath }
        '.md' { Assert-CommentHeader -Text $text -PathLabel $relativePath -OpeningMarker '<!--' }
        '.ps1' { Assert-CommentHeader -Text $text -PathLabel $relativePath -OpeningMarker '# ' }
        default {
            if ($relativePath.Equals('.gitignore', [StringComparison]::OrdinalIgnoreCase) -or
                $relativePath.StartsWith('.githooks/', [StringComparison]::OrdinalIgnoreCase)) {
                Assert-CommentHeader -Text $text -PathLabel $relativePath -OpeningMarker '#'
            }
        }
    }

    $validatedCount++
}

Write-Output "LICENSE_HEADER_VALIDATION=Passed;COUNT=$validatedCount"
