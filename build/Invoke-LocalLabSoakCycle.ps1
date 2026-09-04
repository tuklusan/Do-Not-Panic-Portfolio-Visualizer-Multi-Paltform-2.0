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
    [ValidateRange(1, 240)]
    [int]$DurationMinutes,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$LocalPublishRoot,

    [Parameter()]
    [string]$InventoryPath = (Join-Path $PSScriptRoot 'vm/remote-test-machines.local.txt'),

    [Parameter()]
    [string]$ArtifactRoot = (Join-Path $env:TEMP ("dnppv2-local-cycle-{0:yyyyMMdd-HHmmss}" -f (Get-Date))),

    [Parameter()]
    [ValidateRange(30, 14400)]
    [int]$TimeoutSeconds = 180,

    [Parameter()]
    [ValidateRange(30, 180)]
    [int]$SceneWarmupSeconds = 30,

    [Parameter()]
    [string]$MachineName,

    [Parameter()]
    [string]$AvailabilityManifestPath,

    [Parameter()]
    [switch]$ProbeOnly,

    [Parameter()]
    [switch]$SkipAvailabilityProbe
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$resolvedPublishRoot = (Resolve-Path -LiteralPath $LocalPublishRoot -ErrorAction Stop).Path
$resolvedArtifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
New-Item -ItemType Directory -Path $resolvedArtifactRoot -Force | Out-Null

$probePath = Join-Path $PSScriptRoot 'Test-LocalLabAvailability.ps1'
$availabilityPath = if ($SkipAvailabilityProbe) {
    if ([string]::IsNullOrWhiteSpace($AvailabilityManifestPath)) {
        throw '-AvailabilityManifestPath is required with -SkipAvailabilityProbe.'
    }
    (Resolve-Path -LiteralPath $AvailabilityManifestPath -ErrorAction Stop).Path
}
else {
    $null = & $probePath -InventoryPath $InventoryPath -ArtifactRoot $resolvedArtifactRoot
    Join-Path $resolvedArtifactRoot 'local-lab-availability.json'
}
$availability = Get-Content -LiteralPath $availabilityPath -Raw | ConvertFrom-Json

$inventory = @{}
foreach ($line in Get-Content -LiteralPath $InventoryPath) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) { continue }
    $parts = $trimmed.Split('|', 3)
    if ($parts.Count -ne 3) { throw "Malformed local lab inventory entry: $trimmed" }
    $inventory[$parts[0]] = [ordered]@{ name = $parts[0]; user = $parts[1]; address = $parts[2] }
}

$password = $env:DNPPV_LOCAL_LAB_PASSWORD
if (-not $ProbeOnly -and [string]::IsNullOrWhiteSpace($password) -and @($availability.machines | Where-Object reachable).Count -gt 0) {
    throw 'DNPPV_LOCAL_LAB_PASSWORD must be supplied through the operator secret environment for a non-probe cycle.'
}
if (-not $ProbeOnly) {
    foreach ($tool in @('sshpass', 'ssh', 'scp')) {
        if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
            throw "Required local-lab tool is unavailable: $tool"
        }
    }
}

$ridByMachine = @{
    'linux-x64-lxqt' = 'linux-x64'
    'windows-10-reference' = 'win-x64'
    'windows-11-laptop' = 'win-x64'
    'macos-x64-intel-big-sur' = 'osx-x64'
}
$platformByMachine = @{
    'linux-x64-lxqt' = 'linux'
    'windows-10-reference' = 'windows'
    'windows-11-laptop' = 'windows'
}
$remoteRootByMachine = @{
    'linux-x64-lxqt' = '/tmp/dnppv2-local-cycle'
    'windows-10-reference' = 'D:\SW_DEV\DO-NOT-PANIC-2.0\dnppv2-local-cycle'
    'windows-11-laptop' = 'C:\Users\vagab\DNPPV2\dnppv2-local-cycle'
}

