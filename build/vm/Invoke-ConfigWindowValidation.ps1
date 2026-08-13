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
    [ValidateNotNullOrEmpty()]
    [string]$WindowsTaskName = 'DNPPV_CR007_ConfigWindow'
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

    return Join-Path $env:TEMP $LeafName
}

function Invoke-LinuxValidation {
    param(
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$SourcePublishDir,
        [Parameter(Mandatory = $true)][string]$TargetPublishDir,
        [Parameter(Mandatory = $true)][string]$ArtifactRoot,
        [Parameter(Mandatory = $true)][int]$Timeout
    )

    $remotePublishDirLiteral = Convert-ToBashSingleQuotedLiteral -Value $TargetPublishDir
    $xAuthorityLiteral = Convert-ToBashSingleQuotedLiteral -Value ("/home/{0}/.Xauthority" -f $User)
    $previous = $env:SSHPASS
    $env:SSHPASS = $Secret
    try {
        Invoke-NativeCommand -FilePath 'sshpass' -ArgumentList @(
            '-e',
            'ssh',
            '-o',
            'StrictHostKeyChecking=no',
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

    $localScriptPath = New-TemporaryScriptPath -LeafName 'dnppv2-linux-config-window-validation.sh'
    $remoteScriptPath = "$TargetPublishDir/run-validation.sh"
    $scriptLines = @(
        '#!/usr/bin/env bash',
        'set -euo pipefail',
        'export DISPLAY=:0',
        "export XAUTHORITY=$xAuthorityLiteral",
        'export XDG_RUNTIME_DIR=/run/user/1000',
        "ART=$remotePublishDirLiteral",
        'cd "$ART"',
        'chmod +x ./DoNotPanicPortfolioVisualizer.App',
        'rm -f general.png validation.png run.log step.log',
        './DoNotPanicPortfolioVisualizer.App > run.log 2>&1 &',
        'APPPID=$!',
        'echo "APPPID=$APPPID" >> step.log',
        'cleanup() {',
        '  if kill -0 "$APPPID" 2>/dev/null; then',
        '    kill "$APPPID" 2>/dev/null || true',
        '    sleep 2',
        '    kill -9 "$APPPID" 2>/dev/null || true',
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
        'sleep 2',
        'eval "$(xdotool getwindowgeometry --shell "$WID")"',
        'echo "X=$X Y=$Y W=$WIDTH H=$HEIGHT" >> step.log',
        'BG_X=$((X + (WIDTH * 384 / 1000)))',
        'BG_Y=$((Y + (HEIGHT * 349 / 1000)))',
        'ADV_X=$((X + (WIDTH * 129 / 1000)))',
        'ADV_Y=$((Y + (HEIGHT * 192 / 1000)))',
        'RSS_X=$((X + (WIDTH * 408 / 1000)))',
        'RSS_Y=$((Y + (HEIGHT * 434 / 1000)))',
        'scrot -o general.png',
        'xdotool mousemove "$BG_X" "$BG_Y" click 1',
        'sleep 1',
        'xdotool key ctrl+a',
        'sleep 1',
        'xdotool key BackSpace',
        'sleep 1',
        'xdotool type --delay 20 /tmp/dnppv2-backgrounds',
        'sleep 1',
        'xdotool mousemove "$ADV_X" "$ADV_Y" click 1',
        'sleep 2',
        'xdotool mousemove "$RSS_X" "$RSS_Y" click 1',
        'sleep 1',
        'xdotool key ctrl+a',
        'sleep 1',
        'xdotool key BackSpace',
        'sleep 1',
        'xdotool type --delay 20 not-a-url',
        'sleep 2',
        'scrot -o validation.png',
        'echo "VALIDATION_DONE" >> step.log'
    )
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
            "$User@$HostName",
            "bash $remoteScriptPath"
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

    foreach ($artifactName in @('general.png', 'validation.png', 'run.log', 'step.log')) {
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
        [Parameter(Mandatory = $true)][string]$TaskName
    )

    $targetPublishDirPsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $TargetPublishDir
    $taskNamePsLiteral = Convert-ToPowerShellSingleQuotedLiteral -Value $TaskName
    Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText "New-Item -ItemType Directory -Force -Path $targetPublishDirPsLiteral | Out-Null"
    Copy-ToRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Join-Path $SourcePublishDir '.') -DestinationPath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path "$TargetPublishDir/")

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
        '$proc = Start-Process -FilePath $exePath -WorkingDirectory $artifactDir -PassThru',
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
        '    Start-Sleep -Seconds 2',
        '',
        '    $rect = New-Object DnppvRemoteNative+RECT',
        '    [DnppvRemoteNative]::GetWindowRect($proc.MainWindowHandle, [ref]$rect) | Out-Null',
        '    $width = $rect.Right - $rect.Left',
        '    $height = $rect.Bottom - $rect.Top',
        '    Add-Content -Path $stepPath -Value (''RECT={0},{1},{2},{3}'' -f $rect.Left, $rect.Top, $width, $height)',
        '',
        '    $backgroundX = $rect.Left + [int][Math]::Round($width * 0.384)',
        '    $backgroundY = $rect.Top + [int][Math]::Round($height * 0.349)',
        '    $advancedX = $rect.Left + [int][Math]::Round($width * 0.129)',
        '    $advancedY = $rect.Top + [int][Math]::Round($height * 0.192)',
        '    $rssX = $rect.Left + [int][Math]::Round($width * 0.408)',
        '    $rssY = $rect.Top + [int][Math]::Round($height * 0.434)',
        '',
        '    Save-WindowScreenshot -Rect $rect -Path (Join-Path $artifactDir ''general.png'')',
        '',
        '    Click-Point -X $backgroundX -Y $backgroundY',
        '    [System.Windows.Forms.SendKeys]::SendWait(''^a'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''{BACKSPACE}'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''D:\TEMP\dnppv2-backgrounds'')',
        '    Start-Sleep -Milliseconds 500',
        '',
        '    Click-Point -X $advancedX -Y $advancedY',
        '    Start-Sleep -Seconds 2',
        '',
        '    Click-Point -X $rssX -Y $rssY',
        '    [System.Windows.Forms.SendKeys]::SendWait(''^a'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''{BACKSPACE}'')',
        '    Start-Sleep -Milliseconds 300',
        '    [System.Windows.Forms.SendKeys]::SendWait(''not-a-url'')',
        '    Start-Sleep -Seconds 2',
        '',
        '    Save-WindowScreenshot -Rect $rect -Path (Join-Path $artifactDir ''validation.png'')',
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
Remove-Item -Force -ErrorAction SilentlyContinue `$donePath, (Join-Path `$artifactDir 'general.png'), (Join-Path `$artifactDir 'validation.png'), (Join-Path `$artifactDir 'step.log')
try {
    Unregister-ScheduledTask -TaskName `$taskName -Confirm:`$false -ErrorAction SilentlyContinue | Out-Null
}
catch {
}

