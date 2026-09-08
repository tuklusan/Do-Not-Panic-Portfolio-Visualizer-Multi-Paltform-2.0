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
        $payload = @'
$matches = Get-Process | Where-Object { $_.ProcessName -like '*DoNotPanicPortfolioVisualizer*' -or $_.ProcessName -like '*YFinance.NET.Server*' }
$matches | Stop-Process -Force -ErrorAction SilentlyContinue
for ($attempt = 0; $attempt -lt 20; $attempt++) {
    $matches = Get-Process | Where-Object { $_.ProcessName -like '*DoNotPanicPortfolioVisualizer*' -or $_.ProcessName -like '*YFinance.NET.Server*' }
    if (@($matches).Count -eq 0) { exit 0 }
    Start-Sleep -Milliseconds 250
}
$matches = Get-Process | Where-Object { $_.ProcessName -like '*DoNotPanicPortfolioVisualizer*' -or $_.ProcessName -like '*YFinance.NET.Server*' }
if (@($matches).Count -gt 0) { exit 17 }
'@
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

function Remove-InterruptedCycleRoots {
    param(
        [Parameter(Mandatory = $true)][object[]]$MachineRecords,
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][int]$Timeout,
        [Parameter(Mandatory = $true)][string]$CycleId,
        [Parameter(Mandatory = $true)][hashtable]$InventoryRecords,
        [Parameter(Mandatory = $true)][hashtable]$PlatformRecords,
        [Parameter(Mandatory = $true)][hashtable]$RemoteRoots,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[string]]$CleanupFailures
    )

    foreach ($record in $MachineRecords) {
        if (-not $record.reachable) { continue }
        $machineRecord = $InventoryRecords[$record.name]
        $platform = if ($PlatformRecords.ContainsKey($record.name)) { $PlatformRecords[$record.name] } else { 'macos' }
        try {
            Assert-RemoteProductProcessesClean -MachineRecord $machineRecord -Platform $platform -Secret $Secret -Timeout ([Math]::Min(300, $Timeout))
            if ($platform -eq 'windows') {
                $root = Join-Path $RemoteRoots[$record.name] $CycleId
                $rootLiteral = "'" + $root.Replace("'", "''") + "'"
                $ownerLiteral = "'" + $CycleId.Replace("'", "''") + "'"
                $ownerPathLiteral = "'" + (Join-Path $root '.dnppv2-cycle-owner').Replace("'", "''") + "'"
                $payload = "if (Test-Path -LiteralPath $rootLiteral) { if ((Test-Path -LiteralPath $ownerPathLiteral) -and ((Get-Content -LiteralPath $ownerPathLiteral -Raw).Trim() -eq $ownerLiteral)) { Remove-Item -LiteralPath $rootLiteral -Recurse -Force -ErrorAction SilentlyContinue } else { throw 'WINDOWS_STORAGE_HARD_STOP=UnownedCycleRoot' } }"
                $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($payload))
                Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $Secret -Arguments @(
                    'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                    "$($machineRecord.user)@$($machineRecord.address)", 'powershell.exe', '-NoProfile', '-NonInteractive', '-EncodedCommand', $encoded
                ) -Timeout $Timeout | Out-Null
            }
            elseif ($platform -eq 'linux') {
                $root = "$($RemoteRoots[$record.name])/$CycleId"
                Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $Secret -Arguments @(
                    'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                    "$($machineRecord.user)@$($machineRecord.address)", "if [ -e '$root' ]; then if [ ! -f '$root/.dnppv2-cycle-owner' ] || ! grep -Fqx '$CycleId' '$root/.dnppv2-cycle-owner'; then echo 'LINUX_STORAGE_HARD_STOP=UnownedCycleRoot' >&2; exit 2; fi; rm -rf -- '$root'; fi"
                ) -Timeout $Timeout | Out-Null
            }
            else {
                $root = "/Users/$($machineRecord.user)/SOFTWARE_DEV/DNPPV_20/dnppv2-local-cycle-$CycleId"
                Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $Secret -Arguments @(
                    'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                    "$($machineRecord.user)@$($machineRecord.address)", "if [ -e '$root' ]; then if [ ! -f '$root/.dnppv2-cycle-owner' ] || ! grep -Fqx '$CycleId' '$root/.dnppv2-cycle-owner'; then echo 'MAC_STORAGE_HARD_STOP=UnownedCycleRoot' >&2; exit 2; fi; rm -rf -- '$root'; fi"
                ) -Timeout $Timeout | Out-Null
            }
        }
        catch {
            $message = "Interrupted cycle cleanup failed for $($record.name): $($_.Exception.Message)"
            $CleanupFailures.Add($message)
            Write-Warning $message
        }
    }
}

