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
    [ValidateSet('linux', 'windows')]
    [string]$Platform,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RemoteHost,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RemoteUser,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Password,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$LocalPublishDir,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RemotePublishDir,

    [Parameter()]
    [string]$LocalArtifactRoot = (Join-Path $env:TEMP ("dnppv2-config-window-validation-{0}-{1:yyyyMMdd-HHmmss}" -f $Platform, (Get-Date))),

    [Parameter()]
    [ValidateRange(30, 600)]
    [int]$TimeoutSeconds = 120,

    [Parameter()]
    [ValidateRange(2, 180)]
    [int]$SceneWarmupSeconds = 2,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$WindowsTaskName = 'DNPPV_CR007_ConfigWindow',

    [Parameter()]
    [switch]$ProductScene,

    [Parameter()]
    [switch]$GraphImpulseFixture,

    [Parameter()]
    [switch]$CinematicPlaybackTrace,

    [Parameter()]
    [switch]$RenderHeartbeatFixture,

    [Parameter()]
    [switch]$DuplicateInstanceFixture,

    [Parameter()]
    [switch]$SkipDeployment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter()][string[]]$ArgumentList = @(),
        [Parameter()][int[]]$AllowedExitCodes = @(0)
    )

    & $FilePath @ArgumentList
    $exitCode = $global:LASTEXITCODE
    if ($AllowedExitCodes -notcontains $exitCode) {
        throw "Native command failed with exit code ${exitCode}: $FilePath $($ArgumentList -join ' ')"
    }
}

function Convert-ToBashSingleQuotedLiteral {
    param([Parameter(Mandatory = $true)][string]$Value)

    return "'" + ($Value -replace "'", '''"''"''') + "'"
}

function Convert-ToPowerShellSingleQuotedLiteral {
    param([Parameter(Mandatory = $true)][string]$Value)

    return "'{0}'" -f ($Value -replace "'", "''")
}

function Assert-SafeRemoteInputs {
    param(
        [Parameter(Mandatory = $true)][string]$TargetPlatform,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$PublishDir,
        [Parameter(Mandatory = $true)][string]$TaskLabel
    )

    if ($HostName -notmatch '^[A-Za-z0-9.\-]+$') {
        throw "Unsafe remote host value: $HostName"
    }

    if ($User -notmatch '^[A-Za-z0-9._\-]+$') {
        throw "Unsafe remote user value: $User"
    }

    if ($TaskLabel -notmatch '^[A-Za-z0-9._\-]+$') {
        throw "Unsafe Windows task name: $TaskLabel"
    }

    if ($TargetPlatform -eq 'windows') {
        if ($PublishDir -notmatch '^[A-Za-z]:\\[A-Za-z0-9._\-\\/: ]+$') {
            throw "Unsafe Windows remote publish path: $PublishDir"
        }
    }
    else {
        if ($PublishDir -notmatch '^/[A-Za-z0-9._\-/]+$') {
            throw "Unsafe Linux remote publish path: $PublishDir"
        }
    }
}

function Write-Utf8NoBomFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Convert-ToScpRemotePath {
    param(
        [Parameter(Mandatory = $true)][string]$TargetPlatform,
        [Parameter(Mandatory = $true)][string]$Path
    )

    if ($TargetPlatform -eq 'windows') {
        $normalized = $Path.Replace('\', '/')
        if ($normalized -match '^[A-Za-z]:/') {
            return "/$normalized"
        }

        return $normalized
    }

    return $Path
}

function Invoke-RemotePowerShell {
    param(
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$ScriptText
    )

    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($ScriptText))
    $previous = $env:SSHPASS
    $env:SSHPASS = $Secret
    try {
        Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @(
            '-e',
            'ssh',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=15',
            "$User@$HostName",
            'powershell',
            '-NoProfile',
            '-EncodedCommand',
            $encoded
        )
    }
    finally {
        if ($null -eq $previous) {
            Remove-Item Env:SSHPASS -ErrorAction SilentlyContinue
        }
        else {
            $env:SSHPASS = $previous
        }
    }
}

