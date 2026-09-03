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
    [ValidateNotNullOrEmpty()]
    [string]$ExecutablePath,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 240)]
    [int]$DurationMinutes,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactRoot,

    [Parameter()]
    [string]$ScreenshotPath,

    [Parameter()]
    [ValidateRange(0, 240)]
    [int]$ScreenshotIntervalMinutes = 0,

    [Parameter()]
    [string]$LocalDataRoot,

    [Parameter()]
    [string]$OpenRouterApiKey = $(if ($env:DNPPV_OPENROUTER_API_KEY) { $env:DNPPV_OPENROUTER_API_KEY } elseif ($env:OPENROUTER_API_KEY) { $env:OPENROUTER_API_KEY } else { $env:OPENROUTER_AI_API_KEY }),

    [Parameter()]
    [string[]]$ArgumentList = @('--windowed=1280x800'),

    [Parameter()]
    [ValidateRange(1, 60)]
    [int]$PollIntervalSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedExecutable = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
$resolvedArtifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
$resolvedDataRoot = if ([string]::IsNullOrWhiteSpace($LocalDataRoot)) {
    Join-Path $resolvedArtifactRoot 'local-data'
} else {
    [IO.Path]::GetFullPath($LocalDataRoot)
}
$traceSource = Join-Path $resolvedDataRoot 'Trace'
$traceDestination = Join-Path $resolvedArtifactRoot 'trace'
$resultPath = Join-Path $resolvedArtifactRoot 'soak-result.json'
$startedAt = [DateTimeOffset]::UtcNow
$process = $null
$outcome = 'Failed'
$failure = $null
$samples = [Collections.Generic.List[object]]::new()

New-Item -ItemType Directory -Path $resolvedArtifactRoot, $resolvedDataRoot -Force | Out-Null

try {
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $resolvedExecutable
    $startInfo.WorkingDirectory = Split-Path -Parent $resolvedExecutable
    $startInfo.UseShellExecute = $false
    $startInfo.ArgumentList.Clear()
    foreach ($argument in $ArgumentList) {
        [void]$startInfo.ArgumentList.Add($argument)
    }
    $startInfo.Environment['DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT'] = $resolvedDataRoot
    $startInfo.Environment['DNPPV_SOAK_DURATION_MINUTES'] = $DurationMinutes.ToString([Globalization.CultureInfo]::InvariantCulture)
    if (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
        # The key exists only in the child process environment and is never
        # written to the result manifest, trace artifact, or command output.
        $startInfo.Environment['DNPPV_OPENROUTER_API_KEY'] = $OpenRouterApiKey
    }
    if (-not [string]::IsNullOrWhiteSpace($ScreenshotPath)) {
        $startInfo.Environment['DNPPV_PRODUCT_CAPTURE_PATH'] = $ScreenshotPath
        $startInfo.Environment['DNPPV_PRODUCT_CAPTURE_INTERVAL_MINUTES'] = $ScreenshotIntervalMinutes.ToString([Globalization.CultureInfo]::InvariantCulture)
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Could not start product executable: $resolvedExecutable"
    }

    $deadline = [DateTimeOffset]::UtcNow.AddMinutes($DurationMinutes)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        Start-Sleep -Seconds $PollIntervalSeconds
        $process.Refresh()
        $samples.Add([pscustomobject]@{
            utc = [DateTimeOffset]::UtcNow.ToString('O')
            pid = $process.Id
            running = -not $process.HasExited
        })
        if ($process.HasExited) {
            throw "Product exited before soak duration completed with code $($process.ExitCode)."
        }
    }
    $outcome = 'Passed'
}
catch {
    $failure = $_.Exception.Message
}
finally {
    if ($null -ne $process) {
        $primaryFailure = $failure
        try {
            if (-not $process.HasExited) {
                [void]$process.CloseMainWindow()
                if (-not $process.WaitForExit(10000)) {
                    $process.Kill($true)
                    [void]$process.WaitForExit(10000)
                }
            }
        }
        catch {
            $outcome = 'Failed'
            if ([string]::IsNullOrWhiteSpace($primaryFailure)) {
                $failure = "Process cleanup failed: $($_.Exception.Message)"
            }
            else {
                $failure = "$primaryFailure; process cleanup also failed: $($_.Exception.Message)"
            }
        }
        finally {
            $process.Dispose()
        }
    }

    New-Item -ItemType Directory -Path $traceDestination -Force | Out-Null
    foreach ($traceName in @('trace.circular.log', 'trace.circular.idx')) {
        $source = Join-Path $traceSource $traceName
        if (Test-Path -LiteralPath $source -PathType Leaf) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $traceDestination $traceName) -Force
        }
    }

    $screenshotPresent = $false
    if (-not [string]::IsNullOrWhiteSpace($ScreenshotPath)) {
        if ($ScreenshotIntervalMinutes -gt 0) {
            $screenshotPresent = @(Get-ChildItem -LiteralPath $ScreenshotPath -Filter '*.png' -File -ErrorAction SilentlyContinue).Count -gt 0
        }
        else {
            $screenshotPresent = @(Get-ChildItem -LiteralPath $ScreenshotPath -Filter '*.png' -File -ErrorAction SilentlyContinue).Count -gt 0
        }
    }

    $result = [ordered]@{
        schema = 'dnppv2-product-soak/v1'
        outcome = $outcome
        executable = $resolvedExecutable
        durationMinutes = $DurationMinutes
        startedUtc = $startedAt.ToString('O')
        completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        host = [Environment]::MachineName
        processCleanedUp = $true
        openRouterKeyProvided = -not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)
        traceFiles = @(Get-ChildItem -LiteralPath $traceDestination -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name)
        screenshot = [ordered]@{
            requested = -not [string]::IsNullOrWhiteSpace($ScreenshotPath)
            path = $ScreenshotPath
            intervalMinutes = $ScreenshotIntervalMinutes
            present = $screenshotPresent
        }
        samples = @($samples)
    }
    if ($null -ne $failure) {
        $result.failure = $failure
    }
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resultPath -Encoding utf8
}

if ($outcome -ne 'Passed') {
    throw "Product soak failed. See $resultPath"
}

Write-Output "PRODUCT_SOAK=Passed;MINUTES=$DurationMinutes;ARTIFACT_ROOT=$resolvedArtifactRoot"