$cycle = [ordered]@{
    schema = 'dnppv2-local-lab-cycle/v1'
    cycleId = [IO.Path]::GetFileName($resolvedArtifactRoot)
    startedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    durationMinutes = $DurationMinutes
    machines = [Collections.Generic.List[object]]::new()
}
$cleanupFailures = [Collections.Generic.List[string]]::new()
if ($cycle.cycleId -notmatch '^dnppv2-local-cycle-[A-Za-z0-9._-]+$') {
    throw "Artifact root basename is not a safe local cycle identity: $($cycle.cycleId)"
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
        $childStartParameters = @{
            FilePath = $pwshPath
            ArgumentList = $childArguments
            WorkingDirectory = $repoRoot
            RedirectStandardOutput = $childOutput
            RedirectStandardError = $childError
            PassThru = $true
        }
        if ($IsWindows) {
            $childStartParameters.WindowStyle = 'Hidden'
        }
        $child = Start-Process @childStartParameters
        $childProcesses.Add([pscustomobject]@{ record = $record; process = $child; artifactRoot = $machineArtifactRoot; output = $childOutput })
    }

    $timeoutMilliseconds = [int64]($TimeoutSeconds + ($DurationMinutes * 60) + 1800) * 1000
    $childDeadline = [DateTime]::UtcNow.AddMilliseconds($timeoutMilliseconds)
    $completedChildren = [Collections.Generic.HashSet[int]]::new()
    $cycleFailureObserved = $false
    while ($completedChildren.Count -lt $childProcesses.Count) {
        foreach ($childInfo in $childProcesses) {
            $childId = [int]$childInfo.process.Id
            if ($completedChildren.Contains($childId)) { continue }
            $childInfo.process.Refresh()
            if (-not $childInfo.process.HasExited) { continue }
            $completedChildren.Add($childId) | Out-Null
            if ($childInfo.process.ExitCode -ne 0) { $cycleFailureObserved = $true }
        }

        if ($cycleFailureObserved) {
            foreach ($childInfo in $childProcesses) {
                $childId = [int]$childInfo.process.Id
                $childInfo.process.Refresh()
                $wasRunning = -not $childInfo.process.HasExited
                if ($wasRunning) {
                    try { $childInfo.process.Kill($true) } catch { }
                }
                if ($wasRunning) {
                    try { $childInfo.process.WaitForExit(30000) | Out-Null } catch { }
                }
                $completedChildren.Add($childId) | Out-Null
                $fallbackManifest = Join-Path $childInfo.artifactRoot 'machine-result.json'
                $namedManifest = Join-Path $childInfo.artifactRoot "$($childInfo.record.name)-machine-result.json"
                if (-not (Test-Path -LiteralPath $namedManifest -PathType Leaf) -and -not (Test-Path -LiteralPath $fallbackManifest -PathType Leaf)) {
                    Set-Content -LiteralPath $fallbackManifest -Value (@{
                        name = $childInfo.record.name
                        address = $childInfo.record.address
                        user = $childInfo.record.user
                        status = 'Failed'
                        failure = if ($wasRunning) { 'Sibling machine child failed; this cycle was aborted so every started lane could clean up.' } else { 'Machine child exited during a sibling-aborted cycle; no detailed child manifest was produced.' }
                        artifactRoot = $childInfo.artifactRoot
                    } | ConvertTo-Json -Depth 8) -Encoding utf8
                }
            }
            Remove-InterruptedCycleRoots -MachineRecords $reachableRecords -Secret $password -Timeout ([Math]::Max(60, $TimeoutSeconds)) -CycleId $cycle.cycleId -InventoryRecords $inventory -PlatformRecords $platformByMachine -RemoteRoots $remoteRootByMachine -CleanupFailures $cleanupFailures
            if ($cleanupFailures.Count -gt 0) { $cycle.cleanupFailures = @($cleanupFailures) }
            break
        }

        if ([DateTime]::UtcNow -ge $childDeadline) {
            foreach ($childInfo in $childProcesses) {
                $childId = [int]$childInfo.process.Id
                if ($completedChildren.Contains($childId)) { continue }
                try { $childInfo.process.Kill($true) } catch { }
                try { $childInfo.process.WaitForExit(30000) | Out-Null } catch { }
                $completedChildren.Add($childId) | Out-Null
                Set-Content -LiteralPath (Join-Path $childInfo.artifactRoot 'machine-result.json') -Value (@{
                    name = $childInfo.record.name
                    address = $childInfo.record.address
                    user = $childInfo.record.user
                    status = 'Failed'
                    failure = "Machine child process timed out after $($timeoutMilliseconds / 1000)s."
                    artifactRoot = $childInfo.artifactRoot
                } | ConvertTo-Json -Depth 8) -Encoding utf8
            }
            Remove-InterruptedCycleRoots -MachineRecords $reachableRecords -Secret $password -Timeout ([Math]::Max(60, $TimeoutSeconds)) -CycleId $cycle.cycleId -InventoryRecords $inventory -PlatformRecords $platformByMachine -RemoteRoots $remoteRootByMachine -CleanupFailures $cleanupFailures
            if ($cleanupFailures.Count -gt 0) { $cycle.cleanupFailures = @($cleanupFailures) }
            break
        }
        Start-Sleep -Milliseconds 250
    }

    foreach ($childInfo in $childProcesses) {
        $childInfo.process.Refresh()
        $childExitCode = if ($childInfo.process.HasExited) { $childInfo.process.ExitCode } else { 1 }
        $resultPath = Join-Path $childInfo.artifactRoot "$($childInfo.record.name)-machine-result.json"
        if ($childExitCode -ne 0) {
            $cycle.machines.Add([ordered]@{
                name = $childInfo.record.name
                address = $childInfo.record.address
                user = $childInfo.record.user
                status = 'Failed'
                failure = "Machine child process exited with code $childExitCode. See $($childInfo.output)."
                artifactRoot = $childInfo.artifactRoot
            })
        }
        elseif (Test-Path -LiteralPath $resultPath -PathType Leaf) {
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
    $macPublishArchive = $null
    $macArtifactArchive = $null
    $localFailureArchive = $null
    $remoteArtifact = $null
    $remoteArtifactArchive = $null
    $remoteFailureArchive = $null
    $remotePublish = if ($platformByMachine.ContainsKey($record.name) -and $platformByMachine[$record.name] -eq 'linux') {
        "$($remoteRootByMachine[$record.name])/$($cycle.cycleId)"
    }
    elseif ($platformByMachine.ContainsKey($record.name)) {
        Join-Path $remoteRootByMachine[$record.name] $cycle.cycleId
    }
    else {
        $null
    }
    # Bind every platform's cycle root before launch.  If the product does not
    # reach the validation scene within the driver's timeout, the finally block
    # must still remove this root so the next cycle is not blocked by stale
    # storage from an aborted launch.
    if (-not [string]::IsNullOrWhiteSpace([string]$remotePublish)) {
        $remoteCleanupRoot = $remotePublish
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
            $macPublishArchive = Join-Path $machine.artifactRoot 'mac-publish.zip'
            Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $password -Arguments @(
                'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                "$($machineRecord.user)@$($machineRecord.address)",
                # Product/helper processes were stopped immediately before
                # this command and the serialized gate excludes overlap, so a
                # root left by an interrupted cycle is reclaimable only when
                # its ownership marker matches this exact cycle identity.
                "if [ -e '$remoteRoot' ]; then if [ ! -f '$remoteRoot/.dnppv2-cycle-owner' ] || ! grep -Fqx '$($cycle.cycleId)' '$remoteRoot/.dnppv2-cycle-owner'; then echo 'MAC_STORAGE_HARD_STOP=UnownedCycleRoot' >&2; exit 2; fi; rm -rf -- '$remoteRoot'; fi; mkdir -p -- '$remotePublish' '$remoteArtifact'; printf '%s' '$($cycle.cycleId)' > '$remoteRoot/.dnppv2-cycle-owner'"
            ) -Timeout $macTimeout
            # Copy the publish contents into the already-created canonical
            # directory. Copying the directory itself is scp-layout dependent
            # and can leave the executable one level deeper on macOS.
            # Archive the resolved RID directory before transfer. Expanding a
            # self-contained publish into ProcessStartInfo arguments can exceed
            # the Windows command-line limit before sshpass starts.
            [IO.Compression.ZipFile]::CreateFromDirectory($publish, $macPublishArchive)
            Copy-LocalTree -User $machineRecord.user -HostName $machineRecord.address -Secret $password -LocalPath $macPublishArchive -RemotePath "$remoteRoot/mac-publish.zip" -Timeout $macTimeout
            Copy-LocalTree -User $machineRecord.user -HostName $machineRecord.address -Secret $password -LocalPath $driver -RemotePath $remoteRoot -Timeout $macTimeout
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
            $remoteCommand += "if [ -f '$remoteRoot/mac-publish.zip' ]; then /usr/bin/unzip -q '$remoteRoot/mac-publish.zip' -d '$remotePublish'; rm -f '$remoteRoot/mac-publish.zip'; else echo 'MAC_ACCEPTANCE_HARD_STOP=PublishedArchiveMissing' >&2; exit 2; fi; chmod +x '$driverRemote' && exec bash '$driverRemote' '$remoteRoot' '$remoteArtifact' '$DurationMinutes'"
            Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $password -Arguments @(
                'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                "$($machineRecord.user)@$($machineRecord.address)",
                $remoteCommand
            ) -StandardInput $remoteStdin -Timeout ($TimeoutSeconds + ($DurationMinutes * 60) + 900) | Tee-Object -FilePath (Join-Path $machine.artifactRoot 'harness-output.txt')
            # Retrieve one bounded archive. Recursive wildcard SCP is prone to
            # hanging against the slow Big Sur SFTP endpoint.
            $remoteArtifactArchive = "$remoteRoot/artifacts.tar.gz"
            $macArtifactArchive = Join-Path $machine.artifactRoot 'mac-artifacts.tar.gz'
            Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $password -Arguments @(
                'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                "$($machineRecord.user)@$($machineRecord.address)",
                "tar -czf '$remoteArtifactArchive' -C '$remoteArtifact' ."
            ) -Timeout 900
            Copy-RemoteTree -User $machineRecord.user -HostName $machineRecord.address -Secret $password -RemotePath $remoteArtifactArchive -LocalPath $macArtifactArchive -Timeout 900
            & tar -xzf $macArtifactArchive -C $machine.artifactRoot
            if ($LASTEXITCODE -ne 0) { throw "Local Mac artifact archive extraction failed with exit code $LASTEXITCODE." }
        }
        $machine.status = 'Passed'
    }
    catch {
        $primaryFailure = $_.Exception.Message
        if ($null -ne $remoteCleanupRoot) {
            try {
                # Preserve failure evidence before the remote cycle root is
                # removed; this is key-free trace and screenshot material.
                if (-not [string]::IsNullOrWhiteSpace($remoteArtifact)) {
                    $remoteFailureArchive = "$remoteCleanupRoot/failure-artifacts.tar.gz"
                    Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $password -Arguments @(
                        'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                        "$($machineRecord.user)@$($machineRecord.address)",
                        "if [ -d '$remoteArtifact' ]; then tar -czf '$remoteFailureArchive' -C '$remoteArtifact' .; fi"
                    ) -Timeout 900
                    $localFailureArchive = Join-Path $machine.artifactRoot 'mac-failure-artifacts.tar.gz'
                    Copy-RemoteTree -User $machineRecord.user -HostName $machineRecord.address -Secret $password -RemotePath $remoteFailureArchive -LocalPath $localFailureArchive -Timeout 900
                    & tar -xzf $localFailureArchive -C $machine.artifactRoot
                    if ($LASTEXITCODE -ne 0) { throw "Local Mac failure-artifact archive extraction failed with exit code $LASTEXITCODE." }
                }
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
        if ($null -ne $macPublishArchive) {
            Remove-Item -LiteralPath $macPublishArchive -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $macArtifactArchive) {
            Remove-Item -LiteralPath $macArtifactArchive -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $localFailureArchive) {
            Remove-Item -LiteralPath $localFailureArchive -Force -ErrorAction SilentlyContinue
        }
        if ($null -ne $remoteCleanupRoot) {
            try {
                if ($platformByMachine.ContainsKey($record.name) -and $platformByMachine[$record.name] -eq 'windows') {
                    # A forced coordinator stop can leave the interactive
                    # scheduled task alive and Windows marks its loaded files
                    # as undeletable. Stop only this cycle's process tree and
                    # remove the tree through native PowerShell so read-only
                    # attributes and Windows path semantics are handled.
                    $cycleToken = [IO.Path]::GetFileName($remoteCleanupRoot)
                    $cycleTokenLiteral = "'" + $cycleToken.Replace("'", "''") + "'"
                    $remoteCleanupRootLiteral = "'" + $remoteCleanupRoot.Replace("'", "''") + "'"
                    $remoteCleanupLines = @(
                        '$token = ' + $cycleTokenLiteral,
                        'Get-ScheduledTask -TaskName ''DNPPV_ProductSceneValidation'' -ErrorAction SilentlyContinue | Stop-ScheduledTask -ErrorAction SilentlyContinue',
                        'Unregister-ScheduledTask -TaskName ''DNPPV_ProductSceneValidation'' -Confirm:$false -ErrorAction SilentlyContinue',
                        'Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like (''*'' + $token + ''*'') } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }',
                        'Start-Sleep -Seconds 2',
                        'if (Test-Path -LiteralPath ' + $remoteCleanupRootLiteral + ') { Remove-Item -LiteralPath ' + $remoteCleanupRootLiteral + ' -Force -Recurse -ErrorAction SilentlyContinue }'
                    )
                    # Encode a typed, newline-delimited script so OpenSSH and
                    # PowerShell cannot collapse adjacent statements.
                    $remoteCleanupPayload = [string]::Join([Environment]::NewLine, [string[]]$remoteCleanupLines) + [Environment]::NewLine
                    $remoteCleanupEncoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($remoteCleanupPayload))
                    Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $password -Arguments @(
                        'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                        "$($machineRecord.user)@$($machineRecord.address)", 'powershell.exe', '-NoProfile', '-NonInteractive', '-EncodedCommand', $remoteCleanupEncoded
                    ) -Timeout $macTimeout | Out-Null
                }
                else {
                    Invoke-RemoteNative -User $machineRecord.user -HostName $machineRecord.address -Secret $password -Arguments @(
                        'ssh', '-o', 'StrictHostKeyChecking=accept-new', '-o', 'BatchMode=no', '-o', 'PreferredAuthentications=password', '-o', 'PubkeyAuthentication=no', '-o', 'NumberOfPasswordPrompts=1', '-o', 'ConnectTimeout=60',
                        "$($machineRecord.user)@$($machineRecord.address)",
                        "if [ -d '$remoteCleanupRoot' ]; then rm -rf -- '$remoteCleanupRoot'; fi"
                    ) -Timeout $macTimeout | Out-Null
                }
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
