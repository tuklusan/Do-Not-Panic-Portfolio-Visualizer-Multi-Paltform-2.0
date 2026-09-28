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

    [string]$ProductVersion = '2.0',

    [string]$ProductName = 'DO NOT PANIC PORTFOLIO VISUALIZER'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath($ReleaseDirectory)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "Release directory does not exist: $root"
}

$manifestPath = Join-Path $root 'release-manifest.json'
$rootPrefix = if ($root.EndsWith([IO.Path]::DirectorySeparatorChar)) { $root } else { $root + [IO.Path]::DirectorySeparatorChar }
$files = @(
    Get-ChildItem -LiteralPath $root -File -Recurse |
        Where-Object { $_.FullName -ne $manifestPath } |
        Sort-Object FullName
)
if ($files.Count -eq 0) {
    throw "Release directory contains no files: $root"
}

$entries = foreach ($file in $files) {
    $relative = $file.FullName.Substring($rootPrefix.Length).Replace('\', '/')
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    [ordered]@{
        path = $relative
        sizeBytes = [int64]$file.Length
        sha256 = $hash
    }
}

$document = [ordered]@{
    schemaVersion = 1
    productName = $ProductName
    productVersion = $ProductVersion
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('o')
    files = @($entries)
}

$json = $document | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText($manifestPath, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Output "RELEASE_MANIFEST_GENERATED=Passed;FILES=$($entries.Count);PATH=$manifestPath"
