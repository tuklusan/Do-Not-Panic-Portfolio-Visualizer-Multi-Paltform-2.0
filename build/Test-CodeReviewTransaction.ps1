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
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Transaction self-test requires PowerShell 7 or later.' }
. ([IO.Path]::Combine($PSScriptRoot,'CodeReviewerCommon.ps1'))
$repoRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..'))
$fixtureParent = [IO.Path]::Combine($repoRoot,'build','code-review','test-fixtures')
$fixture = [IO.Path]::Combine($fixtureParent,[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
try {
    $deep = [ordered]@{ sentinel = 'retained' }
    for ($i = 20; $i -ge 1; $i--) { $deep = [ordered]@{ ("level$i") = $deep } }
    $deepJson = [Text.UTF8Encoding]::new($false,$true).GetString((ConvertTo-CodeReviewJsonBytes -Value $deep)) | ConvertFrom-Json -AsHashtable
    for ($i = 1; $i -le 20; $i++) { $deepJson = $deepJson["level$i"] }
    if ($deepJson.sentinel -cne 'retained') { throw 'Identity JSON serializer truncated nested evidence.' }
    $packetBytes = [Text.UTF8Encoding]::new($false).GetBytes('{"schema":"transaction-self-test"}')
    $packetSha = Get-CodeReviewBytesSha256 -Bytes $packetBytes
    $deadline = [DateTimeOffset]::UtcNow.AddMinutes(2)
    $prepared = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'build','code-review')) -ReviewType CODE -Scope 'SELFTEST' -BaseSha ('a' * 40) -HeadSha ('b' * 40) -SnapshotSha256 ('c' * 64) -PacketSha256 $packetSha -PacketBytes $packetBytes -DeadlineUtc $deadline
    $transactionPath = [IO.Path]::Combine($prepared.Directory,'transaction.json')
    $state = Read-CodeReviewTransaction -TransactionPath $transactionPath
    if ($state.state -cne 'PREPARED' -or $state.packet_sha256 -cne $packetSha -or $state.transaction_id -cne $prepared.Transaction.transaction_id -or
        $state.deadline_utc -isnot [string] -or $state.deadline_utc -cne $deadline.ToString('o')) {
        throw 'Prepared transaction identity is invalid.'
    }
    if (Test-CodeReviewProtocolReplayAuthorization -TransactionPath $transactionPath -Transaction $state -Model 'nvidia/nemotron-3-super-120b-a12b') {
        throw 'Protocol replay was authorized without an audited response.'
    }
    $lease = Enter-CodeReviewLease -RepositoryRoot $fixture -Operation Health -HealthModelRole Primary -DeadlineUtc $deadline
    try {
        $contended = $false
        try {
            $second = Enter-CodeReviewLease -RepositoryRoot $fixture -Operation Health -HealthModelRole Fallback -DeadlineUtc $deadline
            Exit-CodeReviewLease -Lease $second
        }
        catch { $contended = $_.Exception.Data['DNPPVFailureClass'] -ceq 'LOCAL_CONCURRENCY_BUSY' }
        if (-not $contended) { throw 'Concurrent review lease was accepted.' }
    }
    finally { Exit-CodeReviewLease -Lease $lease }
    if ([IO.File]::Exists([IO.Path]::Combine($fixture,'build','code-review','active-review.json'))) { throw 'Health lease owner metadata was not released.' }
    $expiredLease = Enter-CodeReviewLease -RepositoryRoot $fixture -Operation Health -HealthModelRole Primary -DeadlineUtc ([DateTimeOffset]::UtcNow.AddSeconds(-1))
    try {
        $staleOwnerRejected = $false
        $observedClass = $null
        try {
            $contender = Enter-CodeReviewLease -RepositoryRoot $fixture -Operation Health -HealthModelRole Fallback -DeadlineUtc $deadline
            Exit-CodeReviewLease -Lease $contender
        }
        catch { $observedClass = $_.Exception.Data['DNPPVFailureClass']; $staleOwnerRejected = $observedClass -ceq 'LOCAL_CONCURRENCY_STALE_OWNER' }
        if (-not $staleOwnerRejected -or -not $expiredLease.Stream.CanWrite) {
            throw "Live lease owner past deadline was stolen, killed, or misclassified; class=$observedClass."
        }
        Write-Output 'CODE_REVIEW_LIVE_STALE_OWNER_REJECTED=Passed'
    }
    finally { Exit-CodeReviewLease -Lease $expiredLease }
    $childScript = [IO.Path]::Combine($fixture,'hold-lease.ps1')
    $readyPath = [IO.Path]::Combine($fixture,'lease-ready.txt')
    $childText = @'
param([string]$CommonPath,[string]$FixturePath,[string]$ReadyPath)
. $CommonPath
$lease = Enter-CodeReviewLease -RepositoryRoot $FixturePath -Operation Health -HealthModelRole Primary -DeadlineUtc ([DateTimeOffset]::UtcNow.AddMinutes(2))
[IO.File]::WriteAllText($ReadyPath,'ready')
try { while ($true) { Start-Sleep -Milliseconds 250 } }
finally { Exit-CodeReviewLease -Lease $lease }
'@
    [IO.File]::WriteAllText($childScript,$childText,[Text.UTF8Encoding]::new($false))
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'pwsh'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile','-File',$childScript,([IO.Path]::Combine($PSScriptRoot,'CodeReviewerCommon.ps1')),$fixture,$readyPath)) {
        [void]$start.ArgumentList.Add($argument)
    }
    $child = [Diagnostics.Process]::new()
    $child.StartInfo = $start
    try {
        if (-not $child.Start()) { throw 'Dead-owner fixture process did not start.' }
        $readyDeadline = [DateTimeOffset]::UtcNow.AddSeconds(15)
        while (-not [IO.File]::Exists($readyPath) -and -not $child.HasExited -and [DateTimeOffset]::UtcNow -lt $readyDeadline) {
            [Threading.Thread]::Sleep(100)
        }
        if (-not [IO.File]::Exists($readyPath)) {
            if (-not $child.HasExited) { $child.Kill($true); $null = $child.WaitForExit(5000) }
            throw ('Dead-owner fixture did not acquire lease: ' + $child.StandardError.ReadToEnd())
        }
        $child.Kill($true)
        if (-not $child.WaitForExit(5000)) { throw 'Dead-owner fixture process did not terminate.' }
        $replacement = Enter-CodeReviewLease -RepositoryRoot $fixture -Operation Health -HealthModelRole Fallback -DeadlineUtc $deadline
        try {
            if (-not $replacement.Stream.CanWrite) { throw 'OS lease did not release after owner death.' }
            Write-Output 'CODE_REVIEW_DEAD_OWNER_LEASE_RELEASED=Passed'
        }
        finally { Exit-CodeReviewLease -Lease $replacement }
    }
    finally {
        if (-not $child.HasExited) { $child.Kill($true); $null = $child.WaitForExit(5000) }
        $child.Dispose()
    }
    $state.state = 'COMPLETED_PASS'
    $state.result_sha256 = 'd' * 64
    $null = Write-CodeReviewTransaction -TransactionPath $transactionPath -State $state
    $immutable = $false
    try { $null = Write-CodeReviewTransaction -TransactionPath $transactionPath -State $state }
    catch { $immutable = $_.Exception.Data['DNPPVFailureClass'] -ceq 'LOCAL_PERSISTENCE_FAILURE' }
    if (-not $immutable) { throw 'Completed transaction was mutable.' }
    Write-Output 'CODE_REVIEW_TRANSACTION_SELFTEST=Passed'
}
finally {
    $target = [IO.Path]::GetFullPath($fixture)
    $parent = [IO.Path]::GetFullPath($fixtureParent)
    if (-not $target.StartsWith($parent + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($target) -notmatch '^[0-9a-f]{32}$') {
        throw 'Refusing transaction fixture cleanup outside the exact test root.'
    }
    if ([IO.Directory]::Exists($target)) { Remove-Item -LiteralPath $target -Recurse -Force }
}
