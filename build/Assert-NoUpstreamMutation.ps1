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
    [string]$RemoteName,
    [string]$RemoteUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$upstreamPattern = '(?i)(^https://github\.com/|^http://github\.com/|^git@github\.com:|^ssh://git@github\.com/)?tuklusan/DO-NOT-PANIC-PORTFOLIO-VISUALIZER(\.git)?$'

function Test-IsProtectedUpstreamUrl {
    param([Parameter(Mandatory = $true)][string]$Url)

    return $Url.Trim() -match $upstreamPattern
}

$violations = New-Object System.Collections.Generic.List[string]

if (-not [string]::IsNullOrWhiteSpace($RemoteUrl)) {
    if (Test-IsProtectedUpstreamUrl -Url $RemoteUrl) {
        $label = if ([string]::IsNullOrWhiteSpace($RemoteName)) { '<unnamed-remote>' } else { $RemoteName }
        [void]$violations.Add("Push target '$label' resolves to the protected upstream repository.")
    }
}
else {
    $remoteLines = @(& git remote -v)
    if ($LASTEXITCODE -ne 0) {
        throw 'git remote -v failed while checking the upstream push lock.'
    }

    foreach ($line in $remoteLines) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line -notmatch '\(push\)\s*$') {
            continue
        }

        $parts = $line -split '\s+'
        if ($parts.Count -lt 2) {
            continue
        }

        if (Test-IsProtectedUpstreamUrl -Url $parts[1]) {
            [void]$violations.Add("Configured push remote '$($parts[0])' resolves to the protected upstream repository.")
        }
    }
}

if ($violations.Count -gt 0) {
    throw (($violations -join ' ') + ' Upstream mutations are forbidden in this migration workspace.')
}

Write-Output 'UPSTREAM_MUTATION_GUARD=Passed'