function Invoke-RemoteNative {
    param(
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter()][string]$StandardInput,
        [Parameter()][int]$Timeout = 900
    )
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.Environment.Remove('SSHPASS')
    $psi.Environment['SSHPASS'] = $Secret
    $psi.FileName = 'sshpass'
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.RedirectStandardInput = -not [string]::IsNullOrEmpty($StandardInput)
        foreach ($argument in @('-e') + $Arguments) { [void]$psi.ArgumentList.Add($argument) }
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $psi
        try {
            if (-not $process.Start()) { throw 'Could not start sshpass.' }
            if ($psi.RedirectStandardInput) {
                $process.StandardInput.Write($StandardInput)
                $process.StandardInput.Close()
            }
            $stdoutTask = $process.StandardOutput.ReadToEndAsync()
            $stderrTask = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit($Timeout * 1000)) {
                $process.Kill($true)
                throw "Remote command timed out after ${Timeout}s."
            }
            $stdout = $stdoutTask.GetAwaiter().GetResult()
            $stderr = $stderrTask.GetAwaiter().GetResult()
            if ($stdout) { Write-Output $stdout.TrimEnd() }
            if ($process.ExitCode -ne 0) { throw "Remote command failed with exit code $($process.ExitCode): $($stderr.Trim())" }
        }
        finally { $process.Dispose() }
}

function Copy-RemoteTree {
    param(
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$RemotePath,
        [Parameter(Mandatory = $true)][string]$LocalPath,
        [Parameter()][int]$Timeout = 900
    )
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.Environment.Remove('SSHPASS')
    $psi.Environment['SSHPASS'] = $Secret
    $psi.FileName = 'sshpass'
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        foreach ($argument in @('-e', 'scp', '-r', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', "${User}@${HostName}:$RemotePath", $LocalPath)) {
            [void]$psi.ArgumentList.Add($argument)
        }
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $psi
        try {
            if (-not $process.Start()) { throw 'Could not start remote artifact copy.' }
            $stdoutTask = $process.StandardOutput.ReadToEndAsync()
            $stderrTask = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit($Timeout * 1000)) { $process.Kill($true); throw "Artifact copy timed out after ${Timeout}s." }
            [void]$stdoutTask.GetAwaiter().GetResult()
            $stderr = $stderrTask.GetAwaiter().GetResult()
            if ($process.ExitCode -ne 0) { throw "Artifact copy failed with exit code $($process.ExitCode): $($stderr.Trim())" }
        }
        finally { $process.Dispose() }
}