function Copy-ToRemote {
    param(
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    $previous = $env:SSHPASS
    $env:SSHPASS = $Secret
    try {
        Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @(
            '-e',
            'scp',
            '-r',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=15',
            $SourcePath,
            "${User}@${HostName}:$DestinationPath"
        )
    }
    finally {
        if ($null -eq $previous) {
            Remove-Item Env:SSHPASS -ErrorAction SilentlyContinue
        }
        else {
            $env:SSHPASS = $previous
        }
    }
}

function Copy-FromRemote {
    param(
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    $previous = $env:SSHPASS
    $env:SSHPASS = $Secret
    try {
        Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @(
            '-e',
            'scp',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=15',
            "${User}@${HostName}:$SourcePath",
            $DestinationPath
        )
    }
    finally {
        if ($null -eq $previous) {
            Remove-Item Env:SSHPASS -ErrorAction SilentlyContinue
        }
        else {
            $env:SSHPASS = $previous
        }
    }
}

function Assert-RequiredTool {
    param([Parameter(Mandatory = $true)][string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command is not available: $Name"
    }
}

function New-TemporaryScriptPath {
    param([Parameter(Mandatory = $true)][string]$LeafName)

    $extension = [IO.Path]::GetExtension($LeafName)
    $stem = [IO.Path]::GetFileNameWithoutExtension($LeafName)
    $uniqueLeafName = '{0}-{1}{2}' -f $stem, [Guid]::NewGuid().ToString('N'), $extension
    return Join-Path $env:TEMP $uniqueLeafName
}

function Invoke-LinuxValidation {
    param(
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$SourcePublishDir,
        [Parameter(Mandatory = $true)][string]$TargetPublishDir,
        [Parameter(Mandatory = $true)][string]$ArtifactRoot,
        [Parameter(Mandatory = $true)][int]$Timeout,
        [Parameter(Mandatory = $true)][int]$Warmup,
        [Parameter(Mandatory = $true)][bool]$CaptureProductScene,
        [Parameter(Mandatory = $true)][bool]$CaptureGraphImpulseFixture,
        [Parameter(Mandatory = $true)][bool]$CaptureCinematicPlaybackTrace,
        [Parameter(Mandatory = $true)][bool]$CaptureRenderHeartbeatFixture,
        [Parameter(Mandatory = $true)][bool]$CaptureDuplicateInstanceFixture,
        [Parameter(Mandatory = $true)][bool]$SkipRemoteDeployment
    )

    $remotePublishDirLiteral = Convert-ToBashSingleQuotedLiteral -Value $TargetPublishDir
    $xAuthorityLiteral = Convert-ToBashSingleQuotedLiteral -Value ("/home/{0}/.Xauthority" -f $User)
    if (-not $SkipRemoteDeployment) {
        $previous = $env:SSHPASS
        $env:SSHPASS = $Secret
        try {
            Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @(
                '-e',
                'ssh',
                '-o',
                'StrictHostKeyChecking=no',
                '-o',
                'BatchMode=no',
                '-o',
                'ConnectTimeout=15',
                "$User@$HostName",
                "mkdir -p -- $remotePublishDirLiteral"
            )
        }
        finally {
            if ($null -eq $previous) {
                Remove-Item Env:SSHPASS -ErrorAction SilentlyContinue
            }
            else {
                $env:SSHPASS = $previous
            }
        }

        Copy-ToRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Join-Path $SourcePublishDir '.') -DestinationPath (Convert-ToScpRemotePath -TargetPlatform 'linux' -Path "$TargetPublishDir/")
    }

    $linuxExecutableLiteral = Convert-ToBashSingleQuotedLiteral -Value "$TargetPublishDir/DoNotPanicPortfolioVisualizer.App"
    $previous = $env:SSHPASS
    $env:SSHPASS = $Secret
    try {
        Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @(
            '-e',
            'ssh',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=15',
            "$User@$HostName",
            "test -f $linuxExecutableLiteral"
        )
    }
    catch {
        $context = if ($SkipRemoteDeployment) { ' after deployment was skipped' } else { ' after deployment' }
        throw "Remote Linux publish executable is missing${context}: $TargetPublishDir"
    }
    finally {
        if ($null -eq $previous) {
            Remove-Item Env:SSHPASS -ErrorAction SilentlyContinue
        }
        else {
            $env:SSHPASS = $previous
        }
    }

    $localScriptPath = New-TemporaryScriptPath -LeafName 'dnppv2-linux-config-window-validation.sh'
    $remoteScriptPath = "$TargetPublishDir/run-validation.sh"
    $scriptLines = @(
        '#!/usr/bin/env bash',
        'set -euo pipefail',
        'export DISPLAY=:0',
        "export XAUTHORITY=$xAuthorityLiteral",
        'export XDG_RUNTIME_DIR=/run/user/1000',
        "ART=$remotePublishDirLiteral",
        'mkdir -p "$ART/tmp"',
        'export TMPDIR="$ART/tmp"',
        'cd "$ART"',
        'chmod +x ./DoNotPanicPortfolioVisualizer.App ./YFinanceServer/YFinance.NET.Server',
        'rm -f general.png validation.png run.log step.log',
        'DNPPV_CONFIGURATION_VALIDATION_MODE=1 setsid ./DoNotPanicPortfolioVisualizer.App > run.log 2>&1 &',
        'APPPID=$!',
        'echo "APPPID=$APPPID" >> step.log',
        'cleanup() {',
        '  if kill -0 "$APPPID" 2>/dev/null; then',
        '    pkill -TERM -s "$APPPID" 2>/dev/null || true',
        '    sleep 2',
        '    pkill -KILL -s "$APPPID" 2>/dev/null || true',
        '  fi',
        '}',
        'trap cleanup EXIT',
        "for i in `$(seq 1 $Timeout); do",
        '  WID=$(xdotool search --pid "$APPPID" | tail -n 1 || true)',
        '  if [ -n "${WID:-}" ]; then',
        '    break',
        '  fi',
        '  sleep 1',
        'done',
        'if [ -z "${WID:-}" ]; then',
        '  echo "WINDOW_NOT_FOUND" >> step.log',
        '  exit 1',
        'fi',
        'echo "WINDOW=$WID" >> step.log',
        'xdotool windowactivate --sync "$WID" || true',
        'xdotool windowraise "$WID" || true',
        'xdotool windowmove "$WID" 20 20 || true',
        'xdotool windowsize "$WID" 1200 700 || true',
        "sleep $Warmup",
        'eval "$(xdotool getwindowgeometry --shell "$WID")"',
        'echo "X=$X Y=$Y W=$WIDTH H=$HEIGHT" >> step.log',
        'BG_X=$((X + (WIDTH * 384 / 1000)))',
        'BG_Y=$((Y + (HEIGHT * 349 / 1000)))',
        'GROUP_X=$((X + (WIDTH * 300 / 1000)))',
        'GROUP_Y=$((Y + (HEIGHT * 715 / 1000)))',
        'ADVANCED_X=$((X + (WIDTH * 145 / 1000)))',
        'ADVANCED_Y=$((Y + (HEIGHT * 150 / 1000)))',
        'RSS_X=$((X + (WIDTH * 408 / 1000)))',
        'RSS_Y=$((Y + (HEIGHT * 434 / 1000)))',
        'xdotool mousemove "$BG_X" "$BG_Y" click 1',
        'sleep 1',
        'xdotool key ctrl+a',
        'sleep 1',
        'xdotool key BackSpace',
        'sleep 1',
        'xdotool type --delay 20 /tmp/dnppv2-backgrounds',
        'sleep 1',
        'echo "BACKGROUND_EDITED" >> step.log',
        'xdotool mousemove "$GROUP_X" "$GROUP_Y" click 1',
        'sleep 1',
        'xdotool key ctrl+a',
        'sleep 1',
        'xdotool key BackSpace',
        'sleep 1',
        'xdotool type --delay 20 CR007-LINUX-TAPE',
        'sleep 1',
        'echo "TAPE_EDITED" >> step.log',
        'scrot -o general.png',
        'echo "GENERAL_CAPTURED" >> step.log',
        'xdotool mousemove "$ADVANCED_X" "$ADVANCED_Y" click 1',
        'echo "ADVANCED_TAB_REQUESTED" >> step.log',
        'sleep 2',
        'xdotool mousemove "$RSS_X" "$RSS_Y" click 1',
        'sleep 1',
        'xdotool key ctrl+a',
        'sleep 1',
        'xdotool key BackSpace',
        'sleep 1',
        'xdotool type --delay 20 not-a-url',
        'sleep 2',
        'echo "RSS_EDITED" >> step.log',
        'scrot -o validation.png',
        'echo "VALIDATION_CAPTURED" >> step.log',
        'echo "VALIDATION_DONE" >> step.log'
    )
    if ($CaptureProductScene) {
        $launchEnvironment = @()
        if ($CaptureGraphImpulseFixture) {
            $launchEnvironment += 'DNPPV_GRAPH_IMPULSE_FIXTURE=1'
            $launchEnvironment += 'DNPPV_GRAPH_IMPULSE_TRACE="$ART/graph-impulse.log"'
        }
        if ($CaptureCinematicPlaybackTrace) {
            $launchEnvironment += 'DNPPV_CINEMATIC_TRACE="$ART/cinematic-playback.log"'
        }
        if ($CaptureRenderHeartbeatFixture) {
            $launchEnvironment += 'DNPPV_RENDER_HEARTBEAT_FIXTURE=1'
        }
        $launchPrefix = if ($launchEnvironment.Count -eq 0) { '' } else { ($launchEnvironment -join ' ') + ' ' }
        $launchLine = $launchPrefix + 'setsid ./DoNotPanicPortfolioVisualizer.App > run.log 2>&1 &'
        $duplicateLines = if ($CaptureDuplicateInstanceFixture) {
            @(
                ': > duplicate.log',
                './DoNotPanicPortfolioVisualizer.App > duplicate.log 2>&1 &',
                'DUPPID=$!',
                'echo "DUPPID=$DUPPID" >> step.log',
                'DUPWID=""',
                'for i in $(seq 1 10); do',
                '  DUPWID=$(xdotool search --pid "$DUPPID" | tail -n 1 || true)',
                '  if [ -n "${DUPWID:-}" ]; then break; fi',
                '  sleep 1',
                'done',
                'if [ -z "${DUPWID:-}" ]; then echo "DUPLICATE_WINDOW_NOT_FOUND" >> step.log; exit 1; fi',
                'if ! kill -0 "$APPPID" 2>/dev/null; then echo "PRIMARY_EXITED_DURING_DUPLICATE" >> step.log; exit 1; fi',
                'xdotool windowactivate --sync "$DUPWID" || true',
                'xdotool windowraise "$DUPWID" || true',
                'scrot -o duplicate.png',
                'echo "DUPLICATE_CAPTURED" >> step.log',
                'for i in $(seq 1 10); do',
                '  if ! kill -0 "$DUPPID" 2>/dev/null; then break; fi',
                '  sleep 1',
                'done',
                'if kill -0 "$DUPPID" 2>/dev/null; then echo "DUPLICATE_DID_NOT_EXIT" >> step.log; exit 1; fi',
                'if ! kill -0 "$APPPID" 2>/dev/null; then echo "PRIMARY_DID_NOT_SURVIVE" >> step.log; exit 1; fi',
                'echo "DUPLICATE_EXITED_PRIMARY_ALIVE" >> step.log'
            )
        }
        else {
            @()
        }
        $scriptLines = @(
            '#!/usr/bin/env bash',
            'set -euo pipefail',
            'export DISPLAY=:0',
            "export XAUTHORITY=$xAuthorityLiteral",
            'export XDG_RUNTIME_DIR=/run/user/1000',
            "ART=$remotePublishDirLiteral",
            'mkdir -p "$ART/tmp"',
            'export TMPDIR="$ART/tmp"',
            'cd "$ART"',
            'chmod +x ./DoNotPanicPortfolioVisualizer.App ./YFinanceServer/YFinance.NET.Server',
            'rm -f general.png validation.png motion.png duplicate.png graph-impulse.log cinematic-playback.log run.log duplicate.log step.log',
            $launchLine,
            'APPPID=$!',
            'DUPPID=""',
            'echo "APPPID=$APPPID" >> step.log',
            'cleanup() {',
            '  if [ -n "${DUPPID:-}" ] && kill -0 "$DUPPID" 2>/dev/null; then',
            '    kill "$DUPPID" 2>/dev/null || true',
            '    sleep 1',
            '    kill -9 "$DUPPID" 2>/dev/null || true',
            '  fi',
            '  if kill -0 "$APPPID" 2>/dev/null; then',
            '    pkill -TERM -s "$APPPID" 2>/dev/null || true',
            '    sleep 2',
            '    pkill -KILL -s "$APPPID" 2>/dev/null || true',
            '  fi',
            '}',
            'trap cleanup EXIT',
            "for i in `$(seq 1 $Timeout); do",
            '  WID=$(xdotool search --pid "$APPPID" | tail -n 1 || true)',
            '  if [ -n "${WID:-}" ]; then break; fi',
            '  sleep 1',
            'done',
            'if [ -z "${WID:-}" ]; then echo "WINDOW_NOT_FOUND" >> step.log; exit 1; fi',
            'xdotool windowactivate --sync "$WID" || true',
            'xdotool key alt+F10 || true',
            "sleep $Warmup",
            'eval "$(xdotool getwindowgeometry --shell "$WID")"',
            'echo "WINDOW=$WID X=$X Y=$Y W=$WIDTH H=$HEIGHT" >> step.log',
            'scrot -o general.png',
            'echo "GENERAL_CAPTURED" >> step.log'
        ) + $duplicateLines + @(
            'xdotool key F11',
            'echo "FULLSCREEN_REQUESTED" >> step.log',
            'sleep 8',
            'scrot -o validation.png',
            'echo "VALIDATION_CAPTURED" >> step.log',
            'sleep 4',
            'scrot -o motion.png',
            'echo "MOTION_CAPTURED" >> step.log'
        )
    }
    Write-Utf8NoBomFile -Path $localScriptPath -Content ([string]::Join("`n", $scriptLines) + "`n")

    Copy-ToRemote -User $User -HostName $HostName -Secret $Secret -SourcePath $localScriptPath -DestinationPath (Convert-ToScpRemotePath -TargetPlatform 'linux' -Path $remoteScriptPath)
    $previous = $env:SSHPASS
    $env:SSHPASS = $Secret
    try {
        Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @(
            '-e',
            'ssh',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=15',
            "$User@$HostName",
            "timeout --kill-after=10s 90s bash $remoteScriptPath"
        )
    }
    finally {
        if ($null -eq $previous) {
            Remove-Item Env:SSHPASS -ErrorAction SilentlyContinue
        }
        else {
            $env:SSHPASS = $previous
        }
    }

    $artifactNames = if ($CaptureProductScene) {
        @('general.png', 'validation.png', 'motion.png', 'run.log', 'step.log')
    }
    else {
        @('general.png', 'validation.png', 'run.log', 'step.log')
    }
    if ($CaptureGraphImpulseFixture) {
        $artifactNames += 'graph-impulse.log'
    }
    if ($CaptureCinematicPlaybackTrace) {
        $artifactNames += 'cinematic-playback.log'
    }
    if ($CaptureDuplicateInstanceFixture) {
        $artifactNames += @('duplicate.png', 'duplicate.log')
    }
    foreach ($artifactName in $artifactNames) {
        Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'linux' -Path "$TargetPublishDir/$artifactName") -DestinationPath (Join-Path $ArtifactRoot $artifactName)
    }
}

function Invoke-WindowsValidation {
    param(
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$SourcePublishDir,
        [Parameter(Mandatory = $true)][string]$TargetPublishDir,
        [Parameter(Mandatory = $true)][string]$ArtifactRoot,
        [Parameter(Mandatory = $true)][int]$Timeout,
        [Parameter(Mandatory = $true)][int]$Warmup,
        [Parameter(Mandatory = $true)][string]$TaskName,
        [Parameter(Mandatory = $true)][bool]$CaptureProductScene,
        [Parameter(Mandatory = $true)][bool]$CaptureGraphImpulseFixture,
        [Parameter(Mandatory = $true)][bool]$CaptureCinematicPlaybackTrace,
        [Parameter(Mandatory = $true)][bool]$CaptureRenderHeartbeatFixture,
        [Parameter(Mandatory = $true)][bool]$CaptureDuplicateInstanceFixture,
        [Parameter(Mandatory = $true)][bool]$SkipRemoteDeployment
    )

    $targetPublishDirPsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $TargetPublishDir
    $taskNamePsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $TaskName
    if (-not $SkipRemoteDeployment) {
        Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText "New-Item -ItemType Directory -Force -Path $targetPublishDirPsLiteral | Out-Null"
        Copy-ToRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Join-Path $SourcePublishDir '.') -DestinationPath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path "$TargetPublishDir/")
    }
    $deploymentFailureMessage = if ($SkipRemoteDeployment) { 'Remote publish executable is missing after deployment was skipped.' } else { 'Remote publish deployment did not complete.' }
    $deploymentFailureMessagePsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $deploymentFailureMessage
    Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText "if (-not (Test-Path -LiteralPath (Join-Path $targetPublishDirPsLiteral 'DoNotPanicPortfolioVisualizer.App.exe') -PathType Leaf)) { throw $deploymentFailureMessagePsLiteral }"

    $localScriptPath = New-TemporaryScriptPath -LeafName 'dnppv2-windows-config-window-validation.ps1'
    $remoteScriptPath = Join-Path $TargetPublishDir 'run-validation.ps1'
    $scriptLines = @(
        'Add-Type -AssemblyName System.Drawing',
        'Add-Type -AssemblyName System.Windows.Forms',
        'Add-Type @"',
        'using System;',
        'using System.Runtime.InteropServices;',
        'public static class DnppvRemoteNative',
        '{',
        '    [StructLayout(LayoutKind.Sequential)]',
        '    public struct RECT',
        '    {',
        '        public int Left;',
        '        public int Top;',
        '        public int Right;',
        '        public int Bottom;',
        '    }',
        '',
        '    [DllImport("user32.dll")]',
        '    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);',
        '',
        '    [DllImport("user32.dll")]',
        '    public static extern bool SetForegroundWindow(IntPtr hWnd);',
        '',
        '    [DllImport("user32.dll")]',
        '    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);',
        '',
        '    [DllImport("user32.dll")]',
        '    public static extern bool SetCursorPos(int X, int Y);',
        '',
        '    [DllImport("user32.dll")]',
        '    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);',
        '',
        '    [DllImport("user32.dll")]',
        '    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);',
        '}',
        '"@',
        '',
        '$artifactDir = ' + $targetPublishDirPsLiteral,
        '$donePath = Join-Path $artifactDir ''done.txt''',
        '$stepPath = Join-Path $artifactDir ''step.log''',
        'Remove-Item -Force -ErrorAction SilentlyContinue $donePath, $stepPath, (Join-Path $artifactDir ''general.png''), (Join-Path $artifactDir ''validation.png'')',
        '',
        'function Click-Point {',
        '    param([int]$X, [int]$Y)',
        '    [DnppvRemoteNative]::SetCursorPos($X, $Y) | Out-Null',
        '    Start-Sleep -Milliseconds 250',
        '    [DnppvRemoteNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)',
        '    Start-Sleep -Milliseconds 100',
        '    [DnppvRemoteNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)',
        '    Start-Sleep -Milliseconds 350',
        '}',
        '',
        'function Save-WindowScreenshot {',
        '    param(',
        '        [DnppvRemoteNative+RECT]$Rect,',
        '        [string]$Path',
        '    )',
        '',
        '    $width = $Rect.Right - $Rect.Left',
        '    $height = $Rect.Bottom - $Rect.Top',
        '    $bitmap = New-Object System.Drawing.Bitmap $width, $height',
        '    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)',
        '    try {',
        '        $graphics.CopyFromScreen($Rect.Left, $Rect.Top, 0, 0, $bitmap.Size)',
        '        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)',
        '    }',
        '    finally {',
        '        $graphics.Dispose()',
        '        $bitmap.Dispose()',
        '    }',
        '}',
        '',
        '$exePath = Join-Path $artifactDir ''DoNotPanicPortfolioVisualizer.App.exe''',
        '$env:DNPPV_CONFIGURATION_VALIDATION_MODE = ''1''',
        '$proc = Start-Process -FilePath $exePath -WorkingDirectory $artifactDir -PassThru',
        'Remove-Item Env:DNPPV_CONFIGURATION_VALIDATION_MODE -ErrorAction SilentlyContinue',
        'try {',
        '    Add-Content -Path $stepPath -Value (''PID={0}'' -f $proc.Id)',
        "    for (`$attempt = 0; `$attempt -lt $Timeout; `$attempt++) {",
        '        Start-Sleep -Seconds 1',
        '        $proc.Refresh()',
        '        if ($proc.MainWindowHandle -ne 0) {',
        '            break',
        '        }',
        '    }',
        '    Add-Content -Path $stepPath -Value (''HANDLE={0}'' -f $proc.MainWindowHandle)',
        '    if ($proc.MainWindowHandle -eq 0) {',
        '        throw ''Main window handle was not detected.''',
        '    }',
        '',
        '    [DnppvRemoteNative]::ShowWindow($proc.MainWindowHandle, 5) | Out-Null',
        '    [DnppvRemoteNative]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null',
        "    Start-Sleep -Seconds $Warmup",
        '    [DnppvRemoteNative]::MoveWindow($proc.MainWindowHandle, 32, 32, 960, 640, $true) | Out-Null',
        '    Start-Sleep -Seconds 1',
        '    [DnppvRemoteNative]::ShowWindow($proc.MainWindowHandle, 5) | Out-Null',
        '    [DnppvRemoteNative]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null',
        '    Start-Sleep -Seconds 1',
        '',
        '    $rect = New-Object DnppvRemoteNative+RECT',
        '    [DnppvRemoteNative]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null',
        '    $width = $rect.Right - $rect.Left',
        '    $height = $rect.Bottom - $rect.Top',
        '    Add-Content -Path $stepPath -Value (''RECT={0},{1},{2},{3}'' -f $rect.Left, $rect.Top, $width, $height)',
        '',
        '    $backgroundX = $rect.Left + [int][Math]::Round($width * 0.384)',
        '    $backgroundY = $rect.Top + [int][Math]::Round($height * 0.349)',
        '    $groupNameX = $rect.Left + [int][Math]::Round($width * 0.300)',
        '    $groupNameY = $rect.Top + [int][Math]::Round($height * 0.785)',
        '    $advancedX = $rect.Left + [int][Math]::Round($width * 0.100)',
        '    $advancedY = $rect.Top + [int][Math]::Round($height * 0.200)',
        '    $rssX = $rect.Left + [int][Math]::Round($width * 0.408)',
        '    $rssY = $rect.Top + [int][Math]::Round($height * 0.434)',
        '    Add-Content -Path $stepPath -Value (''POINTS=BG({0},{1}) GROUP({2},{3}) ADV({4},{5}) RSS({6},{7})'' -f $backgroundX, $backgroundY, $groupNameX, $groupNameY, $advancedX, $advancedY, $rssX, $rssY)',
        '',
        '    Click-Point -X $backgroundX -Y $backgroundY',
        '    [System.Windows.Forms.SendKeys]::SendWait(''^a'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''{BACKSPACE}'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''D:\TEMP\dnppv2-backgrounds'')',
        '    Start-Sleep -Milliseconds 500',
        '    Add-Content -Path $stepPath -Value ''BACKGROUND_EDITED''',
        '',
        '    Click-Point -X $groupNameX -Y $groupNameY',
        '    [System.Windows.Forms.SendKeys]::SendWait(''^a'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''{BACKSPACE}'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''CR007-WIN-TAPE'')',
        '    Start-Sleep -Milliseconds 500',
        '    Add-Content -Path $stepPath -Value ''TAPE_EDITED''',
        '',
        '    Save-WindowScreenshot -Rect $rect -Path (Join-Path $artifactDir ''general.png'')',
        '    Add-Content -Path $stepPath -Value ''GENERAL_CAPTURED''',
        '',
        '    Click-Point -X $advancedX -Y $advancedY',
        '    Start-Sleep -Milliseconds 500',
        '    [System.Windows.Forms.SendKeys]::SendWait(''{RIGHT}'')',
        '    Add-Content -Path $stepPath -Value ''ADVANCED_TAB_REQUESTED''',
        '    Start-Sleep -Seconds 2',
        '',
        '    Click-Point -X $rssX -Y $rssY',
        '    [System.Windows.Forms.SendKeys]::SendWait(''^a'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''{BACKSPACE}'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''not-a-url'')',
        '    Start-Sleep -Seconds 2',
        '    Add-Content -Path $stepPath -Value ''RSS_EDITED''',
        '',
        '    Save-WindowScreenshot -Rect $rect -Path (Join-Path $artifactDir ''validation.png'')',
        '    Add-Content -Path $stepPath -Value ''VALIDATION_CAPTURED''',
        '    ''DONE'' | Set-Content -Path $donePath',
        '}',
        'catch {',
        '    $_ | Out-String | Set-Content -Path $donePath',
        '    throw',
        '}',
        'finally {',
        '    if ($proc -and -not $proc.HasExited) {',
        '        $proc.CloseMainWindow() | Out-Null',
        '        Start-Sleep -Seconds 2',
        '        if (-not $proc.HasExited) {',
        '            $proc.Kill()',
        '            $proc.WaitForExit()',
        '        }',
        '    }',
        '}'
    )
    if ($CaptureProductScene) {
        $fixtureEnabledLine = if ($CaptureGraphImpulseFixture) {
            '$env:DNPPV_GRAPH_IMPULSE_FIXTURE = ''1'''
        }
        else {
            'Remove-Item Env:DNPPV_GRAPH_IMPULSE_FIXTURE -ErrorAction SilentlyContinue'
        }
        $fixtureTraceLine = if ($CaptureGraphImpulseFixture) {
            '$env:DNPPV_GRAPH_IMPULSE_TRACE = Join-Path $artifactDir ''graph-impulse.log'''
        }
        else {
            'Remove-Item Env:DNPPV_GRAPH_IMPULSE_TRACE -ErrorAction SilentlyContinue'
        }
        $cinematicTraceLine = if ($CaptureCinematicPlaybackTrace) {
            '$env:DNPPV_CINEMATIC_TRACE = Join-Path $artifactDir ''cinematic-playback.log'''
        }
        else {
            'Remove-Item Env:DNPPV_CINEMATIC_TRACE -ErrorAction SilentlyContinue'
        }
        $renderHeartbeatFixtureLine = if ($CaptureRenderHeartbeatFixture) {
            '$env:DNPPV_RENDER_HEARTBEAT_FIXTURE = ''1'''
        }
        else {
            'Remove-Item Env:DNPPV_RENDER_HEARTBEAT_FIXTURE -ErrorAction SilentlyContinue'
        }
        $duplicateLines = if ($CaptureDuplicateInstanceFixture) {
            @(
                '    $duplicate = Start-Process -FilePath $exePath -WorkingDirectory $artifactDir -PassThru',
                '    Add-Content -Path $stepPath -Value (''DUPPID={0}'' -f $duplicate.Id)',
                '    $duplicateWindow = [IntPtr]::Zero',
                '    for ($attempt = 0; $attempt -lt 40; $attempt++) {',
                '        Start-Sleep -Milliseconds 250',
                '        $duplicate.Refresh()',
                '        if ($duplicate.HasExited) { break }',
                '        $duplicateWindow = [DnppvSceneNative]::FindVisibleWindowForProcess($duplicate.Id)',
                '        if ($duplicateWindow -ne [IntPtr]::Zero) { break }',
                '    }',
                '    if ($duplicate.HasExited -or $duplicateWindow -eq [IntPtr]::Zero) { throw ''Duplicate notice window was not detected.'' }',
                '    if ($proc.HasExited) { throw ''Primary process exited during duplicate launch.'' }',
                '    [DnppvSceneNative]::ShowWindow($duplicateWindow, 5) | Out-Null',
                '    [DnppvSceneNative]::SetForegroundWindow($duplicateWindow) | Out-Null',
                '    Start-Sleep -Milliseconds 750',
                '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''duplicate.png'')',
                '    Add-Content -Path $stepPath -Value ''DUPLICATE_CAPTURED''',
                '    if (-not $duplicate.WaitForExit(10000)) { throw ''Duplicate process did not exit after its notice timeout.'' }',
                '    $proc.Refresh()',
                '    if ($proc.HasExited) { throw ''Primary process did not survive duplicate launch.'' }',
                '    Add-Content -Path $stepPath -Value ''DUPLICATE_EXITED_PRIMARY_ALIVE'''
            )
        }
        else {
            @()
        }
        $scriptLines = @(
            'Add-Type -AssemblyName System.Drawing',
            'Add-Type -AssemblyName System.Windows.Forms',
            'Add-Type @"',
            'using System;',
            'using System.Runtime.InteropServices;',
            'public static class DnppvSceneNative',
            '{',
            '    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);',
            '    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();',
            '    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);',
            '    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);',
            '    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);',
            '    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);',
            '    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);',
            '    public static IntPtr FindVisibleWindowForProcess(int processId)',
            '    {',
            '        IntPtr found = IntPtr.Zero;',
            '        EnumWindows((hWnd, _) =>',
            '        {',
            '            uint ownerProcessId;',
            '            GetWindowThreadProcessId(hWnd, out ownerProcessId);',
            '            if (ownerProcessId == (uint)processId && IsWindowVisible(hWnd))',
            '            {',
            '                found = hWnd;',
            '                return false;',
            '            }',
            '            return true;',
            '        }, IntPtr.Zero);',
            '        return found;',
            '    }',
            '}',
            '"@',
            '[DnppvSceneNative]::SetProcessDPIAware() | Out-Null',
            '$artifactDir = ' + $targetPublishDirPsLiteral,
            '$donePath = Join-Path $artifactDir ''done.txt''',
            '$stepPath = Join-Path $artifactDir ''step.log''',
            'Remove-Item -Force -ErrorAction SilentlyContinue $donePath, $stepPath, (Join-Path $artifactDir ''general.png''), (Join-Path $artifactDir ''validation.png''), (Join-Path $artifactDir ''motion.png''), (Join-Path $artifactDir ''duplicate.png''), (Join-Path $artifactDir ''graph-impulse.log''), (Join-Path $artifactDir ''cinematic-playback.log'')',
            'function Save-DesktopScreenshot {',
            '    param([string]$Path)',
            '    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds',
            '    $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height',
            '    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)',
            '    try {',
            '        $graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bitmap.Size)',
            '        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)',
            '    }',
            '    finally { $graphics.Dispose(); $bitmap.Dispose() }',
            '}',
            $fixtureEnabledLine,
            $fixtureTraceLine,
            $cinematicTraceLine,
            $renderHeartbeatFixtureLine,
            '$exePath = Join-Path $artifactDir ''DoNotPanicPortfolioVisualizer.App.exe''',
            '$startInfo = [System.Diagnostics.ProcessStartInfo]::new()',
            '$startInfo.FileName = $exePath',
            '$startInfo.WorkingDirectory = $artifactDir',
            '$startInfo.UseShellExecute = $true',
            '$duplicate = $null',
            '$proc = [System.Diagnostics.Process]::Start($startInfo)',
            'if ($null -eq $proc) { throw ''Product process launch returned no process handle.'' }',
            'try {',
            '    Add-Content -Path $stepPath -Value (''PID={0}'' -f $proc.Id)',
            "    for (`$attempt = 0; `$attempt -lt $Timeout; `$attempt++) {",
            '        Start-Sleep -Seconds 1',
            '        $proc.Refresh()',
            '        if ($proc.HasExited) { throw (''Product process exited before opening a window. Exit code: {0}'' -f $proc.ExitCode) }',
            '        if ($proc.MainWindowHandle -ne 0) { break }',
            '    }',
            '    if ($proc.MainWindowHandle -eq 0) { throw ''Main window handle was not detected.'' }',
            '    [DnppvSceneNative]::ShowWindow($proc.MainWindowHandle, 3) | Out-Null',
            '    [DnppvSceneNative]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null',
            "    Start-Sleep -Seconds $Warmup",
            '    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds',
            '    Add-Content -Path $stepPath -Value (''SCREEN={0},{1}'' -f $bounds.Width, $bounds.Height)',
            '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''general.png'')',
            '    Add-Content -Path $stepPath -Value ''GENERAL_CAPTURED'''
        ) + $duplicateLines + @(
            '    [System.Windows.Forms.SendKeys]::SendWait(''{F11}'')',
            '    Add-Content -Path $stepPath -Value ''FULLSCREEN_REQUESTED''',
            '    Start-Sleep -Seconds 8',
            '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''validation.png'')',
            '    Add-Content -Path $stepPath -Value ''VALIDATION_CAPTURED''',
            '    Start-Sleep -Seconds 4',
            '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''motion.png'')',
            '    Add-Content -Path $stepPath -Value ''MOTION_CAPTURED''',
            '    ''DONE'' | Set-Content -Path $donePath',
            '}',
            'catch { $_ | Out-String | Set-Content -Path $donePath; throw }',
            'finally {',
            '    if ($duplicate -and -not $duplicate.HasExited) {',
            '        $duplicate.Kill()',
            '        $duplicate.WaitForExit()',
            '    }',
            '    if ($proc -and -not $proc.HasExited) {',
            '        $proc.CloseMainWindow() | Out-Null',
            '        Start-Sleep -Seconds 2',
            '        if (-not $proc.HasExited) { $proc.Kill(); $proc.WaitForExit() }',
            '    }',
            '}'
        )
    }
    Write-Utf8NoBomFile -Path $localScriptPath -Content ([string]::Join("`r`n", $scriptLines) + "`r`n")
    Copy-ToRemote -User $User -HostName $HostName -Secret $Secret -SourcePath $localScriptPath -DestinationPath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path $remoteScriptPath)

    $remoteScriptPathPsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $remoteScriptPath
    $remoteUserPsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $User
    $remoteDriver = @"
