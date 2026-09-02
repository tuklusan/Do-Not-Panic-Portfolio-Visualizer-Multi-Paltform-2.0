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
    [ValidateNotNullOrEmpty()]
    [string]$DownloadUrl = 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.11/windowsdesktop-runtime-10.0.11-win-x64.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$installer = Join-Path $env:TEMP 'dnppv2-windowsdesktop-runtime.exe'
try {
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $installer -UseBasicParsing
    $process = Start-Process -FilePath $installer -ArgumentList @('/install', '/quiet', '/norestart') -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Desktop Runtime installer failed with exit code $($process.ExitCode)."
    }
}
finally {
    Remove-Item -LiteralPath $installer -Force -ErrorAction SilentlyContinue
}

Write-Output 'DOTNET_DESKTOP_RUNTIME_INSTALLATION=Passed'
