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
param(
    [Parameter(Mandatory)][ValidateSet('Review','Health')][string]$Operation,
    [ValidateSet('CODE','DOCUMENTATION','TEST_ARTIFACT')][string]$ReviewType,
    [string]$RepositoryRoot,
    [string]$BaseSha,
    [string]$HeadSha,
    [string]$Scope,
    [string]$Requirement,
    [string]$RequirementPath,
    [string[]]$ContextPath = @(),
    [string]$ContextSpecPath,
    [string]$ReviewMaterialPath,
    [string]$OutputDirectory,
    [ValidateSet('Primary','Fallback')][string]$HealthModelRole,
    [ValidateRange(60,14400)][int]$ReviewTimeoutSeconds = 1800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'DNPPV review infrastructure requires PowerShell 7 or later.' }
$implementationRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..'))
. ([IO.Path]::Combine($implementationRoot,'build','CodeReviewerCommon.ps1'))
$repo = Resolve-CodeReviewRepositoryRoot -RepositoryRoot $(if ($RepositoryRoot) { $RepositoryRoot } else { $implementationRoot })
$policyPath = [IO.Path]::Combine($implementationRoot,'build','Invoke-ReviewGate.ps1')
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($ReviewTimeoutSeconds)

if ($Operation -eq 'Health') {
    if (-not $HealthModelRole) { throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'Health model role is required.') }
    $lease = Enter-CodeReviewLease -RepositoryRoot $repo -Operation Health -HealthModelRole $HealthModelRole -DeadlineUtc $deadline
    try {
        $args = @{ Operation='Health'; HealthModelRole=$HealthModelRole; DeadlineUtc=$deadline; RepositoryRoot=$implementationRoot }
        return & $policyPath @args
    }
    finally { Exit-CodeReviewLease -Lease $lease }
}

if (-not $ReviewType -or -not $Scope -or -not $HeadSha) {
    throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'Review type, scope, and HEAD are required.')
}
if ([bool]$Requirement -eq [bool]$RequirementPath) {
    throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'Exactly one requirement text or path is required.')
}
if ($RequirementPath) {
    $requirementFile = Resolve-CodeReviewPath -RepositoryRoot $repo -Path $RequirementPath -InsideRepository
    if (-not [IO.File]::Exists($requirementFile)) { throw (New-CodeReviewFailure -FailureClass 'LOCAL_PATH_FAILURE' -Message 'Requirement file is missing.') }
    $Requirement = [IO.File]::ReadAllText($requirementFile,[Text.UTF8Encoding]::new($false,$true))
}
if ([string]::IsNullOrWhiteSpace($Requirement)) { throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'Requirement is empty.') }