`$taskName = $taskNamePsLiteral
`$scriptPath = $remoteScriptPathPsLiteral
`$artifactDir = $targetPublishDirPsLiteral
`$taskUser = $remoteUserPsLiteral
`$donePath = Join-Path `$artifactDir 'done.txt'
Remove-Item -Force -ErrorAction SilentlyContinue `$donePath, (Join-Path `$artifactDir 'general.png'), (Join-Path `$artifactDir 'validation.png'), (Join-Path `$artifactDir 'motion.png'), (Join-Path `$artifactDir 'graph-impulse.log'), (Join-Path `$artifactDir 'cinematic-playback.log'), (Join-Path `$artifactDir 'step.log')
try {
    Unregister-ScheduledTask -TaskName `$taskName -Confirm:`$false -ErrorAction SilentlyContinue | Out-Null
}
catch {
}

try {
    `$escapedScriptPath = `$scriptPath.Replace('"', '""')
    `$actionArgs = '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + `$escapedScriptPath + '"'
    `$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument `$actionArgs
    `$trigger = New-ScheduledTaskTrigger -Once -At ([DateTime]::Today.AddHours(23).AddMinutes(59))
    `$principal = New-ScheduledTaskPrincipal -UserId `$taskUser -LogonType Interactive -RunLevel Limited
    Register-ScheduledTask -TaskName `$taskName -Action `$action -Trigger `$trigger -Principal `$principal -Force | Out-Null
    Start-ScheduledTask -TaskName `$taskName
    for (`$attempt = 0; `$attempt -lt $Timeout; `$attempt++) {
        if (Test-Path `$donePath) {
            break
        }

        Start-Sleep -Seconds 1
    }

    if (Test-Path `$donePath) {
        Get-Content `$donePath
    }
    else {
        'DONE_FILE_MISSING' | Set-Content -Path `$donePath
        Get-Content `$donePath
    }
}
finally {
    Unregister-ScheduledTask -TaskName `$taskName -Confirm:`$false -ErrorAction SilentlyContinue | Out-Null
}
"@
    Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText $remoteDriver

    $artifactNames = if ($CaptureProductScene) {
        @('general.png', 'validation.png', 'motion.png', 'step.log', 'done.txt')
    }
    else {
        @('general.png', 'validation.png', 'step.log', 'done.txt')
    }
    if ($CaptureGraphImpulseFixture) {
        $artifactNames += 'graph-impulse.log'
    }
    if ($CaptureCinematicPlaybackTrace) {
        $artifactNames += 'cinematic-playback.log'
    }
    if ($CaptureDuplicateInstanceFixture) {
        $artifactNames += 'duplicate.png'
    }
    foreach ($artifactName in $artifactNames) {
        Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path (Join-Path $TargetPublishDir $artifactName)) -DestinationPath (Join-Path $ArtifactRoot $artifactName)
    }
}

