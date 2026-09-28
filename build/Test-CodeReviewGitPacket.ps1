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
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Git packet test requires PowerShell 7 or later.' }
. ([IO.Path]::Combine($PSScriptRoot,'CodeReviewerCommon.ps1'))
$projectRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..'))
$testParent = [IO.Path]::Combine($projectRoot,'build','code-review','test-fixtures')
$fixture = [IO.Path]::Combine($testParent,[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
function Invoke-FixtureGit([string[]]$Arguments) {
    $output = & git -C $fixture @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Fixture Git failed: $($Arguments -join ' '): $output" }
    return (($output | Out-String).Trim())
}
try {
    Invoke-FixtureGit @('init','--quiet') | Out-Null
    Invoke-FixtureGit @('config','core.autocrlf','false') | Out-Null
    Invoke-FixtureGit @('config','core.filemode','false') | Out-Null
    Invoke-FixtureGit @('config','user.name','Git Packet Test') | Out-Null
    Invoke-FixtureGit @('config','user.email','git-packet-test@example.invalid') | Out-Null
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'.gitignore'),"build/`n",$utf8)
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'.gitattributes'),"*.txt diff=wordy`nbinary.bin binary`n",$utf8)
    foreach ($name in @('old name.txt','delete.txt','mode.sh',"space's (path).txt")) {
        [IO.File]::WriteAllText([IO.Path]::Combine($fixture,$name),"baseline $name`n",$utf8)
    }
    Invoke-FixtureGit @('add','--','.gitignore','.gitattributes','old name.txt','delete.txt','mode.sh',"space's (path).txt") | Out-Null
    Invoke-FixtureGit @('commit','--quiet','-m','baseline') | Out-Null
    $base = Invoke-FixtureGit @('rev-parse','HEAD')
    $oldPath = [IO.Path]::Combine($fixture,'old name.txt')
    $newPath = [IO.Path]::Combine($fixture,'new name.txt')
    if (-not $oldPath.StartsWith($fixture + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        -not $newPath.StartsWith($fixture + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture rename leaves test root.' }
    Move-Item -LiteralPath $oldPath -Destination $newPath
    [IO.File]::Delete([IO.Path]::Combine($fixture,'delete.txt'))
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,"space's (path).txt"),"changed with special path`n",$utf8)
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'Unicode Ω.txt'),"new Unicode path`n",$utf8)
    [IO.File]::WriteAllBytes([IO.Path]::Combine($fixture,'binary.bin'),[byte[]]@(0,1,2,255,0))
    Invoke-FixtureGit @('add','-A','--') | Out-Null
    Invoke-FixtureGit @('update-index','--chmod=+x','--','mode.sh') | Out-Null
    Invoke-FixtureGit @('commit','--quiet','-m','mixed changes') | Out-Null
    $head = Invoke-FixtureGit @('rev-parse','HEAD')
    $clean = Invoke-FixtureGit @('status','--porcelain','--untracked-files=all')
    if ($clean) { throw 'Fixture repository is not clean.' }
    $descriptor = New-CodeReviewCandidateDescriptorV2 -RepositoryRoot $fixture -BaseSha $base -HeadSha $head -Scope 'GIT-PACKET' -Requirement 'Review the directly changed paths.'
    $entries = @($descriptor.Entries)
    if ($entries.Count -ne 7) { throw "Expected 7 no-renames entries, found $($entries.Count)." }
    $paths = @($entries.Path)
    foreach ($name in @('old name.txt','new name.txt','delete.txt','mode.sh',"space's (path).txt",'Unicode Ω.txt','binary.bin')) {
        if ($paths -cnotcontains $name) { throw "Changed path omitted: $name" }
    }
    $modeEntry = @($entries | Where-Object { $_.Path -ceq 'mode.sh' })[0]
    if ($modeEntry.BaseBlob -cne $modeEntry.HeadBlob -or $modeEntry.BaseMode -ceq $modeEntry.HeadMode) {
        throw 'Chmod-only record lost identical blob or changed mode identity.'
    }
    $packet = New-CodeReviewPacketV2 -RepositoryRoot $fixture -BaseSha $base -HeadSha $head -Scope 'GIT-PACKET' -Requirement 'Review the directly changed paths.' -ContextPath @('.gitignore')
    if (@($packet.Packet.changed_files).Count -ne 7 -or @($packet.Packet.context_files).Count -ne 1 -or
        $packet.Packet.context_files[0].path -cne '.gitignore') { throw 'Packet changed/context set is incomplete.' }
    foreach ($setting in @(
        @('diff.algorithm','histogram'),@('diff.renames','true'),@('diff.indentHeuristic','false'),
        @('diff.interHunkContext','20'),@('core.quotepath','false'),@('color.ui','always'),
        @('diff.wordy.command','false'),@('diff.wordy.textconv','false'),@('diff.wordy.xfuncname','^changed')
    )) { Invoke-FixtureGit (@('config') + $setting) | Out-Null }
    $configuredPacket = New-CodeReviewPacketV2 -RepositoryRoot $fixture -BaseSha $base -HeadSha $head -Scope 'GIT-PACKET' -Requirement 'Review the directly changed paths.' -ContextPath @('.gitignore')
    if ($configuredPacket.PacketSha256 -cne $packet.PacketSha256 -or
        $configuredPacket.Candidate.SnapshotSha256 -cne $packet.Candidate.SnapshotSha256) {
        throw 'Git diff configuration or attributes changed packet identity.'
    }
    Write-Output 'CODE_REVIEW_GIT_DIFF_CONFIGURATION_INDEPENDENCE=Passed'
    $binaryEntry = @($packet.Packet.changed_files | Where-Object { $_.path -ceq 'binary.bin' })[0]
    if ($binaryEntry.head_kind -cne 'binary' -or $binaryEntry.review_context_kind -cne 'metadata-only') { throw 'Binary changed content was not identified.' }
    $nested = [IO.Path]::Combine($fixture,'nested')
    [IO.Directory]::CreateDirectory($nested) | Out-Null
    Push-Location $nested
    try { $nestedPacket = New-CodeReviewPacketV2 -RepositoryRoot $fixture -BaseSha $base -HeadSha $head -Scope 'GIT-PACKET' -Requirement 'Review the directly changed paths.' -ContextPath @('.gitignore') }
    finally { Pop-Location }
    if ($nestedPacket.PacketSha256 -cne $packet.PacketSha256 -or $nestedPacket.Candidate.SnapshotSha256 -cne $packet.Candidate.SnapshotSha256) {
        throw 'Packet identity changed with CWD.'
    }
    $outsideCwd = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    Push-Location $outsideCwd
    try { $outsidePacket = New-CodeReviewPacketV2 -RepositoryRoot $fixture -BaseSha $base -HeadSha $head -Scope 'GIT-PACKET' -Requirement 'Review the directly changed paths.' -ContextPath @('.gitignore') }
    finally { Pop-Location }
    if ($outsidePacket.PacketSha256 -cne $packet.PacketSha256 -or $outsidePacket.Candidate.SnapshotSha256 -cne $packet.Candidate.SnapshotSha256) {
        throw 'Packet identity changed outside the repository or on a different drive.'
    }
    $driveRelativeRejected = $false
    try { $null = Resolve-CodeReviewPath -RepositoryRoot $fixture -Path 'D:folder\file.txt' -InsideRepository }
    catch { $driveRelativeRejected = $_.Exception.Message -match 'drive-relative' }
    if (-not $driveRelativeRejected) { throw 'Drive-relative path was accepted.' }
    Write-Output 'CODE_REVIEW_GIT_PACKET_MIXED_FIXTURE=Passed'
}
finally {
    $target = [IO.Path]::GetFullPath($fixture)
    $parent = [IO.Path]::GetFullPath($testParent)
    if (-not $target.StartsWith($parent + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($target) -notmatch '^[0-9a-f]{32}$') { throw 'Refusing Git packet fixture cleanup outside the exact test root.' }
    if ([IO.Directory]::Exists($target)) { Remove-Item -LiteralPath $target -Recurse -Force }
}