if ($ReviewType -eq 'CODE') {
    if (-not $BaseSha -or $ReviewMaterialPath -or $OutputDirectory) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'CODE requires base SHA and no material/output override.')
    }
    $null = Assert-CodeReviewCommittedCandidate -RepositoryRoot $repo -BaseSha $BaseSha -HeadSha $HeadSha
    $packet = New-CodeReviewPacketV2 -RepositoryRoot $repo -BaseSha $BaseSha -HeadSha $HeadSha -Scope $Scope -Requirement $Requirement -ContextPath $ContextPath -ContextSpecPath $ContextSpecPath
    $outputRoot = [IO.Path]::Combine($repo,'build','code-review')
    $snapshot = $packet.Candidate.SnapshotSha256
}
else {
    if ($BaseSha -or $ContextPath.Count -gt 0 -or $ContextSpecPath -or -not $ReviewMaterialPath -or -not $OutputDirectory) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'Material-review inputs are incomplete or contain CODE-only fields.')
    }
    $current = [Text.UTF8Encoding]::new($false,$true).GetString((Invoke-CodeReviewRawGit -RepositoryRoot $repo -Arguments @('rev-parse','--verify','HEAD^{commit}'))).Trim()
    if ($current -cne $HeadSha.ToLowerInvariant()) { throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'Material candidate HEAD changed.') }
    $outputRoot = Resolve-CodeReviewPath -RepositoryRoot $repo -Path $OutputDirectory -InsideRepository
    $materialFile = Resolve-CodeReviewPath -RepositoryRoot $repo -Path $ReviewMaterialPath -InsideRepository
    $outputRelative = [IO.Path]::GetRelativePath($repo,$outputRoot).Replace('\','/')
    if (-not ($outputRelative -eq 'build/code-review' -or $outputRelative.StartsWith('build/code-review/',[StringComparison]::Ordinal) -or
        $outputRelative -match '^artifacts/.+/review(?:/.*)?$')) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_PATH_FAILURE' -Message 'Material output must be in a project-owned review tree.')
    }
    & git -C $repo check-ignore -q -- $outputRelative
    if ($LASTEXITCODE -ne 0) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_PATH_FAILURE' -Message 'Material output tree is not ignored by Git.')
    }
    $transactionRoot = [IO.Path]::Combine($outputRoot,'transactions')
    if ($materialFile.Equals($transactionRoot,[StringComparison]::OrdinalIgnoreCase) -or
        $materialFile.StartsWith($transactionRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_PATH_FAILURE' -Message 'Review material cannot be inside transaction output.')
    }
    $packet = New-CodeReviewMaterialPacketV1 -RepositoryRoot $repo -ReviewType $ReviewType -HeadSha $HeadSha -Scope $Scope -Requirement $Requirement -ReviewMaterialPath $ReviewMaterialPath
    $snapshot = $packet.SnapshotSha256
}

