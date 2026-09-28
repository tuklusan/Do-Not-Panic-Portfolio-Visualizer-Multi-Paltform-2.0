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
$root = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..'))
$fixtureRoot = [IO.Path]::Combine($root,'build','code-review','test-fixtures',[guid]::NewGuid().ToString('N'))
$hookSource = [IO.Path]::Combine($root,'.githooks')
$hookDirectory = [IO.Path]::Combine($fixtureRoot,'.githooks')
$buildDirectory = [IO.Path]::Combine($fixtureRoot,'build')
[IO.Directory]::CreateDirectory($hookDirectory) | Out-Null
[IO.Directory]::CreateDirectory($buildDirectory) | Out-Null
[IO.File]::Copy([IO.Path]::Combine($hookSource,'pre-push'),[IO.Path]::Combine($hookDirectory,'pre-push'))
[IO.File]::Copy([IO.Path]::Combine($hookSource,'pre-push.ps1'),[IO.Path]::Combine($hookDirectory,'pre-push.ps1'))
& git -C $fixtureRoot init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Hook fixture git init failed.' }
& git -C $fixtureRoot config core.hooksPath .githooks
$utf8 = [Text.UTF8Encoding]::new($false)
$eventsPath = [IO.Path]::Combine($fixtureRoot,'events.txt')
$stubs = @{
    'Assert-NoUpstreamMutation.ps1' = 'param([string]$RemoteName,[string]$RemoteUrl) [IO.File]::AppendAllText((Join-Path (Split-Path $PSScriptRoot) "events.txt"),"UPSTREAM:$RemoteName|$RemoteUrl`n")'
    'Test-LicenseHeaders.ps1' = '[IO.File]::AppendAllText((Join-Path (Split-Path $PSScriptRoot) "events.txt"),"LICENSE`n")'
    'Test-PowerShellSyntax.ps1' = '[IO.File]::AppendAllText((Join-Path (Split-Path $PSScriptRoot) "events.txt"),"SYNTAX`n")'
    'Test-WorkflowGateConfiguration.ps1' = '[IO.File]::AppendAllText((Join-Path (Split-Path $PSScriptRoot) "events.txt"),"WORKFLOW`n")'
    'Test-HarnessFreeze.ps1' = 'param([string]$BaseRef) [IO.File]::AppendAllText((Join-Path (Split-Path $PSScriptRoot) "events.txt"),"FREEZE:$BaseRef`n")'
    'Assert-CodeReviewReceipt.ps1' = 'param([string]$Operation,[string]$RepositoryRoot,[string]$BaseSha,[string]$NewSha,[string]$RemoteOldSha,[string]$RemoteRef) [IO.File]::AppendAllText((Join-Path (Split-Path $PSScriptRoot) "events.txt"),"RECEIPT:$Operation|$BaseSha|$NewSha|$RemoteOldSha|$RemoteRef`n"); if ($NewSha -eq ("f" * 40)) { throw "Synthetic receipt rejection." }'
}
foreach ($entry in $stubs.GetEnumerator()) { [IO.File]::WriteAllText([IO.Path]::Combine($buildDirectory,$entry.Key),$entry.Value,$utf8) }
$sh = 'C:\Program Files\Git\bin\sh.exe'
if (-not [IO.File]::Exists($sh)) { throw 'Git sh.exe unavailable for hook protocol test.' }

function Invoke-Hook([string]$InputText) {
    if ([IO.File]::Exists($eventsPath)) { [IO.File]::Delete($eventsPath) }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $sh
    $start.WorkingDirectory = $fixtureRoot
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('.githooks/pre-push','origin','https://example.invalid/repo')) { [void]$start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw 'Hook shell did not start.' }
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write($InputText)
        $process.StandardInput.Close()
        $process.WaitForExit()
        $events = if ([IO.File]::Exists($eventsPath)) { [IO.File]::ReadAllText($eventsPath) } else { '' }
        return [pscustomobject]@{ Exit=$process.ExitCode; Stdout=$stdoutTask.GetAwaiter().GetResult(); Stderr=$stderrTask.GetAwaiter().GetResult(); Events=$events }
    }
    finally { $process.Dispose() }
}

$a='a'*40; $b='b'*40; $c='c'*40; $f='f'*40; $zero='0'*40
$main = "refs/heads/main $b refs/heads/main $a`n"
$feature = "refs/heads/feature $c refs/heads/feature $a`n"
try {
    $one = Invoke-Hook -InputText $main
    if ($one.Exit -ne 0 -or $one.Events -notmatch 'UPSTREAM:origin\|https://example.invalid/repo' -or
        $one.Events -notmatch "RECEIPT:Validate\|$a\|$b\|$a\|refs/heads/main" -or
        $one.Events.IndexOf('UPSTREAM:') -gt $one.Events.IndexOf('RECEIPT:')) { throw 'Single main update did not preserve stdin/argv and gate order.' }
    Write-Output 'HOOK_SHELL_STDIN_ARGV_ONE_MAIN=Passed'
    $multiple = Invoke-Hook -InputText ($main + $feature + "refs/heads/main $c refs/heads/main $b`n")
    if ($multiple.Exit -ne 0 -or @([regex]::Matches($multiple.Events,'RECEIPT:')).Count -ne 2) { throw 'Multiple update tuples were not handled exactly.' }
    Write-Output 'HOOK_MULTIPLE_MAIN_AND_NONMAIN=Passed'
    $featureOnly = Invoke-Hook -InputText $feature
    if ($featureOnly.Exit -ne 0 -or $featureOnly.Events.Contains('RECEIPT:')) { throw 'Non-main push invoked receipt validation.' }
    Write-Output 'HOOK_NONMAIN_NO_RECEIPT=Passed'
    $malformed = Invoke-Hook -InputText "refs/heads/main $b refs/heads/main`n"
    if ($malformed.Exit -eq 0 -or $malformed.Events) { throw 'Malformed tuple reached slower gates.' }
    Write-Output 'HOOK_MALFORMED_PRE_GATES_REJECTED=Passed'
    $deletion = Invoke-Hook -InputText "refs/heads/main $zero refs/heads/main $a`n"
    if ($deletion.Exit -eq 0 -or $deletion.Events) { throw 'Protected deletion reached slower gates.' }
    Write-Output 'HOOK_MAIN_DELETION_PRE_GATES_REJECTED=Passed'
    $badReceipt = Invoke-Hook -InputText "refs/heads/main $f refs/heads/main $a`n"
    if ($badReceipt.Exit -eq 0 -or $badReceipt.Events -notmatch 'RECEIPT:Validate') { throw 'Invalid receipt did not fail hook.' }
    Write-Output 'HOOK_RECEIPT_FAILURE_PROPAGATES=Passed'
}
finally {
    $fixturesRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($root,'build','code-review','test-fixtures'))
    $target = [IO.Path]::GetFullPath($fixtureRoot)
    if (-not $target.StartsWith($fixturesRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing out-of-root hook fixture cleanup.' }
    Remove-Item -LiteralPath $target -Recurse -Force
}