try {
    `$escapedScriptPath = `$scriptPath.Replace('"', '""')
    `$actionArgs = '-NoProfile -ExecutionPolicy Bypass -File "' + `$escapedScriptPath + '"'
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
        'DONE_FILE_MISSING'
    }
}
finally {
    Unregister-ScheduledTask -TaskName `$taskName -Confirm:`$false -ErrorAction SilentlyContinue | Out-Null
}
"@
    Invoke-RemotePowerShell -User $User -HostName $HostName -Secret $Secret -ScriptText $remoteDriver

    foreach ($artifactName in @('general.png', 'validation.png', 'step.log', 'done.txt')) {
        Copy-FromRemote -User $User -HostName $HostName -Secret $Secret -SourcePath (Convert-ToScpRemotePath -TargetPlatform 'windows' -Path (Join-Path $TargetPublishDir $artifactName)) -DestinationPath (Join-Path $ArtifactRoot $artifactName)
    }
}

Assert-RequiredTool -Name 'sshpass'
Assert-RequiredTool -Name 'ssh'
Assert-RequiredTool -Name 'scp'

$resolvedPublishDir = (Resolve-Path -LiteralPath $LocalPublishDir -ErrorAction Stop).Path
if (-not (Test-Path -LiteralPath (Join-Path $resolvedPublishDir 'DoNotPanicPortfolioVisualizer.App.exe') -PathType Leaf) -and
    -not (Test-Path -LiteralPath (Join-Path $resolvedPublishDir 'DoNotPanicPortfolioVisualizer.App') -PathType Leaf)) {
    throw "Local publish directory does not contain the expected app binary: $resolvedPublishDir"
}

Assert-SafeRemoteInputs -TargetPlatform $Platform -HostName $RemoteHost -User $RemoteUser -PublishDir $RemotePublishDir -TaskLabel $WindowsTaskName

New-Item -ItemType Directory -Force -Path $LocalArtifactRoot | Out-Null

switch ($Platform) {
    'linux' {
        Invoke-LinuxValidation -HostName $RemoteHost -User $RemoteUser -Secret $Password -SourcePublishDir $resolvedPublishDir -TargetPublishDir $RemotePublishDir -ArtifactRoot $LocalArtifactRoot -Timeout $TimeoutSeconds
        break
    }
    'windows' {
        Invoke-WindowsValidation -HostName $RemoteHost -User $RemoteUser -Secret $Password -SourcePublishDir $resolvedPublishDir -TargetPublishDir $RemotePublishDir -ArtifactRoot $LocalArtifactRoot -Timeout $TimeoutSeconds -TaskName $WindowsTaskName
        break
    }
    default {
        throw "Unsupported platform: $Platform"
    }
}

Write-Output "CONFIG_WINDOW_VALIDATION=Passed;PLATFORM=$Platform;ARTIFACT_ROOT=$LocalArtifactRoot"