[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$candidateDirectory = [IO.Path]::Combine($outputRoot,'transactions',$HeadSha.ToLowerInvariant(),$packet.PacketSha256)
$existing = @()
if ([IO.Directory]::Exists($candidateDirectory)) {
    foreach ($path in [IO.Directory]::EnumerateFiles($candidateDirectory,'transaction.json',[IO.SearchOption]::AllDirectories)) {
        $state = Read-CodeReviewTransaction -TransactionPath $path
        if ($state.packet_sha256 -cne $packet.PacketSha256 -or $state.head_sha -cne $HeadSha.ToLowerInvariant()) {
            throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Transaction candidate identity conflicts with directory.')
        }
        if ($state.state -notlike 'COMPLETED_*') { $existing += $path }
    }
}
if ($existing.Count -gt 1) {
    $conflictingIds = @($existing | ForEach-Object { [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($_)) })
    $failure = New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Multiple nonterminal same-packet transactions exist.'
    if (@($conflictingIds | Where-Object { $_ -cnotmatch '^[0-9a-f]{32}$' }).Count -eq 0) {
        $failure.Data['DNPPVConflictingTransactionIds'] = [string[]]$conflictingIds
    }
    throw $failure
}
if ($existing.Count -eq 1) {
    $transactionPath = $existing[0]
    $state = Read-CodeReviewTransaction -TransactionPath $transactionPath
    if ($state.state -like 'FAILED_*' -or $state.state -eq 'AMBIGUOUS_DISPATCH') {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Existing same-packet transaction requires operator reconciliation.')
    }
    $persistedPacket = [IO.File]::ReadAllBytes([IO.Path]::Combine([IO.Path]::GetDirectoryName($transactionPath),'packet.json'))
    if ((Get-CodeReviewBytesSha256 -Bytes $persistedPacket) -cne $packet.PacketSha256) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Existing packet evidence differs.')
    }
}
else {
    $prepared = New-CodeReviewPreparedTransaction -OutputRoot $outputRoot -ReviewType $ReviewType -Scope $Scope -BaseSha $BaseSha -HeadSha $HeadSha -SnapshotSha256 $snapshot -PacketSha256 $packet.PacketSha256 -PacketBytes $packet.Bytes -DeadlineUtc $deadline
    $transactionPath = [IO.Path]::Combine($prepared.Directory,'transaction.json')
}
$state = Read-CodeReviewTransaction -TransactionPath $transactionPath
$storedDeadline = [DateTimeOffset]::MinValue
if (-not [DateTimeOffset]::TryParse([string]$state.deadline_utc,[ref]$storedDeadline)) {
    throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Transaction deadline is invalid.')
}
$deadline = $storedDeadline
$lease = Enter-CodeReviewLease -RepositoryRoot $repo -Operation Review -TransactionId $state.transaction_id -ReviewType $ReviewType -BaseSha $state.base_sha -HeadSha $state.head_sha -PacketSha256 $state.packet_sha256 -DeadlineUtc $deadline
try {
    $resultPath = [IO.Path]::Combine([IO.Path]::GetDirectoryName($transactionPath),'result.json')
    if ([IO.File]::Exists($resultPath)) {
        $resultBytes = [IO.File]::ReadAllBytes($resultPath)
        $resultSha = Get-CodeReviewBytesSha256 -Bytes $resultBytes
        try { $savedResult = [Text.UTF8Encoding]::new($false,$true).GetString($resultBytes) | ConvertFrom-Json -AsHashtable -ErrorAction Stop }
        catch { throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Retained result JSON is invalid.') }
        if ($savedResult.schema -cne 'dnppv2-review-result/v3' -or $savedResult.transaction_id -cne $state.transaction_id -or
            $savedResult.packet_sha256 -cne $state.packet_sha256 -or $savedResult.snapshot_sha256 -cne $state.snapshot_sha256 -or
            $savedResult.head_sha -cne $state.head_sha -or $savedResult.base_sha -cne $state.base_sha -or
            $savedResult.reviewer_id -cne 'dnppv2-nvidia-review-gate-v2' -or
            $savedResult.review_type -cne $state.review_type -or $savedResult.scope -cne $state.scope -or
            $savedResult.verdict -notin @('PASS','FAIL','INCONCLUSIVE') -or
            $savedResult.review_complete -isnot [bool] -or $savedResult.fallback_used -isnot [bool] -or
            @($savedResult.models_attempted).Count -ne $state.provider_post_attempt_count -or
            $savedResult.provider_post_attempt_count -ne $state.provider_post_attempt_count -or
            @($savedResult.semantic_content_sha256s).Count -ne $savedResult.review_stage_count -or
            ($state.result_sha256 -and $state.result_sha256 -cne $resultSha)) {
            throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Retained result identity conflicts with transaction.')
        }
        if ($savedResult.verdict -eq 'PASS' -and (-not $savedResult.review_complete -or
            @($savedResult.blocking_findings).Count -ne 0 -or @($savedResult.uncertainties).Count -ne 0)) {
            throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Retained PASS is semantically inconsistent.')
        }
        for ($i = 0; $i -lt $state.provider_post_attempt_count; $i++) {
            if ($savedResult.models_attempted[$i] -cne $state.models_attempted[$i]) {
                throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Retained model history conflicts with transaction.')
            }
        }
        foreach ($semanticSha in @($savedResult.semantic_content_sha256s)) {
            if (@($state.response_evidence | Where-Object { $_.http_status -eq 200 -and $_.final_content_sha256 -ceq $semanticSha }).Count -eq 0) {
                throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Retained semantic hash lacks response evidence.')
            }
        }
        $state.result_sha256 = $resultSha
        $state.state = 'COMPLETED_' + $savedResult.verdict
        $null = Write-CodeReviewTransaction -TransactionPath $transactionPath -State $state
        return [pscustomobject]$savedResult
    }
    if ([DateTimeOffset]::UtcNow -ge $deadline) {
        throw (New-CodeReviewFailure -FailureClass 'DEADLINE_EXCEEDED' -Message 'Stored whole-review deadline has elapsed.')
    }
    $args = @{ Operation='Review'; ReviewType=$ReviewType; PacketPath=([IO.Path]::Combine([IO.Path]::GetDirectoryName($transactionPath),'packet.json')); TransactionPath=$transactionPath; DeadlineUtc=$deadline; RepositoryRoot=$implementationRoot; CandidateRepositoryRoot=$repo }
    $policyResult = & $policyPath @args
    if ($policyResult.verdict -notin @('PASS','FAIL','INCONCLUSIVE') -or $policyResult.reviewer_id -cne 'dnppv2-nvidia-review-gate-v2' -or
        $policyResult.review_complete -isnot [bool] -or $policyResult.fallback_used -isnot [bool]) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_SCHEMA_FAILURE' -Message 'Policy returned an invalid semantic result.')
    }
    if ($policyResult.verdict -eq 'PASS' -and (-not $policyResult.review_complete -or
        @($policyResult.blocking_findings).Count -ne 0 -or @($policyResult.uncertainties).Count -ne 0)) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_SCHEMA_FAILURE' -Message 'Policy PASS is inconsistent with findings or uncertainty.')
    }
    $state = Read-CodeReviewTransaction -TransactionPath $transactionPath
    if (@($state.models_attempted).Count -ne $state.provider_post_attempt_count -or
        @($policyResult.semantic_content_sha256s).Count -ne $policyResult.review_stage_count) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_SCHEMA_FAILURE' -Message 'Policy semantic counts disagree with transaction evidence.')
    }
    foreach ($semanticSha in @($policyResult.semantic_content_sha256s)) {
        if (@($state.response_evidence | Where-Object { $_.http_status -eq 200 -and $_.final_content_sha256 -ceq $semanticSha }).Count -eq 0) {
            throw (New-CodeReviewFailure -FailureClass 'LOCAL_SCHEMA_FAILURE' -Message 'Policy semantic content is not bound to response evidence.')
        }
    }
    $result = [ordered]@{
        schema='dnppv2-review-result/v3'; reviewer_id=$policyResult.reviewer_id; review_type=$ReviewType; scope=$Scope
        transaction_id=$state.transaction_id; base_sha=$state.base_sha; head_sha=$state.head_sha
        snapshot_sha256=$state.snapshot_sha256; packet_sha256=$state.packet_sha256; models_attempted=@($state.models_attempted)
        fallback_used=[bool]$policyResult.fallback_used; review_complete=[bool]$policyResult.review_complete; verdict=$policyResult.verdict
        blocking_findings=@($policyResult.blocking_findings); uncertainties=@($policyResult.uncertainties)
        review_stage_count=[int]$policyResult.review_stage_count; provider_post_attempt_count=[int]$state.provider_post_attempt_count
        semantic_content_sha256s=@($policyResult.semantic_content_sha256s); failure_class=$null; completed_utc=[DateTimeOffset]::UtcNow.ToString('o')
    }
    $resultSha = Write-CodeReviewAtomicJson -Path $resultPath -Value $result
    $state.result_sha256 = $resultSha
    $state.state = 'COMPLETED_' + $policyResult.verdict
    $state.failure_class = $null
    $state.failure_detail = $null
    $null = Write-CodeReviewTransaction -TransactionPath $transactionPath -State $state
    return [pscustomobject]$result
}
catch {
    $state = Read-CodeReviewTransaction -TransactionPath $transactionPath
    if ($state.state -notlike 'COMPLETED_*' -and $state.state -notlike 'FAILED_*' -and $state.state -ne 'AMBIGUOUS_DISPATCH') {
        $failureClass = if ($_.Exception.Data['DNPPVFailureClass']) { [string]$_.Exception.Data['DNPPVFailureClass'] } else { 'LOCAL_UNEXPECTED_FAILURE' }
        $state.state = switch -Wildcard ($failureClass) {
            'DEADLINE_EXCEEDED' { 'DEADLINE_EXCEEDED' }
            'AMBIGUOUS_DISPATCH' { 'AMBIGUOUS_DISPATCH' }
            'PACKET_*' { 'FAILED_PACKET' }
            'MODEL_PROTOCOL_FAILURE' { 'FAILED_PROTOCOL' }
            'PROVIDER_*' { 'FAILED_PROVIDER' }
            default { 'FAILED_LOCAL' }
        }
        $state.failure_class = $failureClass
        $state.failure_detail = 'Review orchestration failed after transaction preparation.'
        $null = Write-CodeReviewTransaction -TransactionPath $transactionPath -State $state
    }
    $_.Exception.Data['DNPPVTransactionPath'] = [IO.Path]::GetRelativePath($repo,$transactionPath).Replace('\','/')
    throw
}
finally { Exit-CodeReviewLease -Lease $lease }
