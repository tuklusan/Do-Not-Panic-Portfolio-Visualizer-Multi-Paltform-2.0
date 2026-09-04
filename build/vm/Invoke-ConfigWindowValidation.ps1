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
    [ValidateRange(30, 14400)]
    [int]$TimeoutSeconds = 120,

    [Parameter()]
    [ValidateRange(2, 180)]
    [int]$SceneWarmupSeconds = 2,

    [Parameter()]
    [ValidateRange(0, 240)]
    [int]$SoakDurationMinutes = 0,

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
    [switch]$ForceNewsFailure,

    [Parameter()]
    [string]$OpenRouterApiKey = $(if ($env:DNPPV_OPENROUTER_API_KEY) { $env:DNPPV_OPENROUTER_API_KEY } elseif ($env:OPENROUTER_API_KEY) { $env:OPENROUTER_API_KEY } else { $env:OPENROUTER_AI_API_KEY }),

    [Parameter()]
    [switch]$SkipDeployment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:NativeCommandTimeoutSeconds = $TimeoutSeconds + ($SoakDurationMinutes * 60) + 600

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter()][string[]]$ArgumentList = @(),
        [Parameter()][int[]]$AllowedExitCodes = @(0),
        [Parameter()][ValidateRange(0, 30000)][int]$TimeoutSeconds = 0
    )

    if ($TimeoutSeconds -le 0) {
        $TimeoutSeconds = $script:NativeCommandTimeoutSeconds
    }

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $FilePath
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    if ($psi.PSObject.Properties.Name -contains 'ArgumentList') {
        foreach ($argument in $ArgumentList) {
            [void]$psi.ArgumentList.Add($argument)
        }
    }
    else {
        $psi.Arguments = ($ArgumentList | ForEach-Object {
                if ($_ -match '[\s\"]') {
                    '"' + ($_ -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
                }
                else {
                    $_
                }
            }) -join ' '
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $psi
    try {
        if (-not $process.Start()) {
            throw "Unable to start native command: $FilePath"
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            Stop-NativeProcessTree -ProcessId $process.Id
            throw "Native command timed out after ${TimeoutSeconds}s: $FilePath $($ArgumentList -join ' ')"
        }

        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if ($stdout) { Write-Output $stdout.TrimEnd() }
        if ($stderr) { Write-Verbose ("Native command stderr: " + $stderr.TrimEnd()) }
        $exitCode = $process.ExitCode
    }
    finally {
        $process.Dispose()
    }

    if ($AllowedExitCodes -notcontains $exitCode) {
        throw "Native command failed with exit code ${exitCode}: $FilePath $($ArgumentList -join ' ')"
    }
}

function Stop-NativeProcessTree {
    param([Parameter(Mandatory = $true)][int]$ProcessId)

    $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$ProcessId" -ErrorAction SilentlyContinue)
    foreach ($child in $children) {
        Stop-NativeProcessTree -ProcessId ([int]$child.ProcessId)
    }

    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
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
    $remoteExecutionFailure = $null
    $artifactRetrievalFailure = $null
    try {
        Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @(
            '-e',
            'ssh',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=60',
            "$User@$HostName",
            'powershell',
            '-NoProfile',
            '-NonInteractive',
            '-OutputFormat',
            'Text',
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

function Assert-Windows10StorageContract {
    param(
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Secret
    )

    $scriptText = @'
$projectRoot = 'D:\SW_DEV\DO-NOT-PANIC-2.0'
$tempRoot = 'D:\TEMP'
foreach ($requiredPath in @($projectRoot, $tempRoot)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Container)) {
        throw "WINDOWS10_STORAGE_HARD_STOP=MissingOrInaccessible:$requiredPath"
    }
    try {
        $probe = Join-Path $requiredPath '.dnppv2-storage-contract-probe'
        [IO.File]::WriteAllText($probe, 'probe')
        Remove-Item -LiteralPath $probe -Force
    }
    catch {
        throw "WINDOWS10_STORAGE_HARD_STOP=NotWritable:$requiredPath"
    }
}
$machineTemp = [Environment]::GetEnvironmentVariable('TEMP', 'Machine')
$machineTmp = [Environment]::GetEnvironmentVariable('TMP', 'Machine')
if ($machineTemp -ne $tempRoot -or $machineTmp -ne $tempRoot) {
    throw "WINDOWS10_STORAGE_HARD_STOP=MachineTempMapping:TEMP=$machineTemp;TMP=$machineTmp"
}
$d = Get-CimInstance -ClassName Win32_LogicalDisk -Filter "DeviceID='D:'"
if ($null -eq $d -or [int64]$d.FreeSpace -le 0) {
    throw 'WINDOWS10_STORAGE_HARD_STOP=DDriveUnavailable'
}
[pscustomobject]@{
    Contract = 'windows-10-project-storage'
    ProjectRoot = $projectRoot
    TempRoot = $tempRoot
    MachineTemp = $machineTemp
    MachineTmp = $machineTmp
    DFreeBytes = [int64]$d.FreeSpace
} | ConvertTo-Json -Compress
'@
    Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText $scriptText
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
        Invoke-NativeCommand -FilePath 'sshpass' -TimeoutSeconds ([Math]::Max($script:NativeCommandTimeoutSeconds, 600)) -ArgumentList @(
            '-e',
            'scp',
            '-O',
            '-r',
            '-C',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=60',
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

function Copy-LinuxPublishToRemote {
    param(
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$TargetPublishPath
    )

    Assert-RequiredTool -Name 'tar'
    $archivePath = New-TemporaryScriptPath -LeafName 'dnppv2-linux-publish.tar.gz'
    $remoteArchivePath = "$TargetPublishPath/.dnppv2-linux-publish.tar.gz"
    $targetLiteral = Convert-ToBashSingleQuotedLiteral -Value $TargetPublishPath
    $archiveLiteral = Convert-ToBashSingleQuotedLiteral -Value $remoteArchivePath
    $previous = $env:SSHPASS
    $env:SSHPASS = $Secret
    try {
        Invoke-NativeCommand -FilePath 'tar' -TimeoutSeconds ([Math]::Max($script:NativeCommandTimeoutSeconds, 600)) -ArgumentList @(
            '-czf',
            $archivePath,
            '-C',
            $SourcePath,
            '.'
        )
        Invoke-NativeCommand -FilePath 'sshpass' -TimeoutSeconds ([Math]::Max($script:NativeCommandTimeoutSeconds, 600)) -ArgumentList @(
            '-e',
            'scp',
            '-O',
            '-C',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=60',
            $archivePath,
            "${User}@${HostName}:$remoteArchivePath"
        )
        Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @(
            '-e',
            'ssh',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=60',
            "${User}@${HostName}",
            "mkdir -p -- $targetLiteral && tar -xzf $archiveLiteral -C $targetLiteral && rm -f -- $archiveLiteral"
        )
    }
    finally {
        Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue
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
        [Parameter(Mandatory = $true)][string]$DestinationPath,
        [Parameter()][switch]$Recursive
    )

    $previous = $env:SSHPASS
    $env:SSHPASS = $Secret
    try {
        $copyArguments = @('-e', 'scp')
        if ($Recursive.IsPresent) {
            $copyArguments += '-r'
        }
        $copyArguments += @(
            '-O',
            '-o',
            'StrictHostKeyChecking=no',
            '-o',
            'BatchMode=no',
            '-o',
            'ConnectTimeout=60',
            "${User}@${HostName}:$SourcePath",
            $DestinationPath
        )
        Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList $copyArguments
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

function Publish-RemoteOpenRouterSecret {
    param(
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$ApiKey,
        [Parameter(Mandatory = $true)][string]$RemotePath,
        [Parameter(Mandatory = $true)][ValidateSet('linux', 'windows')][string]$TargetPlatform
    )
    $localPath = New-TemporaryScriptPath -LeafName 'dnppv2-openrouter-secret.txt'
    try {
        [IO.File]::WriteAllText($localPath, $ApiKey)
        $destination = Convert-ToScpRemotePath -TargetPlatform $TargetPlatform -Path $RemotePath
        Copy-ToRemote -User $User -HostName $HostName -Secret $Secret -SourcePath $localPath -DestinationPath $destination
        if ($TargetPlatform -eq 'linux') {
            $literal = Convert-ToBashSingleQuotedLiteral -Value $RemotePath
            $previous = $env:SSHPASS
            $env:SSHPASS = $Secret
            try {
                Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @('-e', 'ssh', '-o', 'StrictHostKeyChecking=no', '-o', 'BatchMode=no', '-o', 'ConnectTimeout=60', "$User@$HostName", "chmod 600 -- $literal")
            }
            finally {
                if ($null -eq $previous) { Remove-Item Env:SSHPASS -ErrorAction SilentlyContinue } else { $env:SSHPASS = $previous }
            }
        }
    }
    finally {
        Remove-Item -LiteralPath $localPath -Force -ErrorAction SilentlyContinue
    }
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
        [Parameter(Mandatory = $true)][int]$SoakMinutes,
        [Parameter(Mandatory = $true)][bool]$CaptureProductScene,
        [Parameter(Mandatory = $true)][bool]$CaptureGraphImpulseFixture,
        [Parameter(Mandatory = $true)][bool]$CaptureCinematicPlaybackTrace,
        [Parameter(Mandatory = $true)][bool]$CaptureRenderHeartbeatFixture,
        [Parameter(Mandatory = $true)][bool]$CaptureDuplicateInstanceFixture,
        [Parameter(Mandatory = $true)][bool]$ForceNewsFailure,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$OpenRouterApiKey,
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
                'ConnectTimeout=60',
                "$User@$HostName",
                "rm -rf -- $remotePublishDirLiteral && mkdir -p -- $remotePublishDirLiteral"
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

        Copy-LinuxPublishToRemote -User $User -HostName $HostName -Secret $Secret -SourcePath $SourcePublishDir -TargetPublishPath $TargetPublishDir
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
            'ConnectTimeout=60',
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
    $remoteSecretPath = "$TargetPublishDir/.dnppv2-openrouter-secret"
    if (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
        Publish-RemoteOpenRouterSecret -User $User -HostName $HostName -Secret $Secret -ApiKey $OpenRouterApiKey -RemotePath $remoteSecretPath -TargetPlatform 'linux'
    }
    $scriptLines = @(
        '#!/usr/bin/env bash',
        'set -euo pipefail',
        'export DISPLAY=:0',
        'XAUTHORITY_DISCOVERED=',
        'for candidate in /run/sddm/xauth_*; do if [ -r "$candidate" ]; then XAUTHORITY_DISCOVERED="$candidate"; break; fi; done',
        ('export XAUTHORITY=${XAUTHORITY_DISCOVERED:-' + $xAuthorityLiteral + '}'),
        'if [ ! -r "$XAUTHORITY" ]; then echo "XAUTHORITY_NOT_FOUND:$XAUTHORITY" >> step.log; exit 1; fi',
        'export XDG_RUNTIME_DIR=/run/user/1000',
        "ART=$remotePublishDirLiteral",
        'mkdir -p "$ART/tmp"',
        'export TMPDIR="$ART/tmp"',
        'cd "$ART"',
        'chmod +x ./DoNotPanicPortfolioVisualizer.App',
        'if [ -f ./YFinanceServer/YFinance.NET.Server ]; then chmod +x ./YFinanceServer/YFinance.NET.Server; fi',
        'rm -f general.png validation.png step.log',
        'rm -rf "$ART/local-data"',
        'DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT="$ART/local-data" DNPPV_CONFIGURATION_VALIDATION_MODE=1 setsid ./DoNotPanicPortfolioVisualizer.App > /dev/null 2>&1 &',
        'APPPID=$!',
        'echo "APPPID=$APPPID" >> step.log',
        'cleanup() {',
        '  if kill -0 "$APPPID" 2>/dev/null; then',
        '    pkill -TERM -s "$APPPID" 2>/dev/null || true',
        '    sleep 2',
        '    pkill -KILL -s "$APPPID" 2>/dev/null || true',
        '  fi',
        ('  rm -f -- ' + (Convert-ToBashSingleQuotedLiteral -Value $remoteSecretPath)),
        '}',
        'trap cleanup EXIT',
        "trap 'cleanup; exit 143' TERM INT",
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
        }
        $launchEnvironment += 'DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT="$ART/local-data"'
        if ($CaptureRenderHeartbeatFixture) {
            $launchEnvironment += 'DNPPV_RENDER_HEARTBEAT_FIXTURE=1'
        }
        if ($ForceNewsFailure) {
            $launchEnvironment += 'DNPPV_FORCE_NEWS_FAILURE=1'
        }
        if (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
            $launchEnvironment += ('DNPPV_OPENROUTER_API_KEY="$(cat -- ' + (Convert-ToBashSingleQuotedLiteral -Value $remoteSecretPath) + ')"')
        }
        if ($SoakMinutes -gt 0) {
            $launchEnvironment += 'DNPPV_PRODUCT_CAPTURE_PATH="$ART/screenshots"'
            $launchEnvironment += 'DNPPV_PRODUCT_CAPTURE_INTERVAL_MINUTES=30'
            if (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
                $launchEnvironment += 'DNPPV_SOAK_REQUIRE_AI_NEWS=1'
            }
        }
        $launchPrefix = if ($launchEnvironment.Count -eq 0) { '' } else { ($launchEnvironment -join ' ') + ' ' }
        $launchLine = $launchPrefix + 'setsid ./DoNotPanicPortfolioVisualizer.App > /dev/null 2>&1 &'
        $duplicateLines = if ($CaptureDuplicateInstanceFixture) {
            @(
                './DoNotPanicPortfolioVisualizer.App > /dev/null 2>&1 &',
                'DUPPID=$!',
                'echo "DUPPID=$DUPPID" >> step.log',
                'DUPWID=""',
                'for i in $(seq 1 10); do',
                '  DUPWID=$(xdotool search --pid "$DUPPID" | tail -n 1 || true)',
                '  if [ -n "${DUPWID:-}" ]; then break; fi',
                '  sleep 1',
                'done',
                'if [ -z "${DUPWID:-}" ]; then echo "DUPLICATE_WINDOW_NOT_FOUND" >> step.log; exit 1; fi',
                'if ! kill -0 "$APPPID" 2>/dev/null || ! xdotool getwindowname "$WID" >/dev/null 2>&1; then echo "PRIMARY_EXITED_DURING_DUPLICATE" >> step.log; exit 1; fi',
                'xdotool windowactivate --sync "$DUPWID" || true',
                'xdotool windowraise "$DUPWID" || true',
                'capture_screenshot duplicate.png',
                'echo "DUPLICATE_CAPTURED" >> step.log',
                'for i in $(seq 1 10); do',
                '  if ! kill -0 "$DUPPID" 2>/dev/null; then break; fi',
                '  sleep 1',
                'done',
                'if kill -0 "$DUPPID" 2>/dev/null; then echo "DUPLICATE_DID_NOT_EXIT" >> step.log; exit 1; fi',
                'if ! kill -0 "$APPPID" 2>/dev/null || ! xdotool getwindowname "$WID" >/dev/null 2>&1; then echo "PRIMARY_DID_NOT_SURVIVE" >> step.log; exit 1; fi',
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
            'XAUTHORITY_DISCOVERED=',
            'for candidate in /run/sddm/xauth_*; do if [ -r "$candidate" ]; then XAUTHORITY_DISCOVERED="$candidate"; break; fi; done',
            ('export XAUTHORITY=${XAUTHORITY_DISCOVERED:-' + $xAuthorityLiteral + '}'),
            'if [ ! -r "$XAUTHORITY" ]; then echo "XAUTHORITY_NOT_FOUND:$XAUTHORITY" >> step.log; exit 1; fi',
            'export XDG_RUNTIME_DIR=/run/user/1000',
            "ART=$remotePublishDirLiteral",
            'mkdir -p "$ART/tmp"',
            'export TMPDIR="$ART/tmp"',
            'cd "$ART"',
            'for tool in timeout xdotool; do',
            '  if ! command -v "$tool" >/dev/null 2>&1; then echo "MISSING_TOOL=$tool" >> step.log; exit 1; fi',
            'done',
            'if ! command -v scrot >/dev/null 2>&1 && ! command -v import >/dev/null 2>&1; then echo "MISSING_SCREENSHOT_TOOL" >> step.log; exit 1; fi',
        'capture_screenshot() {',
        '  local output="$1"',
        '  if command -v gnome-screenshot >/dev/null 2>&1 && timeout --kill-after=5s 15 gnome-screenshot -f "$output" 2>/dev/null && [ -s "$output" ]; then',
        '    echo "CAPTURE_TOOL=gnome-screenshot:$output" >> step.log',
        '    return',
        '  fi',
        '  rm -f "$output"',
        '  if timeout --kill-after=5s 15 scrot -o "$output" 2>/dev/null && [ -s "$output" ]; then return; fi',
        '  rm -f "$output"',
        '  if command -v import >/dev/null 2>&1 && timeout --kill-after=5s 15 import -window root "$output" 2>/dev/null && [ -s "$output" ]; then',
        '    echo "CAPTURE_FALLBACK=import:$output" >> step.log',
        '    return',
        '  fi',
        '  rm -f "$output"',
            '  echo "CAPTURE_FAILED=$output" >> step.log',
            '  exit 1',
            '}',
            'chmod +x ./DoNotPanicPortfolioVisualizer.App',
            'if [ -f ./YFinanceServer/YFinance.NET.Server ]; then chmod +x ./YFinanceServer/YFinance.NET.Server; fi',
            'rm -f general.png menu-open.png validation.png motion.png fullscreen-exit-menu.png duplicate.png step.log',
            'rm -rf "$ART/local-data"',
            $launchLine,
            'APPPID=$!',
            'sleep 1',
            'REALPID=$(pgrep -n -f ''^./DoNotPanicPortfolioVisualizer.App$'' || true)',
            'if [ -n "${REALPID:-}" ]; then APPPID="$REALPID"; fi',
            'DUPPID=""',
            'echo "APPPID=$APPPID" >> step.log',
            'cleanup() {',
            '  if [ -n "${DUPPID:-}" ] && kill -0 "$DUPPID" 2>/dev/null; then',
            '    kill "$DUPPID" 2>/dev/null || true',
            '    sleep 1',
            '    kill -9 "$DUPPID" 2>/dev/null || true',
            '  fi',
            '  if kill -0 "$APPPID" 2>/dev/null; then',
            '    kill -TERM "$APPPID" 2>/dev/null || true',
            '    pkill -TERM -s "$APPPID" 2>/dev/null || true',
            '    sleep 2',
            '    kill -KILL "$APPPID" 2>/dev/null || true',
            '    pkill -KILL -s "$APPPID" 2>/dev/null || true',
            '  fi',
            '}',
            'trap cleanup EXIT',
            "trap 'cleanup; exit 143' TERM INT",
            "for i in `$(seq 1 $Timeout); do",
            '  WID=$(xdotool search --pid "$APPPID" | tail -n 1 || true)',
            '  if [ -n "${WID:-}" ]; then break; fi',
            '  sleep 1',
            'done',
            'if [ -z "${WID:-}" ]; then echo "WINDOW_NOT_FOUND" >> step.log; exit 1; fi',
            'xdotool windowactivate --sync "$WID" || true',
            'xdotool key alt+F10 || true',
            "sleep $Warmup",
            'for i in $(seq 1 15); do',
            '  if grep -a -q "STARTUP;SIGNAL=DEFERRED_LANES_STARTED" "$ART/local-data/Trace/trace.circular.log"; then break; fi',
            '  sleep 1',
            'done',
            'if ! grep -a -q "STARTUP;SIGNAL=DEFERRED_LANES_STARTED" "$ART/local-data/Trace/trace.circular.log"; then echo "DEFERRED_LANES_NOT_STARTED" >> step.log; exit 1; fi',
            'echo "DEFERRED_LANES_CONFIRMED" >> step.log',
            'eval "$(xdotool getwindowgeometry --shell "$WID")"',
            'echo "WINDOW=$WID X=$X Y=$Y W=$WIDTH H=$HEIGHT" >> step.log',
            'capture_screenshot general.png',
            'echo "GENERAL_CAPTURED" >> step.log'
        ) + $duplicateLines + @(
            'xdotool key alt+f',
            'sleep 1',
            'capture_screenshot menu-open.png',
            'echo "MENU_OPEN_CAPTURED" >> step.log',
            'xdotool key Escape',
            'sleep 1',
            'if timeout 5 xdotool key --window "$WID" F11; then',
            '  echo "FULLSCREEN_REQUESTED" >> step.log',
            'else',
            '  echo "FULLSCREEN_REQUEST_FAILED" >> step.log',
            'fi',
            'sleep 8',
            # Avalonia can recreate the X11 window while entering fullscreen.
            # Resolve the live product window again instead of retaining its old ID.
            'WID=$(xdotool search --pid "$APPPID" | tail -n 1 || true)',
            'if [ -z "${WID:-}" ]; then echo "FULLSCREEN_WINDOW_NOT_FOUND" >> step.log; exit 1; fi',
            'GEOMETRY=$(timeout 5 xdotool getwindowgeometry --shell "$WID" 2>/dev/null || true)',
            'X=$(printf "%s\\n" "$GEOMETRY" | sed -n "s/^X=//p")',
            'Y=$(printf "%s\\n" "$GEOMETRY" | sed -n "s/^Y=//p")',
            'WIDTH=$(printf "%s\\n" "$GEOMETRY" | sed -n "s/^WIDTH=//p")',
            'HEIGHT=$(printf "%s\\n" "$GEOMETRY" | sed -n "s/^HEIGHT=//p")',
            'if [[ "${X:-}" =~ ^-?[0-9]+$ ]] && [[ "${Y:-}" =~ ^-?[0-9]+$ ]] && [[ "${WIDTH:-}" =~ ^[0-9]+$ ]] && [[ "${HEIGHT:-}" =~ ^[0-9]+$ ]]; then',
            '  echo "FULLSCREEN_GEOMETRY=X=$X Y=$Y W=$WIDTH H=$HEIGHT" >> step.log',
            'else',
            '  echo "FULLSCREEN_GEOMETRY_UNAVAILABLE" >> step.log',
            '  exit 1',
            'fi',
            'capture_screenshot validation.png',
            'echo "VALIDATION_CAPTURED" >> step.log',
            'sleep 4',
            'capture_screenshot motion.png',
            'echo "MOTION_CAPTURED" >> step.log',
            'timeout 5 xdotool key --window "$WID" F11',
            'echo "FULLSCREEN_EXIT_REQUESTED" >> step.log',
            'sleep 4',
            'WID=$(xdotool search --pid "$APPPID" | tail -n 1 || true)',
            'if [ -z "${WID:-}" ]; then echo "FULLSCREEN_EXIT_WINDOW_NOT_FOUND" >> step.log; exit 1; fi',
            'echo "FULLSCREEN_EXIT_WINDOW=$WID" >> step.log',
            'xdotool windowactivate --sync "$WID"',
            'xdotool mousemove --window "$WID" 24 16 click 1',
            'sleep 1',
            'capture_screenshot fullscreen-exit-menu.png',
            'echo "FULLSCREEN_EXIT_MENU_CAPTURED" >> step.log',
            'xdotool key --window "$WID" Escape',
            'sleep 1'
        )
        $linuxSoakLines = if ($SoakMinutes -gt 0) {
            @(
                'mkdir -p "$ART/screenshots"',
                ('echo "SOAK_STARTED;MINUTES={0}" >> step.log' -f $SoakMinutes),
                ('sleep {0}' -f ($SoakMinutes * 60)),
                'echo "SOAK_COMPLETED" >> step.log'
            )
        } else { @() }
        $scriptLines = $scriptLines + $linuxSoakLines
    }
    Write-Utf8NoBomFile -Path $localScriptPath -Content ([string]::Join("`n", $scriptLines) + "`n")

    Copy-ToRemote -User $User -HostName $HostName -Secret $Secret -SourcePath $localScriptPath -DestinationPath (Convert-ToScpRemotePath -TargetPlatform 'linux' -Path $remoteScriptPath)
    $maximumCaptureSeconds = 45
    $fullscreenTransitionSeconds = 22
    $cleanupSeconds = 3
    $duplicateWorkflowSeconds = if ($CaptureDuplicateInstanceFixture) { 65 } else { 0 }
    $remoteScriptTimeoutSeconds = $Timeout + $Warmup + ($SoakMinutes * 60) +
        (3 * $maximumCaptureSeconds) + $fullscreenTransitionSeconds + $cleanupSeconds +
        $duplicateWorkflowSeconds + 90
    $remoteExecutionFailure = $null
    $artifactRetrievalFailure = $null
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
            'ConnectTimeout=60',
            "$User@$HostName",
            "timeout --kill-after=10s ${remoteScriptTimeoutSeconds}s bash $remoteScriptPath"
        )
    }
    catch {
        $remoteExecutionFailure = $_
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
        @('general.png', 'menu-open.png', 'validation.png', 'motion.png', 'fullscreen-exit-menu.png', 'step.log')
    }
    else {
        @('general.png', 'validation.png', 'step.log')
    }
    if ($CaptureDuplicateInstanceFixture) {
        $artifactNames += 'duplicate.png'
    }
    foreach ($artifactName in $artifactNames) {
        try {
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent (Join-Path $ArtifactRoot $artifactName)) | Out-Null
            Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'linux' -Path "$TargetPublishDir/$artifactName") -DestinationPath (Join-Path $ArtifactRoot $artifactName)
        }
        catch {
            if ($null -eq $artifactRetrievalFailure) { $artifactRetrievalFailure = $_ }
        }
    }
    if ($SoakMinutes -gt 0) {
        $screenshotsArtifactRoot = Join-Path $ArtifactRoot 'screenshots'
        New-Item -ItemType Directory -Force -Path $screenshotsArtifactRoot | Out-Null
        Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'linux' -Path "$TargetPublishDir/screenshots/*") -DestinationPath $screenshotsArtifactRoot -Recursive
    }

    $traceArtifactRoot = Join-Path $ArtifactRoot 'trace'
    New-Item -ItemType Directory -Force -Path $traceArtifactRoot | Out-Null
    Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'linux' -Path "$TargetPublishDir/local-data/Trace/trace.circular.log") -DestinationPath (Join-Path $traceArtifactRoot 'trace.circular.log')
    Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'linux' -Path "$TargetPublishDir/local-data/Trace/trace.circular.idx") -DestinationPath (Join-Path $traceArtifactRoot 'trace.circular.idx')

    if ($null -ne $remoteExecutionFailure -and $null -ne $artifactRetrievalFailure) {
        throw "Linux validation and artifact retrieval both failed. remote=$($remoteExecutionFailure.Exception.Message); retrieval=$($artifactRetrievalFailure.Exception.Message)"
    }
    if ($null -ne $artifactRetrievalFailure) {
        throw "Linux validation completed remotely, but artifact retrieval failed. retrieval=$($artifactRetrievalFailure.Exception.Message)"
    }
    if ($null -ne $remoteExecutionFailure) {
        throw $remoteExecutionFailure
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
        [Parameter(Mandatory = $true)][int]$SoakMinutes,
        [Parameter(Mandatory = $true)][string]$TaskName,
        [Parameter(Mandatory = $true)][bool]$CaptureProductScene,
        [Parameter(Mandatory = $true)][bool]$CaptureGraphImpulseFixture,
        [Parameter(Mandatory = $true)][bool]$CaptureCinematicPlaybackTrace,
        [Parameter(Mandatory = $true)][bool]$CaptureRenderHeartbeatFixture,
        [Parameter(Mandatory = $true)][bool]$CaptureDuplicateInstanceFixture,
        [Parameter(Mandatory = $true)][bool]$ForceNewsFailure,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$OpenRouterApiKey,
        [Parameter(Mandatory = $true)][bool]$SkipRemoteDeployment
    )

    $targetPublishDirPsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $TargetPublishDir
    $remoteSecretName = '.dnppv2-openrouter-secret-{0}' -f ([Guid]::NewGuid().ToString('N'))
    $remoteSecretPath = Join-Path $TargetPublishDir $remoteSecretName
    $taskNamePsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $TaskName
    if ($TargetPublishDir -match '^[Dd]:\\SW_DEV\\DO-NOT-PANIC-2\.0(?:\\|$)') {
        Assert-Windows10StorageContract -User $User -HostName $HostName -Secret $Secret
    }
    if (-not $SkipRemoteDeployment) {
        Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText "Remove-Item -LiteralPath $targetPublishDirPsLiteral -Force -Recurse -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force -Path $targetPublishDirPsLiteral | Out-Null"
        # OpenSSH for Windows accepts the publish contents as a wildcard.  A
        # trailing `\.` source is rejected by some OpenSSH versions even though
        # an individual file or wildcard source is valid.
        Copy-ToRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Join-Path $SourcePublishDir '*') -DestinationPath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path "$TargetPublishDir/")
    }
    $deploymentFailureMessage = if ($SkipRemoteDeployment) { 'Remote publish executable is missing after deployment was skipped.' } else { 'Remote publish deployment did not complete.' }
    $deploymentFailureMessagePsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $deploymentFailureMessage
    Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText "if (-not (Test-Path -LiteralPath (Join-Path $targetPublishDirPsLiteral 'DoNotPanicPortfolioVisualizer.App.exe') -PathType Leaf)) { throw $deploymentFailureMessagePsLiteral }"

    $localScriptPath = New-TemporaryScriptPath -LeafName 'dnppv2-windows-config-window-validation.ps1'
    $remoteScriptPath = Join-Path $TargetPublishDir 'run-validation.ps1'
    $captureEnvironmentLine = if ($SoakMinutes -gt 0) {
        '$captureDirectory = Join-Path $artifactDir ''screenshots''; New-Item -ItemType Directory -Force -Path $captureDirectory | Out-Null; $env:DNPPV_PRODUCT_CAPTURE_PATH = $captureDirectory; $env:DNPPV_PRODUCT_CAPTURE_INTERVAL_MINUTES = ''30'''
    }
    else {
        '$env:DNPPV_PRODUCT_CAPTURE_PATH = $null; $env:DNPPV_PRODUCT_CAPTURE_INTERVAL_MINUTES = $null'
    }
    $windowsSoakLine = if ($SoakMinutes -gt 0) {
        ('    Add-Content -Path $stepPath -Value ''SOAK_STARTED;MINUTES={0}''; Start-Sleep -Seconds {0}; Add-Content -Path $stepPath -Value ''SOAK_COMPLETED''' -f ($SoakMinutes * 60))
    }
    else {
        '    # No soak requested.'
    }
    $requireAiNewsLine = if ($SoakMinutes -gt 0 -and -not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
        '$env:DNPPV_SOAK_REQUIRE_AI_NEWS = ''1'''
    }
    else {
        'Remove-Item Env:DNPPV_SOAK_REQUIRE_AI_NEWS -ErrorAction SilentlyContinue'
    }
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
        '$userDotnetRoot = Join-Path $env:USERPROFILE ''DNPPV2\dotnet''',
        'if (Test-Path -LiteralPath (Join-Path $userDotnetRoot ''dotnet.exe'') -PathType Leaf) {',
        '    $env:DOTNET_ROOT = $userDotnetRoot',
        '    $env:DOTNET_ROOT_X64 = $userDotnetRoot',
        '}',
        '$donePath = Join-Path $artifactDir ''done.txt''',
        '$stepPath = Join-Path $artifactDir ''step.log''',
        '$localDataRoot = Join-Path $artifactDir ''local-data''',
        '$storageContractRoot = ''D:\\SW_DEV\\DO-NOT-PANIC-2.0''',
        '$storageContractTemp = ''D:\\TEMP''',
        'function Assert-StorageContract {',
        '    if ($artifactDir -notlike ($storageContractRoot + ''*'')) { return }',
        '    foreach ($requiredPath in @($storageContractRoot, $storageContractTemp)) {',
        '        if (-not (Test-Path -LiteralPath $requiredPath -PathType Container)) { throw (''WINDOWS10_STORAGE_HARD_STOP=MissingOrInaccessible:{0}'' -f $requiredPath) }',
        '        $probe = Join-Path $requiredPath ''.dnppv2-storage-contract-probe''',
        '        [IO.File]::WriteAllText($probe, ''probe'')',
        '        Remove-Item -LiteralPath $probe -Force',
        '    }',
        '    $machineTemp = [Environment]::GetEnvironmentVariable(''TEMP'', ''Machine'')',
        '    $machineTmp = [Environment]::GetEnvironmentVariable(''TMP'', ''Machine'')',
        '    if ($machineTemp -ne $storageContractTemp -or $machineTmp -ne $storageContractTemp) { throw (''WINDOWS10_STORAGE_HARD_STOP=MachineTempMapping:TEMP={0};TMP={1}'' -f $machineTemp, $machineTmp) }',
        '}',
        'Assert-StorageContract',
        'Remove-Item -Force -Recurse -ErrorAction SilentlyContinue $localDataRoot',
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
        '$env:DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT = $localDataRoot',
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
        $renderHeartbeatFixtureLine = if ($CaptureRenderHeartbeatFixture) {
            '$env:DNPPV_RENDER_HEARTBEAT_FIXTURE = ''1'''
        }
        else {
            'Remove-Item Env:DNPPV_RENDER_HEARTBEAT_FIXTURE -ErrorAction SilentlyContinue'
        }
        $forceNewsFailureLine = if ($ForceNewsFailure) {
            '$env:DNPPV_FORCE_NEWS_FAILURE = ''1'''
        }
        else {
            'Remove-Item Env:DNPPV_FORCE_NEWS_FAILURE -ErrorAction SilentlyContinue'
        }
        $remoteSecretNamePsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $remoteSecretName
        $openRouterApiKeyLine = if (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
            '$openRouterSecretPath = Join-Path $artifactDir ' + $remoteSecretNamePsLiteral
            '$env:DNPPV_OPENROUTER_API_KEY = (Get-Content -LiteralPath $openRouterSecretPath -Raw).Trim()'
        }
        else {
            'Remove-Item Env:DNPPV_OPENROUTER_API_KEY -ErrorAction SilentlyContinue'
        }
        $openRouterApiKeyCleanupLine = if (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
            'Remove-Item -LiteralPath $openRouterSecretPath -Force -ErrorAction SilentlyContinue'
        }
        else {
            '$null = $null'
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
            '    [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr hWnd);',
            '    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);',
            '    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);',
            '    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();',
            '    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);',
            '    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);',
            '    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);',
            '    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);',
            '    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();',
            '    [DllImport("user32.dll")] public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);',
            '    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);',
            '    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);',
            '    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);',
            '    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);',
            '    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool repaint);',
            '    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);',
            '    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }',
            '    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);',
            '    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);',
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
            '$userDotnetRoot = Join-Path $env:USERPROFILE ''DNPPV2\dotnet''',
            'if (Test-Path -LiteralPath (Join-Path $userDotnetRoot ''dotnet.exe'') -PathType Leaf) { $env:DOTNET_ROOT = $userDotnetRoot; $env:DOTNET_ROOT_X64 = $userDotnetRoot }',
            '$donePath = Join-Path $artifactDir ''done.txt''',
            '$stepPath = Join-Path $artifactDir ''step.log''',
            '$localDataRoot = Join-Path $artifactDir ''local-data''',
            'Remove-Item -Force -Recurse -ErrorAction SilentlyContinue $localDataRoot',
            'Remove-Item -Force -ErrorAction SilentlyContinue $donePath, $stepPath, (Join-Path $artifactDir ''general.png''), (Join-Path $artifactDir ''validation.png''), (Join-Path $artifactDir ''motion.png''), (Join-Path $artifactDir ''small-viewport.png''), (Join-Path $artifactDir ''menu-open.png''), (Join-Path $artifactDir ''wide-viewport.png''), (Join-Path $artifactDir ''fullscreen.png''), (Join-Path $artifactDir ''fullscreen-motion.png''), (Join-Path $artifactDir ''fullscreen-exit-menu.png''), (Join-Path $artifactDir ''duplicate.png'')',
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
            'function Assert-ForegroundWindow {',
            '    param([IntPtr]$WindowHandle, [string]$State)',
            '    $foreground = $false',
            '    for ($attempt = 0; $attempt -lt 5; $attempt++) {',
            '        $foregroundWindow = [DnppvSceneNative]::GetForegroundWindow()',
            '        if ($foregroundWindow -eq [IntPtr]::Zero) { Add-Content -Path $stepPath -Value ''FOREGROUND_WINDOW=NONE'' }',
            '        [DnppvSceneNative]::ShowWindow($WindowHandle, 9) | Out-Null',
            '        [DnppvSceneNative]::SetWindowPos($WindowHandle, [IntPtr](-1), 0, 0, 0, 0, 0x0003) | Out-Null',
            '        [DnppvSceneNative]::AllowSetForegroundWindow(-1) | Out-Null',
            '        [DnppvSceneNative]::BringWindowToTop($WindowHandle) | Out-Null',
            '        [DnppvSceneNative]::SwitchToThisWindow($WindowHandle, $true)',
            '        [DnppvSceneNative]::SetForegroundWindow($WindowHandle) | Out-Null',
            '        [DnppvSceneNative]::SetActiveWindow($WindowHandle) | Out-Null',
            '        Start-Sleep -Milliseconds 500',
            '        if ([DnppvSceneNative]::GetForegroundWindow() -eq $WindowHandle) { $foreground = $true; [DnppvSceneNative]::SetWindowPos($WindowHandle, [IntPtr](-2), 0, 0, 0, 0, 0x0003) | Out-Null; break }',
            '        [DnppvSceneNative]::SetWindowPos($WindowHandle, [IntPtr](-2), 0, 0, 0, 0, 0x0003) | Out-Null',
            '        if (-not $foreground) {',
            '            [uint32]$foregroundProcessId = 0; [uint32]$foregroundThread = [DnppvSceneNative]::GetWindowThreadProcessId([DnppvSceneNative]::GetForegroundWindow(), [ref]$foregroundProcessId); [uint32]$currentThread = [DnppvSceneNative]::GetCurrentThreadId();',
            '            if ($foregroundThread -ne 0 -and $foregroundThread -ne $currentThread -and [DnppvSceneNative]::AttachThreadInput($currentThread, $foregroundThread, $true)) { try { [DnppvSceneNative]::SetForegroundWindow($WindowHandle) | Out-Null; [DnppvSceneNative]::SetActiveWindow($WindowHandle) | Out-Null; Start-Sleep -Milliseconds 500; $foreground = [DnppvSceneNative]::GetForegroundWindow() -eq $WindowHandle } finally { [DnppvSceneNative]::AttachThreadInput($currentThread, $foregroundThread, $false) | Out-Null } }',
            '            if ($foreground) { [DnppvSceneNative]::SetWindowPos($WindowHandle, [IntPtr](-2), 0, 0, 0, 0, 0x0003) | Out-Null; break }',
            '        }',
            '    }',
            '    if (-not $foreground) { throw (''Product window was not foreground before {0}.'' -f $State) }',
            '}',
            'function Get-WindowBounds {',
            '    param([IntPtr]$WindowHandle)',
            '    $rect = New-Object DnppvSceneNative+RECT',
            '    if (-not [DnppvSceneNative]::GetWindowRect($WindowHandle, [ref]$rect)) {',
            '        throw ''GetWindowRect failed for the product window.''',
            '    }',
            '    return $rect',
            '}',
            'function Click-UiAutomationElementCenter {',
            '    param([System.Windows.Automation.AutomationElement]$Element)',
            '    $bounds = $Element.Current.BoundingRectangle',
            '    if ($bounds.IsEmpty -or $bounds.Width -le 0 -or $bounds.Height -le 0) { throw ''UI Automation target did not expose a usable rectangle.'' }',
            '    [DnppvSceneNative]::SetCursorPos([int]($bounds.Left + ($bounds.Width / 2)), [int]($bounds.Top + ($bounds.Height / 2))) | Out-Null',
            '    Start-Sleep -Milliseconds 250',
            '    [DnppvSceneNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)',
            '    Start-Sleep -Milliseconds 100',
            '    [DnppvSceneNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)',
            '    Start-Sleep -Milliseconds 350',
            '}',
            $fixtureEnabledLine,
            '$env:DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT = $localDataRoot',
            $captureEnvironmentLine,
            $renderHeartbeatFixtureLine,
            $forceNewsFailureLine,
            $openRouterApiKeyLine,
            $requireAiNewsLine,
            'Add-Content -Path $stepPath -Value ("AI_KEY_PRESENT={0};AI_REQUIRE={1}" -f (-not [string]::IsNullOrWhiteSpace($env:DNPPV_OPENROUTER_API_KEY)), $env:DNPPV_SOAK_REQUIRE_AI_NEWS)',
            '$exePath = Join-Path $artifactDir ''DoNotPanicPortfolioVisualizer.App.exe''',
            '$startInfo = [System.Diagnostics.ProcessStartInfo]::new()',
            '$startInfo.FileName = $exePath',
            '$startInfo.WorkingDirectory = $artifactDir',
            '$startInfo.UseShellExecute = $false',
            '$startInfo.Environment[''DNPPV_OPENROUTER_API_KEY''] = $env:DNPPV_OPENROUTER_API_KEY',
            '$startInfo.Environment[''DNPPV_SOAK_REQUIRE_AI_NEWS''] = $env:DNPPV_SOAK_REQUIRE_AI_NEWS',
            '$startInfo.Arguments = ''--windowed=1024x768''',
            '$duplicate = $null',
            '$proc = [System.Diagnostics.Process]::Start($startInfo)',
            'if ($null -eq $proc) { throw ''Product process launch returned no process handle.'' }',
            $openRouterApiKeyCleanupLine,
            'try {',
            '    Add-Content -Path $stepPath -Value (''PID={0}'' -f $proc.Id)',
            "    for (`$attempt = 0; `$attempt -lt $Timeout; `$attempt++) {",
            '        Start-Sleep -Seconds 1',
            '        if (Get-Command Assert-StorageContract -ErrorAction SilentlyContinue) { Assert-StorageContract }',
            '        $proc.Refresh()',
            '        if ($proc.HasExited) { throw (''Product process exited before opening a window. Exit code: {0}'' -f $proc.ExitCode) }',
            '        if ($proc.MainWindowHandle -ne 0) { break }',
            '    }',
            '    if ($proc.MainWindowHandle -eq 0) { throw ''Main window handle was not detected.'' }',
            # Product-scene runs start with the Avalonia-owned --windowed state.
            # Do not subsequently restore it through Win32, which can reapply the
            # XAML default maximized state after startup.
            '    Start-Sleep -Seconds 1',
            '    Assert-ForegroundWindow -WindowHandle $proc.MainWindowHandle -State ''small-viewport positioning''',
            "    Start-Sleep -Seconds $Warmup",
            '    $startupBounds = Get-WindowBounds -WindowHandle $proc.MainWindowHandle',
            '    Add-Content -Path $stepPath -Value (''STARTUP_VIEWPORT={0},{1}'' -f ($startupBounds.Right - $startupBounds.Left), ($startupBounds.Bottom - $startupBounds.Top))',
            '    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds',
            '    if ($bounds.Width -lt 1024 -or $bounds.Height -lt 768) {',
            '        throw (''Primary screen is too small for required 1024x768 capture: {0}x{1}.'' -f $bounds.Width, $bounds.Height)',
            '    }',
            '    Assert-ForegroundWindow -WindowHandle $proc.MainWindowHandle -State ''small-viewport capture''',
            '    $smallBounds = Get-WindowBounds -WindowHandle $proc.MainWindowHandle',
            '    $smallWidth = $smallBounds.Right - $smallBounds.Left',
            '    $smallHeight = $smallBounds.Bottom - $smallBounds.Top',
            '    $windowedTrace = $null',
            '    $tracePath = Join-Path $localDataRoot ''Trace\trace.circular.log''',
            '    for ($attempt = 0; $attempt -lt 20; $attempt++) {',
            '        $windowedTrace = Get-Content -LiteralPath $tracePath -ErrorAction SilentlyContinue | Where-Object { $_ -like ''*event=WindowedStartupApplied*'' } | Select-Object -Last 1',
            '        if (-not [string]::IsNullOrWhiteSpace($windowedTrace)) { break }',
            '        Start-Sleep -Milliseconds 500',
            '    }',
            '    if ([string]::IsNullOrWhiteSpace($windowedTrace)) {',
            '        throw ''Timed out waiting for the documented Avalonia 1024x768 windowed-startup trace.''',
            '    }',
            '    Add-Content -Path $stepPath -Value (''SCREEN={0},{1}'' -f $bounds.Width, $bounds.Height)',
            '    Add-Content -Path $stepPath -Value ''LOGICAL_SMALL_VIEWPORT=1024,768''',
            '    Add-Content -Path $stepPath -Value (''SMALL_WINDOW_RECT_PIXELS={0},{1}'' -f $smallWidth, $smallHeight)',
            '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''small-viewport.png'')',
            '    Add-Content -Path $stepPath -Value ''SMALL_VIEWPORT_CAPTURED''',
            '    Assert-ForegroundWindow -WindowHandle $proc.MainWindowHandle -State ''menu-open capture''',
            '    Add-Type -AssemblyName UIAutomationClient',
            '    $automationRoot = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)',
            '    $fileCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, ''File'')',
            '    $fileMenu = $automationRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $fileCondition)',
            '    if ($null -eq $fileMenu) { throw ''The File menu item was not discoverable through UI Automation.'' }',
            '    Click-UiAutomationElementCenter -Element $fileMenu',
            '    Start-Sleep -Milliseconds 500',
            '    $exitCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, ''Exit'')',
            '    $exitMenu = $automationRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $exitCondition)',
            '    if ($null -eq $exitMenu -or $exitMenu.Current.IsOffscreen) { throw ''The File menu submenu was not visible after its UI Automation click.'' }',
            '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''menu-open.png'')',
            '    Add-Content -Path $stepPath -Value ''MENU_OPEN_CAPTURED''',
            '    [System.Windows.Forms.SendKeys]::SendWait(''{ESC}'')',
            '    Start-Sleep -Milliseconds 300'
        ) + $duplicateLines + @(
            '    [DnppvSceneNative]::ShowWindow($proc.MainWindowHandle, 3) | Out-Null',
            '    Start-Sleep -Seconds 2',
            '    Assert-ForegroundWindow -WindowHandle $proc.MainWindowHandle -State ''wide-viewport capture''',
            '    $wideBounds = Get-WindowBounds -WindowHandle $proc.MainWindowHandle',
            '    Add-Content -Path $stepPath -Value (''WIDE_VIEWPORT={0},{1}'' -f ($wideBounds.Right - $wideBounds.Left), ($wideBounds.Bottom - $wideBounds.Top))',
            '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''wide-viewport.png'')',
            '    Add-Content -Path $stepPath -Value ''WIDE_VIEWPORT_CAPTURED''',
            '    Assert-ForegroundWindow -WindowHandle $proc.MainWindowHandle -State ''fullscreen request''',
            '    [System.Windows.Forms.SendKeys]::SendWait(''{F11}'')',
            '    Add-Content -Path $stepPath -Value ''FULLSCREEN_REQUESTED''',
            '    $fullScreenConfirmed = $false',
            '    for ($attempt = 0; $attempt -lt 16; $attempt++) {',
            '        Start-Sleep -Milliseconds 500',
            '        $fullBounds = Get-WindowBounds -WindowHandle $proc.MainWindowHandle',
            '        Add-Content -Path $stepPath -Value (''FULLSCREEN_RECT={0},{1},{2},{3}'' -f $fullBounds.Left, $fullBounds.Top, $fullBounds.Right, $fullBounds.Bottom)',
            '        if ($fullBounds.Left -eq $bounds.Left -and $fullBounds.Top -eq $bounds.Top -and',
            '            ($fullBounds.Right - $fullBounds.Left) -eq $bounds.Width -and',
            '            ($fullBounds.Bottom - $fullBounds.Top) -eq $bounds.Height) {',
            '            $fullScreenConfirmed = $true',
            '            break',
            '        }',
            '    }',
            '    if (-not $fullScreenConfirmed) { throw ''F11 did not produce primary-screen fullscreen bounds.'' }',
            '    Assert-ForegroundWindow -WindowHandle $proc.MainWindowHandle -State ''fullscreen capture''',
            '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''fullscreen.png'')',
            '    Add-Content -Path $stepPath -Value ''FULLSCREEN_CAPTURED''',
            '    Start-Sleep -Seconds 4',
            '    Assert-ForegroundWindow -WindowHandle $proc.MainWindowHandle -State ''fullscreen motion capture''',
            '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''fullscreen-motion.png'')',
            '    Add-Content -Path $stepPath -Value ''FULLSCREEN_MOTION_CAPTURED''',
            '    [System.Windows.Forms.SendKeys]::SendWait(''{F11}'')',
            '    Add-Content -Path $stepPath -Value ''FULLSCREEN_EXIT_REQUESTED''',
            '    $fullscreenExitConfirmed = $false',
            '    for ($attempt = 0; $attempt -lt 16; $attempt++) {',
            '        Start-Sleep -Milliseconds 500',
            '        $restoredBounds = Get-WindowBounds -WindowHandle $proc.MainWindowHandle',
            '        if ($restoredBounds.Top -gt $bounds.Top -or $restoredBounds.Left -gt $bounds.Left -or',
            '            ($restoredBounds.Right - $restoredBounds.Left) -lt $bounds.Width -or',
            '            ($restoredBounds.Bottom - $restoredBounds.Top) -lt $bounds.Height) {',
            '            $fullscreenExitConfirmed = $true',
            '            break',
            '        }',
            '    }',
            '    if (-not $fullscreenExitConfirmed) { throw ''F11 did not restore a non-fullscreen product window.'' }',
            '    Assert-ForegroundWindow -WindowHandle $proc.MainWindowHandle -State ''fullscreen exit menu restoration''',
            '    $automationRoot = [System.Windows.Automation.AutomationElement]::FromHandle($proc.MainWindowHandle)',
            '    $fileMenu = $automationRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $fileCondition)',
            '    if ($null -eq $fileMenu) { throw ''The File menu item was not restored after leaving fullscreen.'' }',
            '    Click-UiAutomationElementCenter -Element $fileMenu',
            '    Start-Sleep -Milliseconds 500',
            '    $exitMenu = $automationRoot.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $exitCondition)',
            '    if ($null -eq $exitMenu -or $exitMenu.Current.IsOffscreen) { throw ''The File submenu was not visible after leaving fullscreen.'' }',
            '    Save-DesktopScreenshot -Path (Join-Path $artifactDir ''fullscreen-exit-menu.png'')',
            '    Add-Content -Path $stepPath -Value ''FULLSCREEN_EXIT_MENU_CAPTURED''',
            '    [System.Windows.Forms.SendKeys]::SendWait(''{ESC}'')',
            '    Start-Sleep -Milliseconds 300',
            $windowsSoakLine,
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
    if (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
        Publish-RemoteOpenRouterSecret -User $User -HostName $HostName -Secret $Secret -ApiKey $OpenRouterApiKey -RemotePath $remoteSecretPath -TargetPlatform 'windows'
    }

    $remoteScriptPathPsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $remoteScriptPath
    $remoteUserPsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $User
    $remoteDriver = @"
`$taskName = $taskNamePsLiteral
`$scriptPath = $remoteScriptPathPsLiteral
`$artifactDir = $targetPublishDirPsLiteral
`$taskUser = $remoteUserPsLiteral
`$userDotnetRoot = Join-Path `$env:USERPROFILE 'DNPPV2\dotnet'
if (Test-Path -LiteralPath (Join-Path `$userDotnetRoot 'dotnet.exe') -PathType Leaf) { `$env:DOTNET_ROOT = `$userDotnetRoot; `$env:DOTNET_ROOT_X64 = `$userDotnetRoot }
`$donePath = Join-Path `$artifactDir 'done.txt'
`$localDataRoot = Join-Path `$artifactDir 'local-data'
Remove-Item -Force -Recurse -ErrorAction SilentlyContinue `$localDataRoot
Remove-Item -Force -ErrorAction SilentlyContinue `$donePath, (Join-Path `$artifactDir 'general.png'), (Join-Path `$artifactDir 'validation.png'), (Join-Path `$artifactDir 'motion.png'), (Join-Path `$artifactDir 'small-viewport.png'), (Join-Path `$artifactDir 'menu-open.png'), (Join-Path `$artifactDir 'wide-viewport.png'), (Join-Path `$artifactDir 'fullscreen.png'), (Join-Path `$artifactDir 'fullscreen-motion.png'), (Join-Path `$artifactDir 'fullscreen-exit-menu.png'), (Join-Path `$artifactDir 'duplicate.png'), (Join-Path `$artifactDir 'step.log')
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
    for (`$attempt = 0; `$attempt -lt ($Timeout + ($SoakMinutes * 60)); `$attempt++) {
        if (Test-Path `$donePath) {
            break
        }

        Start-Sleep -Seconds 1
    }

    if (Test-Path `$donePath) {
        for (`$cleanupAttempt = 0; `$cleanupAttempt -lt 30; `$cleanupAttempt++) {
            `$taskState = (Get-ScheduledTask -TaskName `$taskName -ErrorAction SilentlyContinue).State
            if (`$taskState -ne 'Running') {
                break
            }

            Start-Sleep -Seconds 1
        }

        if ((Get-ScheduledTask -TaskName `$taskName -ErrorAction SilentlyContinue).State -eq 'Running') {
            throw 'Validation task did not complete its cleanup after writing done.txt.'
        }

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
    try {
        Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText $remoteDriver
    }
    finally {
        if (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
            Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText ("Remove-Item -LiteralPath '" + $remoteSecretPath.Replace("'", "''") + "' -Force -ErrorAction SilentlyContinue")
        }
    }

    $artifactNames = if ($CaptureProductScene) {
        @('small-viewport.png', 'menu-open.png', 'wide-viewport.png', 'fullscreen.png', 'fullscreen-motion.png', 'fullscreen-exit-menu.png', 'step.log', 'done.txt')
    }
    else {
        @('general.png', 'validation.png', 'step.log', 'done.txt')
    }
    if ($CaptureDuplicateInstanceFixture) {
        $artifactNames += 'duplicate.png'
    }
    $doneDestination = Join-Path $ArtifactRoot 'done.txt'
    try {
        Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path (Join-Path $TargetPublishDir 'done.txt')) -DestinationPath $doneDestination
    }
    catch {
        throw "Remote Windows validation did not produce done.txt; artifact retrieval cannot continue. retrieval_error=$($_.Exception.Message)"
    }
    $doneText = (Get-Content -Raw -LiteralPath $doneDestination).Trim()
    if (-not [string]::Equals($doneText, 'DONE', [StringComparison]::Ordinal)) {
        $failureStepDestination = Join-Path $ArtifactRoot 'step.log'
        try {
            Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path (Join-Path $TargetPublishDir 'step.log')) -DestinationPath $failureStepDestination
        }
        catch {
            $doneText = "$doneText; step.log retrieval failed: $($_.Exception.Message)"
        }
        throw "Remote Windows validation failed before artifact retrieval. See done.txt and step.log: $doneText"
    }
    try {
        $stepDestination = Join-Path $ArtifactRoot 'step.log'
        Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path (Join-Path $TargetPublishDir 'step.log')) -DestinationPath $stepDestination
        foreach ($artifactName in @($artifactNames | Where-Object { $_ -notin @('step.log', 'done.txt') })) {
            Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path (Join-Path $TargetPublishDir $artifactName)) -DestinationPath (Join-Path $ArtifactRoot $artifactName)
        }
        if ($SoakMinutes -gt 0) {
            $screenshotsArtifactRoot = Join-Path $ArtifactRoot 'screenshots'
            New-Item -ItemType Directory -Force -Path $screenshotsArtifactRoot | Out-Null
            Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path (Join-Path $TargetPublishDir 'screenshots/*')) -DestinationPath $screenshotsArtifactRoot -Recursive
        }
        $traceArtifactRoot = Join-Path $ArtifactRoot 'trace'
        New-Item -ItemType Directory -Force -Path $traceArtifactRoot | Out-Null
        Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path (Join-Path $TargetPublishDir 'local-data/Trace/trace.circular.log')) -DestinationPath (Join-Path $traceArtifactRoot 'trace.circular.log')
        Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path (Join-Path $TargetPublishDir 'local-data/Trace/trace.circular.idx')) -DestinationPath (Join-Path $traceArtifactRoot 'trace.circular.idx')
    }
    catch {
        throw "Remote Windows validation completed with done.txt=DONE, but artifact retrieval failed. done.txt=DONE; retrieval_error=$($_.Exception.Message)"
    }
}