Assert-RequiredTool -Name 'sshpass'
Assert-RequiredTool -Name 'ssh'
Assert-RequiredTool -Name 'scp'

if ($GraphImpulseFixture -and -not $ProductScene) {
    throw '-GraphImpulseFixture requires -ProductScene.'
}
if ($CinematicPlaybackTrace -and -not $ProductScene) {
    throw '-CinematicPlaybackTrace requires -ProductScene.'
}
if ($RenderHeartbeatFixture -and -not $ProductScene) {
    throw '-RenderHeartbeatFixture requires -ProductScene.'
}
if ($DuplicateInstanceFixture -and -not $ProductScene) {
    throw '-DuplicateInstanceFixture requires -ProductScene.'
}

$resolvedPublishDir = (Resolve-Path -LiteralPath $LocalPublishDir -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath (Join-Path $resolvedPublishDir 'DoNotPanicPortfolioVisualizer.App.exe') -PathType Leaf) -and
    -not (Test-Path -LiteralPath (Join-Path $resolvedPublishDir 'DoNotPanicPortfolioVisualizer.App') -PathType Leaf)) {
    throw "Local publish directory does not contain the expected app binary: $resolvedPublishDir"
}

Assert-SafeRemoteInputs -TargetPlatform $Platform -HostName $RemoteHost -User $RemoteUser -PublishDir $RemotePublishDir -TaskLabel $WindowsTaskName

