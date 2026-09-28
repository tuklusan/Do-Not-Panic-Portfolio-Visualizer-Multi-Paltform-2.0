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
param([switch]$SelfTest)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $SelfTest) { throw 'Use -SelfTest.' }
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Receipt self-test requires PowerShell 7 or later.' }
$validator = [IO.Path]::Combine($PSScriptRoot,'Assert-CodeReviewReceipt.ps1')
if (-not [IO.File]::Exists($validator)) { throw 'Receipt v2 validator is missing.' }
$source = [IO.File]::ReadAllText($validator)
foreach ($token in @('dnppv2-code-review-receipt/v2','dnppv2-review-result/v3','Build-VerifiedReceipt','RemoteOldSha')) {
    if (-not $source.Contains($token)) { throw "Receipt v2 contract token is missing: $token" }
}
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixture = [IO.Path]::Combine($tempBase,'dnppv2-receipt-negative-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture) | Out-Null
try {
    & git -C $fixture init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Fixture Git initialization failed.' }
    & git -C $fixture config user.name 'Receipt Self-Test'
    & git -C $fixture config user.email 'receipt-selftest@example.invalid'
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'state.txt'),"base`n",[Text.UTF8Encoding]::new($false))
    & git -C $fixture add -- state.txt
    & git -C $fixture commit --quiet -m base
    if ($LASTEXITCODE -ne 0) { throw 'Fixture base commit failed.' }
    $base = (& git -C $fixture rev-parse HEAD).Trim()
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'state.txt'),"head`n",[Text.UTF8Encoding]::new($false))
    & git -C $fixture add -- state.txt
    & git -C $fixture commit --quiet -m head
    if ($LASTEXITCODE -ne 0) { throw 'Fixture head commit failed.' }
    $head = (& git -C $fixture rev-parse HEAD).Trim()
    $nonFastForwardRejected = $false
    try { $null = & $validator -Operation Validate -RepositoryRoot $fixture -BaseSha $head -NewSha $base -RemoteOldSha $head }
    catch { $nonFastForwardRejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'RECEIPT_VALIDATION_FAILURE' -and $_.Exception.Message -match 'fast-forward' }
    if (-not $nonFastForwardRejected) { throw 'Non-fast-forward receipt candidate was not rejected before receipt lookup.' }
    Write-Output 'CODE_REVIEW_RECEIPT_NONFASTFORWARD_REJECTED=Passed'
    foreach ($case in @(
        @{ Name='missing receipt'; Args=@{ Operation='Validate'; RepositoryRoot=$fixture; BaseSha=$base; NewSha=$head; RemoteOldSha=$base } },
        @{ Name='stale remote base'; Args=@{ Operation='Validate'; RepositoryRoot=$fixture; BaseSha=$base; NewSha=$head; RemoteOldSha=$head } },
        @{ Name='missing review result'; Args=@{ Operation='Create'; RepositoryRoot=$fixture; BaseSha=$base; NewSha=$head; Scope='TEST'; ResultPath=([IO.Path]::Combine($fixture,'missing-result.json')) } }
    )) {
        $rejected = $false
        try { $caseArgs = $case.Args; $null = & $validator @caseArgs }
        catch { $rejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'RECEIPT_VALIDATION_FAILURE' }
        if (-not $rejected) { throw "Receipt v2 accepted $($case.Name)." }
    }
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'untracked.txt'),'dirty',[Text.UTF8Encoding]::new($false))
    $dirtyRejected = $false
    try { $null = & $validator -Operation Create -RepositoryRoot $fixture -BaseSha $base -NewSha $head -Scope TEST -ResultPath ([IO.Path]::Combine($fixture,'missing-result.json')) }
    catch { $dirtyRejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'PACKET_VALIDATION_FAILURE' -and $_.Exception.Message -match 'clean' }
    if (-not $dirtyRejected) { throw 'Receipt creation did not recheck the clean committed candidate.' }
    Write-Output 'CODE_REVIEW_RECEIPT_DIRTY_CREATE_REJECTED=Passed'
    try { . $validator -Operation Validate -RepositoryRoot $fixture -BaseSha $base -NewSha $head -RemoteOldSha $base }
    catch { if ($_.Exception.Data['DNPPVFailureClass'] -cne 'RECEIPT_VALIDATION_FAILURE') { throw } }
    $transactionDirectory = [IO.Path]::Combine($fixture,'transaction')
    $outsideDirectory = [IO.Path]::Combine($fixture,'outside')
    [IO.Directory]::CreateDirectory($transactionDirectory) | Out-Null
    [IO.Directory]::CreateDirectory($outsideDirectory) | Out-Null
    [IO.File]::WriteAllText([IO.Path]::Combine($outsideDirectory,'evidence.json'),'{}',[Text.UTF8Encoding]::new($false))
    $linkPath = [IO.Path]::Combine($transactionDirectory,'linked')
    if ($IsWindows) { $null = New-Item -ItemType Junction -Path $linkPath -Target $outsideDirectory }
    else { $null = [IO.Directory]::CreateSymbolicLink($linkPath,$outsideDirectory) }
    $linkRejected = $false
    try { $null = Resolve-Evidence -TransactionDirectory $transactionDirectory -Relative 'linked/evidence.json' }
    catch { $linkRejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'RECEIPT_VALIDATION_FAILURE' }
    if (-not $linkRejected) { throw 'Receipt evidence resolver accepted a link to an outside file.' }
    Write-Output 'CODE_REVIEW_RECEIPT_LINK_TRAVERSAL_REJECTED=Passed'
    Write-Output 'CODE_REVIEW_RECEIPT_NEGATIVE_SELFTEST=Passed'
}
finally {
    $resolved = [IO.Path]::GetFullPath($fixture)
    if (-not $resolved.StartsWith($tempBase,[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolved) -notmatch '^dnppv2-receipt-negative-[0-9a-f]{32}$') {
        throw 'Refusing receipt fixture cleanup outside the exact temp target.'
    }
    if ([IO.Directory]::Exists($resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
