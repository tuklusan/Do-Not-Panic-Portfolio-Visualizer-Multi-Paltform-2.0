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
    [Parameter()]
    [string]$InventoryPath = (Join-Path $PSScriptRoot 'vm/remote-test-machines.local.txt'),

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactRoot,

    [Parameter()]
    [ValidateRange(1, 30)]
    [int]$TimeoutSeconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $InventoryPath -PathType Leaf)) {
    throw "Local lab inventory is missing: $InventoryPath"
}

$records = [Collections.Generic.List[object]]::new()
foreach ($line in Get-Content -LiteralPath $InventoryPath) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
        continue
    }

    $parts = $trimmed.Split('|', 3)
    if ($parts.Count -ne 3 -or $parts | Where-Object { [string]::IsNullOrWhiteSpace($_) }) {
        throw "Malformed local lab inventory entry: $trimmed"
    }

    $name, $user, $address = $parts
    $reachable = Test-NetConnection -ComputerName $address -Port 22 -InformationLevel Quiet -WarningAction SilentlyContinue
    $records.Add([ordered]@{
        name = $name
        user = $user
        address = $address
        sshPort = 22
        reachable = [bool]$reachable
        status = if ($reachable) { 'AvailableForCycle' } else { 'UnavailableAtCycleStart' }
        checkedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    })
}

$resolvedArtifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
New-Item -ItemType Directory -Path $resolvedArtifactRoot -Force | Out-Null
$outputPath = Join-Path $resolvedArtifactRoot 'local-lab-availability.json'
[ordered]@{
    schema = 'dnppv2-local-lab-availability/v1'
    checkedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    machines = @($records)
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outputPath -Encoding utf8

Write-Output "LOCAL_LAB_AVAILABILITY=Recorded;ARTIFACT=$outputPath;AVAILABLE=$(@($records | Where-Object reachable).Count);UNAVAILABLE=$(@($records | Where-Object { -not $_.reachable }).Count)"
