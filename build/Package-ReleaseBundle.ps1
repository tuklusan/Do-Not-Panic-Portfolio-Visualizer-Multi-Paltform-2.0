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
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory,

    [Parameter(Mandatory = $true)]
    [string]$Rid,

    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $ReleaseDirectory) 'packages')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$source = [IO.Path]::GetFullPath($ReleaseDirectory)
if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Release directory does not exist: $source"
}
if (-not (Test-Path -LiteralPath (Join-Path $source 'release-manifest.json') -PathType Leaf)) {
    throw "Release manifest is required before packaging: $source"
}

$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null
$archive = Join-Path $output ("dnppv2-2.0-{0}.zip" -f $Rid)
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }

Compress-Archive -Path (Join-Path $source '*') -DestinationPath $archive -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
$checksum = Join-Path $output ("dnppv2-2.0-{0}.zip.sha256" -f $Rid)
Set-Content -LiteralPath $checksum -Value ("{0} *{1}" -f $hash, [IO.Path]::GetFileName($archive)) -Encoding utf8
Write-Output "RELEASE_BUNDLE_PACKAGED=Passed;RID=$Rid;ARCHIVE=$archive;SHA256=$hash"