function Copy-LocalTree {
    param(
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$HostName,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$LocalPath,
        [Parameter(Mandatory = $true)][string]$RemotePath,
        [Parameter()][int]$Timeout = 900
    )
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.Environment.Remove('SSHPASS')
    $psi.Environment['SSHPASS'] = $Secret
    $psi.FileName = 'sshpass'
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $sourcePaths = @($LocalPath)
        if ($LocalPath.EndsWith('*', [StringComparison]::Ordinal)) {
            $sourceRoot = $LocalPath.Substring(0, $LocalPath.Length - 1).TrimEnd('\', '/')
            $sourcePaths = @(Get-ChildItem -LiteralPath $sourceRoot -Force | Select-Object -ExpandProperty FullName)
        }
        if ($sourcePaths.Count -eq 0) { throw "Local copy source is empty: $LocalPath" }
        foreach ($argument in @('-e', 'scp', '-r', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1') + $sourcePaths + "${User}@${HostName}:$RemotePath") {
            [void]$psi.ArgumentList.Add($argument)
        }
        $process = [Diagnostics.Process]::new()
        $process.StartInfo = $psi
        try {
            if (-not $process.Start()) { throw 'Could not start local artifact deployment.' }
            $stdoutTask = $process.StandardOutput.ReadToEndAsync()
            $stderrTask = $process.StandardError.ReadToEndAsync()
            if (-not $process.WaitForExit($Timeout * 1000)) { $process.Kill($true); throw "Artifact deployment timed out after ${Timeout}s." }
            [void]$stdoutTask.GetAwaiter().GetResult()
            $stderr = $stderrTask.GetAwaiter().GetResult()
            if ($process.ExitCode -ne 0) { throw "Artifact deployment failed with exit code $($process.ExitCode): $($stderr.Trim())" }
        }
        finally { $process.Dispose() }
}

function Resolve-PublishDirectory {
    param([Parameter(Mandatory = $true)][string]$Rid)
    $candidate = Join-Path $resolvedPublishRoot $Rid
    if (Test-Path -LiteralPath $candidate -PathType Container) { return (Resolve-Path -LiteralPath $candidate).Path }
    if (Test-Path -LiteralPath $resolvedPublishRoot -PathType Container) {
        foreach ($executableName in @('DoNotPanicPortfolioVisualizer.App.exe', 'DoNotPanicPortfolioVisualizer.App', 'DoNotPanicPortfolioVisualizer')) {
            if (Test-Path -LiteralPath (Join-Path $resolvedPublishRoot $executableName) -PathType Leaf) { return $resolvedPublishRoot }
        }
    }
    throw "Publish directory for RID $Rid is missing below $resolvedPublishRoot"
}

function Assert-RemoteProductProcessesClean {
    param(
        [Parameter(Mandatory = $true)][hashtable]$MachineRecord,
        [Parameter(Mandatory = $true)][string]$Platform,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][int]$Timeout
    )

    if ($MachineRecord.user -notmatch '^[A-Za-z0-9._-]+$') {
        throw "Local lab inventory user contains unsupported characters: $($MachineRecord.user)"
    }
    if ($MachineRecord.address -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]*$') {
        throw "Local lab inventory address contains unsupported characters: $($MachineRecord.address)"
    }

    $target = "$($MachineRecord.user)@$($MachineRecord.address)"
    if ($Platform -eq 'windows') {
        # Windows OpenSSH commonly dispatches through cmd.exe. Encode the
        # PowerShell payload so cmd cannot reinterpret its pipeline syntax.
        $payload = "Get-Process | Where-Object { `$_.ProcessName -like '*DoNotPanicPortfolioVisualizer*' -or `$_.ProcessName -like '*YFinance.NET.Server*' } | Stop-Process -Force -ErrorAction SilentlyContinue; if (Get-Process | Where-Object { `$_.ProcessName -like '*DoNotPanicPortfolioVisualizer*' -or `$_.ProcessName -like '*YFinance.NET.Server*' }) { exit 17 }"
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($payload))
        $command = "powershell.exe -NoProfile -NonInteractive -EncodedCommand $encoded"
        Invoke-RemoteNative -User $MachineRecord.user -HostName $MachineRecord.address -Secret $Secret -Arguments @(
            'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no',
            '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no',
            '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60', $target, $command
        ) -Timeout $Timeout
    }
    else {
        # Deliver the cleanup script over stdin. This keeps its process-name
        # patterns out of the SSH command line being inspected.
        $command = @'
pids=$(ps -eo pid=,user=,args= | awk -v u="$(id -un)" '$2 == u && $0 ~ /[D]oNotPanicPortfolioVisualizer|[Y]Finance.NET.Server/ {print $1}')
if [ -n "$pids" ]; then kill -TERM $pids 2>/dev/null || true; sleep 1; kill -KILL $pids 2>/dev/null || true; sleep 2; fi
remaining=$(ps -eo pid=,user=,args= | awk -v u="$(id -un)" '$2 == u && $0 ~ /[D]oNotPanicPortfolioVisualizer|[Y]Finance.NET.Server/ {print $1}')
if [ -n "$remaining" ]; then exit 17; fi
'@
        Invoke-RemoteNative -User $MachineRecord.user -HostName $MachineRecord.address -Secret $Secret -StandardInput $command -Arguments @(
            'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no',
            '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no',
            '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60', $target, 'bash -s'
        ) -Timeout $Timeout
    }

}

$cycle = [ordered]@{
    schema = 'dnppv2-local-lab-cycle/v1'
    cycleId = [IO.Path]::GetFileName($resolvedArtifactRoot)
    startedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    durationMinutes = $DurationMinutes
    machines = [Collections.Generic.List[object]]::new()
}
$cyclePath = if ([string]::IsNullOrWhiteSpace($MachineName)) {
    Join-Path $resolvedArtifactRoot 'local-lab-cycle.json'
}
else {
    Join-Path (Join-Path $resolvedArtifactRoot $MachineName) "$MachineName-machine-result.json"
}
function Save-CycleManifest {
    $cycle | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $cyclePath -Encoding utf8
}

