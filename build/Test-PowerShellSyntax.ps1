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
    [ValidateNotNullOrEmpty()]
    [string[]]$Path,

    [Parameter(ParameterSetName = 'Command', Mandatory = $true)]
    [string]$CommandText,

    [Parameter(ParameterSetName = 'Repository')]
    [switch]$Repository,

    [Parameter(ParameterSetName = 'Command')]
    [switch]$AllowCmdShell
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($PSCmdlet.ParameterSetName -eq 'Command' -and [string]::IsNullOrWhiteSpace($CommandText)) {
    throw '-CommandText must contain a non-whitespace PowerShell command.'
}

function Assert-PowerShellText {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Label,
        [bool]$ReviewCommandSafety
    )

    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseInput(
        $Text,
        $Label,
        [ref]$tokens,
        [ref]$parseErrors)

    if ($parseErrors.Count -gt 0) {
        $details = @($parseErrors | ForEach-Object {
            '{0}:{1}:{2}: {3}' -f $Label, $_.Extent.StartLineNumber, $_.Extent.StartColumnNumber, $_.Message
        }) -join [Environment]::NewLine
        throw "PowerShell syntax validation failed:$([Environment]::NewLine)$details"
    }

    if (-not $ReviewCommandSafety) {
        return
    }

    $commands = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true))
    foreach ($command in $commands) {
        $name = $command.GetCommandName()
        if ([string]::IsNullOrWhiteSpace($name)) {
            continue
        }

        if ($name.Equals('Invoke-Expression', [StringComparison]::OrdinalIgnoreCase) -or
            $name.Equals('iex', [StringComparison]::OrdinalIgnoreCase)) {
            throw "PowerShell command safety validation rejected '$name'. Use a script block or structured argument array."
        }

        if (($name.Equals('cmd', [StringComparison]::OrdinalIgnoreCase) -or
             $name.Equals('cmd.exe', [StringComparison]::OrdinalIgnoreCase)) -and
            -not $AllowCmdShell) {
            throw "PowerShell command safety validation rejected a cmd.exe shell hop. Use native PowerShell/structured arguments, or rerun syntax review with -AllowCmdShell after inspecting the exact command."
        }
    }
}

function Find-RepositoryRootFromPath {
    param([Parameter(Mandatory = $true)][string]$StartPath)

    try {
        $current = Get-Item -LiteralPath $StartPath -ErrorAction Stop
    }
    catch {
        throw "Could not inspect repository-root start path '$StartPath': $($_.Exception.Message)"
    }

    if (-not $current.PSIsContainer) {
        $current = $current.Directory
    }

    $depth = 0
    while ($null -ne $current -and $depth -le 20) {
        $hasGitDirectory = Test-Path -LiteralPath (Join-Path $current.FullName '.git') -PathType Container
        $hasAgentsFile = Test-Path -LiteralPath (Join-Path $current.FullName 'AGENTS.md') -PathType Leaf
        $hasGateLayout =
            (Test-Path -LiteralPath (Join-Path $current.FullName 'build\Test-PowerShellSyntax.ps1') -PathType Leaf) -and
            (Test-Path -LiteralPath (Join-Path $current.FullName 'docs\FRESH-PROJECT-DEEPSEEK-REVIEW-GATE.md') -PathType Leaf)
        if ($hasGitDirectory -or $hasAgentsFile -or $hasGateLayout) {
            return $current.FullName
        }

        $current = $current.Parent
        $depth++
    }

    throw "Could not locate repository root from '$StartPath' within 20 parent directories; expected a .git directory or the clean-slate review-gate layout."
}

function Test-IsArchivedSnapshotScriptExcluded {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $normalized = $RelativePath.Replace('\', '/').TrimStart('/')
    if ($normalized -match '(?i)(^|/)(artifacts|bin|node_modules|obj|out|packages)(/|$)') {
        return $true
    }

    if ($normalized -match '(?i)^build/(debug|output|release)/') {
        return $true
    }

    $excludedPrefixes = @(
        '.git/',
        '.vs/',
        'build/deepseek-review/',
        'build/validation/artifacts/',
        'build/vm/artifacts/'
    )
    foreach ($prefix in $excludedPrefixes) {
        if ($normalized.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

$items = New-Object System.Collections.Generic.List[object]
if ($PSCmdlet.ParameterSetName -eq 'Command') {
    [void]$items.Add([pscustomobject]@{ Label = '<command>'; Text = $CommandText; ReviewSafety = $true })
}
else {
    if ($PSCmdlet.ParameterSetName -eq 'Path') {
        $paths = @($Path)
    }
    else {
        $repoRoot = $null
        try {
            $gitRootOutput = & git rev-parse --show-toplevel 2>$null
            if ($LASTEXITCODE -eq 0) {
                $repoRoot = ($gitRootOutput -join [Environment]::NewLine).Trim()
            }
        }
        catch {
            $repoRoot = $null
        }

        if (-not [string]::IsNullOrWhiteSpace($repoRoot)) {
            Push-Location $repoRoot
            try {
                $paths = @(
                    & git ls-files -- '*.ps1'
                    & git ls-files --others --exclude-standard -- '*.ps1'
                ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique | ForEach-Object {
                    Join-Path $repoRoot $_
                }
            }
            finally {
                Pop-Location
            }
        }
        else {
            $repoRoot = Find-RepositoryRootFromPath -StartPath $PSScriptRoot
            $paths = Get-ChildItem -LiteralPath $repoRoot -Recurse -File -Filter '*.ps1' -ErrorAction SilentlyContinue |
                Where-Object {
                    $relative = $_.FullName.Substring($repoRoot.Length)
                    -not (Test-IsArchivedSnapshotScriptExcluded -RelativePath $relative)
                } |
                Sort-Object FullName -Unique |
                ForEach-Object {
                    $_.FullName
                }
        }
    }

    foreach ($candidate in $paths) {
        $resolved = (Resolve-Path -LiteralPath $candidate -ErrorAction Stop).Path
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "PowerShell syntax path is not a file: $candidate"
        }

        [void]$items.Add([pscustomobject]@{
            Label = $resolved
            Text = Get-Content -Raw -LiteralPath $resolved
            ReviewSafety = $true
        })
    }
}

foreach ($item in $items) {
    Assert-PowerShellText -Text $item.Text -Label $item.Label -ReviewCommandSafety $item.ReviewSafety
}

Write-Output "POWERSHELL_SYNTAX_VALIDATION=Passed;COUNT=$($items.Count)"