function Assert-SoakNewsEvidence {
    param(
        [Parameter(Mandatory = $true)][string]$ArtifactRoot,
        [Parameter(Mandatory = $true)][bool]$RequireAiNews
    )

    $tracePath = Join-Path $ArtifactRoot 'trace/trace.circular.log'
    if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) {
        throw "Local soak news evidence is missing its circular trace: $tracePath"
    }

    $trace = Get-Content -LiteralPath $tracePath -Raw
    $rssUsable = $trace -match 'event=RssPlaybackReady\s*/\s*state=(Fresh|Partial)\s*/\s*headline_count=[1-9][0-9]*'
    $aiSucceeded = $trace -match '\bevent=AiSummarySucceeded(?:\s|\||$)'
    $aiRequested = $trace -match '\bevent=AiSummaryRequestStarted(?:\s|\||$)'
    $evidence = [ordered]@{
        schema = 'dnppv2-soak-news-evidence/v1'
        rssUsable = [bool]$rssUsable
        aiRequired = $RequireAiNews
        aiRequestObserved = [bool]$aiRequested
        aiSuccessObserved = [bool]$aiSucceeded
        traceFile = 'trace/trace.circular.log'
    }
    $evidence | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ArtifactRoot 'news-evidence.json') -Encoding utf8

    if (-not $rssUsable) {
        throw 'Local soak RSS evidence failed: no Fresh or Partial NEWS_SOURCE state was found in the circular trace.'
    }
    if ($RequireAiNews -and (-not $aiRequested -or -not $aiSucceeded)) {
        throw 'Local soak AI evidence failed: the circular trace did not prove both AiSummaryRequestStarted and AiSummarySucceeded.'
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
if ($ForceNewsFailure -and -not $ProductScene) {
    throw '-ForceNewsFailure requires -ProductScene.'
}
if ($ProductScene -and $SoakDurationMinutes -gt 0 -and [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
    throw 'A real-product soak requires OPENROUTER_API_KEY (or OPENROUTER_AI_API_KEY) so RSS and AI generation are both exercised.'
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
        Invoke-LinuxValidation -HostName $RemoteHost -User $RemoteUser -Secret $Password -SourcePublishDir $resolvedPublishDir -TargetPublishDir $RemotePublishDir -ArtifactRoot $LocalArtifactRoot -Timeout $TimeoutSeconds -Warmup $SceneWarmupSeconds -SoakMinutes $SoakDurationMinutes -CaptureProductScene $ProductScene.IsPresent -CaptureGraphImpulseFixture $GraphImpulseFixture.IsPresent -CaptureCinematicPlaybackTrace $CinematicPlaybackTrace.IsPresent -CaptureRenderHeartbeatFixture $RenderHeartbeatFixture.IsPresent -CaptureDuplicateInstanceFixture $DuplicateInstanceFixture.IsPresent -ForceNewsFailure $ForceNewsFailure.IsPresent -OpenRouterApiKey $OpenRouterApiKey -SkipRemoteDeployment $SkipDeployment.IsPresent
        break
    }
    'windows' {
        Invoke-WindowsValidation -HostName $RemoteHost -User $RemoteUser -Secret $Password -SourcePublishDir $resolvedPublishDir -TargetPublishDir $RemotePublishDir -ArtifactRoot $LocalArtifactRoot -Timeout $TimeoutSeconds -Warmup $SceneWarmupSeconds -SoakMinutes $SoakDurationMinutes -TaskName $WindowsTaskName -CaptureProductScene $ProductScene.IsPresent -CaptureGraphImpulseFixture $GraphImpulseFixture.IsPresent -CaptureCinematicPlaybackTrace $CinematicPlaybackTrace.IsPresent -CaptureRenderHeartbeatFixture $RenderHeartbeatFixture.IsPresent -CaptureDuplicateInstanceFixture $DuplicateInstanceFixture.IsPresent -ForceNewsFailure $ForceNewsFailure.IsPresent -OpenRouterApiKey $OpenRouterApiKey -SkipRemoteDeployment $SkipDeployment.IsPresent
        break
    }
    default {
        throw "Unsupported platform: $Platform"
    }
}

if ($ProductScene -and $SoakDurationMinutes -gt 0) {
    Assert-SoakNewsEvidence -ArtifactRoot $LocalArtifactRoot -RequireAiNews (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey))
}

Write-Output "CONFIG_WINDOW_VALIDATION=Passed;PLATFORM=$Platform;ARTIFACT_ROOT=$LocalArtifactRoot"