if ([string]::IsNullOrWhiteSpace($MachineName)) {
    if ($ProbeOnly) {
        foreach ($record in @($availability.machines)) {
            $cycle.machines.Add([ordered]@{
                name = $record.name
                address = $record.address
                user = $record.user
                status = if ($record.reachable) { 'AvailableForCycleProbeOnly' } else { 'UnavailableAtCycleStart' }
                artifactRoot = Join-Path $resolvedArtifactRoot $record.name
            })
        }
        $cycle.completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        Save-CycleManifest
        Write-Output "LOCAL_LAB_CYCLE_PROBE=Recorded;CYCLE=$($cycle.cycleId);ARTIFACT=$resolvedArtifactRoot"
        return
    }

    $childProcesses = [Collections.Generic.List[object]]::new()
    $pwshPath = (Get-Command pwsh -ErrorAction Stop).Source
    $reachableRecords = @($availability.machines | Where-Object { $_.reachable })
    foreach ($record in $reachableRecords) {
        $machineArtifactRoot = Join-Path $resolvedArtifactRoot $record.name
        New-Item -ItemType Directory -Path $machineArtifactRoot -Force | Out-Null
        $childOutput = Join-Path $machineArtifactRoot 'coordinator-output.txt'
        $childError = Join-Path $machineArtifactRoot 'coordinator-error.txt'
        $childArguments = @(
            '-NoProfile',
            '-File', $PSCommandPath,
            '-DurationMinutes', $DurationMinutes.ToString(),
            '-LocalPublishRoot', $resolvedPublishRoot,
            '-InventoryPath', (Resolve-Path -LiteralPath $InventoryPath).Path,
            '-ArtifactRoot', $resolvedArtifactRoot,
            '-TimeoutSeconds', $TimeoutSeconds.ToString(),
            '-SceneWarmupSeconds', $SceneWarmupSeconds.ToString(),
            '-MachineName', [string]$record.name,
            '-AvailabilityManifestPath', $availabilityPath,
            '-SkipAvailabilityProbe'
        )
        $child = Start-Process -FilePath $pwshPath -ArgumentList $childArguments -WorkingDirectory $repoRoot -RedirectStandardOutput $childOutput -RedirectStandardError $childError -PassThru
        $childProcesses.Add([pscustomobject]@{ record = $record; process = $child; artifactRoot = $machineArtifactRoot; output = $childOutput })
    }

    foreach ($childInfo in $childProcesses) {
        $timeoutMilliseconds = [int64]($TimeoutSeconds + ($DurationMinutes * 60) + 1800) * 1000
        if (-not $childInfo.process.WaitForExit([int]([Math]::Min([int32]::MaxValue, $timeoutMilliseconds)))) {
            try { $childInfo.process.Kill($true) } catch { }
            $childInfo.process.WaitForExit()
            Set-Content -LiteralPath (Join-Path $childInfo.artifactRoot 'machine-result.json') -Value (@{
                name = $childInfo.record.name
                address = $childInfo.record.address
                user = $childInfo.record.user
                status = 'Failed'
                failure = "Machine child process timed out after $($timeoutMilliseconds / 1000)s."
                artifactRoot = $childInfo.artifactRoot
            } | ConvertTo-Json -Depth 8) -Encoding utf8
        }

        $resultPath = Join-Path $childInfo.artifactRoot "$($childInfo.record.name)-machine-result.json"
        if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
            $childResult = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
            $childMachine = @($childResult.machines) | Select-Object -First 1
            if ($null -ne $childMachine -and $null -ne $childMachine.status) {
                $cycle.machines.Add($childMachine)
            }
            else {
                $cycle.machines.Add([ordered]@{
                    name = $childInfo.record.name
                    address = $childInfo.record.address
                    user = $childInfo.record.user
                    status = 'Failed'
                    failure = "Machine child returned an invalid result manifest. See $($childInfo.output)."
                    artifactRoot = $childInfo.artifactRoot
                })
            }
        }
        else {
            $cycle.machines.Add([ordered]@{
                name = $childInfo.record.name
                address = $childInfo.record.address
                user = $childInfo.record.user
                status = 'Failed'
                failure = "Machine child process returned without a result manifest. See $($childInfo.output)."
                artifactRoot = $childInfo.artifactRoot
            })
        }
    }

    foreach ($record in @($availability.machines | Where-Object { -not $_.reachable })) {
        $cycle.machines.Add([ordered]@{
            name = $record.name
            address = $record.address
            user = $record.user
            status = 'UnavailableAtCycleStart'
            artifactRoot = Join-Path $resolvedArtifactRoot $record.name
        })
    }
    $cycle.completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Save-CycleManifest
    if (@($cycle.machines | Where-Object { $_.status -eq 'Failed' }).Count -gt 0) { throw "Local lab cycle failed; see $cyclePath" }
    Write-Output "LOCAL_LAB_CYCLE=Recorded;CYCLE=$($cycle.cycleId);ARTIFACT=$resolvedArtifactRoot"
    return
}