New-Item -ItemType Directory -Force -Path $LocalArtifactRoot | Out-Null

switch ($Platform) {
    'linux' {
        Invoke-LinuxValidation -HostName $RemoteHost -User $RemoteUser -Secret $Password -SourcePublishDir $resolvedPublishDir -TargetPublishDir $RemotePublishDir -ArtifactRoot $LocalArtifactRoot -Timeout $TimeoutSeconds -Warmup $SceneWarmupSeconds -CaptureProductScene $ProductScene.IsPresent -CaptureGraphImpulseFixture $GraphImpulseFixture.IsPresent -CaptureCinematicPlaybackTrace $CinematicPlaybackTrace.IsPresent -CaptureRenderHeartbeatFixture $RenderHeartbeatFixture.IsPresent -CaptureDuplicateInstanceFixture $DuplicateInstanceFixture.IsPresent -SkipRemoteDeployment $SkipDeployment.IsPresent
        break
    }
    'windows' {
        Invoke-WindowsValidation -HostName $RemoteHost -User $RemoteUser -Secret $Password -SourcePublishDir $resolvedPublishDir -TargetPublishDir $RemotePublishDir -ArtifactRoot $LocalArtifactRoot -Timeout $TimeoutSeconds -Warmup $SceneWarmupSeconds -TaskName $WindowsTaskName -CaptureProductScene $ProductScene.IsPresent -CaptureGraphImpulseFixture $GraphImpulseFixture.IsPresent -CaptureCinematicPlaybackTrace $CinematicPlaybackTrace.IsPresent -CaptureRenderHeartbeatFixture $RenderHeartbeatFixture.IsPresent -CaptureDuplicateInstanceFixture $DuplicateInstanceFixture.IsPresent -SkipRemoteDeployment $SkipDeployment.IsPresent
        break
    }
    default {
        throw "Unsupported platform: $Platform"
    }
}

Write-Output "CONFIG_WINDOW_VALIDATION=Passed;PLATFORM=$Platform;ARTIFACT_ROOT=$LocalArtifactRoot"
