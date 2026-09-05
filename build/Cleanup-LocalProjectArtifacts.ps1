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

$repositoryRoot = Split-Path -Parent $PSScriptRoot

# The prefix is unique to this project's disposable local validation outputs.
Get-ChildItem -LiteralPath $env:TEMP -Directory -Force |
    Where-Object { $_.Name -like 'dnppv2-*' } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }

foreach ($relativePath in @(
    'artifacts',
    'build/nvidia-review',
    'build/publish',
    'build/local-probe',
    'build/vm-artifacts'
)) {
    $path = Join-Path $repositoryRoot $relativePath
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'build') -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'local-cycle-*' } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }

Get-ChildItem -LiteralPath $repositoryRoot -Directory -Recurse -Force |
    Where-Object { $_.Name -in @('bin', 'obj') } |
    Sort-Object FullName -Descending |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }

Write-Output 'LOCAL_PROJECT_ARTIFACT_CLEANUP=Passed'