foreach ($record in @($availability.machines)) {
    if ($record.name -ne $MachineName) { continue }
    $machine = [ordered]@{
        name = $record.name
        address = $record.address
        user = $record.user
        status = if ($record.reachable) { 'Pending' } else { 'UnavailableAtCycleStart' }
        artifactRoot = Join-Path $resolvedArtifactRoot $record.name
    }
    New-Item -ItemType Directory -Path $machine.artifactRoot -Force | Out-Null

    if (-not $record.reachable -or $ProbeOnly) {
        if ($ProbeOnly -and $record.reachable) { $machine.status = 'AvailableForCycleProbeOnly' }
        $cycle.machines.Add($machine)
        Save-CycleManifest
        continue
    }

    if (-not $inventory.ContainsKey($record.name)) { throw "Availability record is not present in inventory: $($record.name)" }
    $machineRecord = $inventory[$record.name]
    $rid = $ridByMachine[$record.name]
    $publish = Resolve-PublishDirectory -Rid $rid
    $machine.status = 'Running'
    $machine.rid = $rid
    $machine.startedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    $remoteCleanupRoot = $null
    $remotePublish = if ($platformByMachine.ContainsKey($record.name) -and $platformByMachine[$record.name] -eq 'linux') {
        "$($remoteRootByMachine[$record.name])/$($cycle.cycleId)"
    }
    elseif ($platformByMachine.ContainsKey($record.name)) {
        Join-Path $remoteRootByMachine[$record.name] $cycle.cycleId
    }
    else {
        $null
    }
    $macTimeout = [Math]::Max(900, $TimeoutSeconds + 300)

    try {
        if ($platformByMachine.ContainsKey($record.name)) {
            Assert-RemoteProductProcessesClean -MachineRecord $machineRecord -Platform $platformByMachine[$record.name] -Secret $password -Timeout ([Math]::Min(300, $TimeoutSeconds))
            $driver = Join-Path $PSScriptRoot 'vm/Invoke-ProductSceneValidation.ps1'
            & $driver `
                -Platform $platformByMachine[$record.name] `
                -RemoteHost $machineRecord.address `
                -RemoteUser $machineRecord.user `
                -Password $password `
                -LocalPublishDir $publish `
                -RemotePublishDir $remotePublish `
                -LocalArtifactRoot $machine.artifactRoot `
                -TimeoutSeconds $TimeoutSeconds `
                -SceneWarmupSeconds $SceneWarmupSeconds `
                -SoakDurationMinutes $DurationMinutes `
                | Tee-Object -FilePath (Join-Path $machine.artifactRoot 'harness-output.txt')
        }
        else {
            Assert-RemoteProductProcessesClean -MachineRecord $machineRecord -Platform 'macos' -Secret $password -Timeout ([Math]::Min(300, $macTimeout))
            $driver = Join-Path $PSScriptRoot 'vm/Invoke-MacConfigWindowValidation.sh'
            if (-not (Test-Path -LiteralPath $driver -PathType Leaf)) { throw "Mac driver is missing: $driver" }
            $remoteRoot = "/Users/$($machineRecord.user)/SOFTWARE_DEV/DNPPV_20/dnppv2-local-cycle-$($cycle.cycleId)"
            $remoteCleanupRoot = $remoteRoot
            $remotePublish = "$remoteRoot/publish-cr019"
            $remoteArtifact = "$remoteRoot/artifacts"
            $driverRemote = "$remoteRoot/Invoke-MacConfigWindowValidation.sh"
            $remote = "$($machineRecord.user)@$($machineRecord.address):$remoteRoot"
            Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $password -Arguments @(
                'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                "$($machineRecord.user)@$($machineRecord.address)",
                "if [ -e '$remoteRoot' ]; then echo 'MAC_STORAGE_HARD_STOP=CycleRootAlreadyExists' >&2; exit 2; fi; mkdir -p -- '$remotePublish' '$remoteArtifact'"
            ) -Timeout $macTimeout
            # Copy the publish contents into the already-created canonical
            # directory. Copying the directory itself is scp-layout dependent
            # and can leave the executable one level deeper on macOS.
            Copy-LocalTree -User $machineRecord.user -HostName $machineRecord.address -Secret $password -LocalPath (Join-Path $publish '*') -RemotePath "$remotePublish/" -Timeout $macTimeout
            Copy-LocalTree -User $machineRecord.user -HostName $machineRecord.address -Secret $password -LocalPath $driver -RemotePath $remoteRoot -Timeout $macTimeout
            Copy-RemoteTree -User $machineRecord.user -HostName $machineRecord.address -Secret $password -RemotePath $driverRemote -LocalPath $machine.artifactRoot -Timeout $macTimeout
            $openRouterKey = if ($env:DNPPV_OPENROUTER_API_KEY) { $env:DNPPV_OPENROUTER_API_KEY } elseif ($env:OPENROUTER_API_KEY) { $env:OPENROUTER_API_KEY } else { $env:OPENROUTER_AI_API_KEY }
            $remoteCommand = ''
            $remoteStdin = $null
            if (-not [string]::IsNullOrWhiteSpace($openRouterKey)) {
                # The fixed remote command consumes the key as stdin data. It
                # is never placed in an argument, shell command, or remote file.
                $remoteCommand = "IFS= read -r DNPPV_OPENROUTER_API_KEY; export DNPPV_OPENROUTER_API_KEY; export DNPPV_SOAK_REQUIRE_AI_NEWS=1; "
                $remoteStdin = "$openRouterKey`n"
            }
            else {
                $remoteCommand = 'unset DNPPV_OPENROUTER_API_KEY DNPPV_SOAK_REQUIRE_AI_NEWS; '
            }
            $remoteCommand += "chmod +x '$driverRemote' && exec bash '$driverRemote' '$remoteRoot' '$remoteArtifact' '$DurationMinutes'"
            Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $password -Arguments @(
                'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                "$($machineRecord.user)@$($machineRecord.address)",
                $remoteCommand
            ) -StandardInput $remoteStdin -Timeout ($TimeoutSeconds + ($DurationMinutes * 60) + 900) | Tee-Object -FilePath (Join-Path $machine.artifactRoot 'harness-output.txt')
            Copy-RemoteTree -User $machineRecord.user -HostName $machineRecord.address -Secret $password -RemotePath "$remoteArtifact/*" -LocalPath $machine.artifactRoot -Timeout 900
        }
        $machine.status = 'Passed'
    }
    catch {
        $primaryFailure = $_.Exception.Message
        if ($null -ne $remoteCleanupRoot) {
            try {
                # Preserve failure evidence before the remote cycle root is
                # removed; this is key-free trace and screenshot material.
                Copy-RemoteTree -User $machineRecord.user -HostName $machineRecord.address -Secret $password -RemotePath "$remoteArtifact/*" -LocalPath $machine.artifactRoot -Timeout 900
            }
            catch {
                $machine.failure = "$primaryFailure; failure-artifact-retrieval=$($_.Exception.Message)"
            }
        }
        $machine.status = 'Failed'
        if (-not $machine.Contains('failure') -or [string]::IsNullOrWhiteSpace([string]$machine['failure'])) {
            $machine['failure'] = $primaryFailure
        }
    }
    finally {
        if ($null -ne $remoteCleanupRoot) {
            try {
                Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $password -Arguments @(
                    'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                    "$($machineRecord.user)@$($machineRecord.address)",
                    "if [ -d '$remoteCleanupRoot' ]; then rm -rf -- '$remoteCleanupRoot'; fi"
                ) -Timeout $macTimeout | Out-Null
            }
            catch {
                $machine.status = 'Failed'
                $machine.cleanupFailure = $_.Exception.Message
            }
        }
        $machine.completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        $cycle.machines.Add($machine)
        Save-CycleManifest
    }
}

$cycle.completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
Save-CycleManifest
if (@($cycle.machines | Where-Object { $_.status -eq 'Failed' }).Count -gt 0) { throw "Local lab cycle failed; see $cyclePath" }
Write-Output "LOCAL_LAB_CYCLE=Recorded;CYCLE=$($cycle.cycleId);ARTIFACT=$resolvedArtifactRoot"
