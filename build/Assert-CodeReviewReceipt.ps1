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
    [Parameter(Mandatory)][ValidateSet('Create','Validate')][string]$Operation,
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [Parameter(Mandatory)][string]$BaseSha,
    [Parameter(Mandatory)][string]$NewSha,
    [string]$Scope,
    [string]$ResultPath,
    [string]$RemoteOldSha,
    [string]$RemoteRef = 'refs/heads/main'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$implementationRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..'))
. ([IO.Path]::Combine($implementationRoot,'build','CodeReviewerCommon.ps1'))
$repo = Resolve-CodeReviewRepositoryRoot -RepositoryRoot $RepositoryRoot
$reviewRoot = [IO.Path]::Combine($repo,'build','code-review')
$receiptsRoot = [IO.Path]::Combine($reviewRoot,'receipts')
if ($BaseSha -notmatch '^[0-9a-fA-F]{40}$' -or $NewSha -notmatch '^[0-9a-fA-F]{40}$' -or $BaseSha -ceq $NewSha) {
    throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Receipt candidate SHA is invalid.')
}
$BaseSha = $BaseSha.ToLowerInvariant(); $NewSha = $NewSha.ToLowerInvariant()
& git -C $repo merge-base --is-ancestor $BaseSha $NewSha 2>$null
if ($LASTEXITCODE -ne 0) {
    throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Receipt candidate is not a fast-forward descendant of the reviewed base.')
}
$receiptPath = [IO.Path]::Combine($receiptsRoot,"$NewSha.json")

function Read-ExactJson([string]$Path) {
    if (-not [IO.File]::Exists($Path)) { throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Required receipt evidence file is missing.') }
    $bytes = [IO.File]::ReadAllBytes($Path)
    try { $value = [Text.UTF8Encoding]::new($false,$true).GetString($bytes) | ConvertFrom-Json -AsHashtable -ErrorAction Stop }
    catch { throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Receipt evidence JSON is invalid.') }
    return [pscustomobject]@{ Value=$value; Bytes=$bytes; Sha=(Get-CodeReviewBytesSha256 -Bytes $bytes) }
}

function Resolve-Evidence([string]$TransactionDirectory,[string]$Relative) {
    if ($Relative -isnot [string] -or $Relative -notmatch '^[A-Za-z0-9_./-]+$' -or
        $Relative -match '(^|/)(\.|\.\.)(/|$)' -or $Relative.StartsWith('/') -or $Relative.Contains('//')) {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Evidence path is not canonical.')
    }
    $full = [IO.Path]::GetFullPath([IO.Path]::Combine($TransactionDirectory,$Relative.Replace('/',[IO.Path]::DirectorySeparatorChar)))
    if (-not $full.StartsWith($TransactionDirectory + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Evidence path leaves transaction directory.')
    }
    $cursor = $TransactionDirectory
    foreach ($segment in $Relative.Split('/')) {
        $cursor = [IO.Path]::Combine($cursor,$segment)
        if ([IO.File]::Exists($cursor) -or [IO.Directory]::Exists($cursor)) {
            if (([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Evidence path traverses a link.')
            }
        }
    }
    return $full
}

function Verify-Evidence([string]$Path,[string]$Hash,[Nullable[int]]$Length) {
    if (-not [IO.File]::Exists($Path) -or $Hash -notmatch '^[0-9a-f]{64}$') {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Referenced evidence is missing or hash is invalid.')
    }
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ((Get-CodeReviewBytesSha256 -Bytes $bytes) -cne $Hash -or ($null -ne $Length -and $bytes.Length -ne $Length)) {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Referenced evidence hash or length changed.')
    }
    return ,$bytes
}

function Test-ReceiptWholeNumber($Value) {
    return ($Value -is [int] -or $Value -is [long]) -and $Value -ge 0
}

function Build-VerifiedReceipt([string]$SelectedResultPath,[string]$ExpectedScope) {
    $resultFull = [IO.Path]::GetFullPath($SelectedResultPath)
    $transactionDirectory = [IO.Path]::GetDirectoryName($resultFull)
    $expectedPrefix = [IO.Path]::Combine($reviewRoot,'transactions',$NewSha)
    if (-not $transactionDirectory.StartsWith($expectedPrefix + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resultFull) -cne 'result.json') {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Result is outside the fixed CODE transaction tree.')
    }
    $resultFile = Read-ExactJson -Path $resultFull
    $result = $resultFile.Value
    $transactionPath = [IO.Path]::Combine($transactionDirectory,'transaction.json')
    $packetPath = [IO.Path]::Combine($transactionDirectory,'packet.json')
    $transactionFile = Read-ExactJson -Path $transactionPath
    $packetFile = Read-ExactJson -Path $packetPath
    $state = $transactionFile.Value; $packet = $packetFile.Value
    if ($result.schema -cne 'dnppv2-review-result/v3' -or $result.reviewer_id -cne 'dnppv2-nvidia-review-gate-v2' -or
        $result.review_type -cne 'CODE' -or $result.base_sha -cne $BaseSha -or $result.head_sha -cne $NewSha -or
        $result.review_complete -isnot [bool] -or -not $result.review_complete -or
        $result.fallback_used -isnot [bool] -or $result.verdict -cne 'PASS' -or
        $result.blocking_findings -isnot [array] -or $result.blocking_findings.Count -ne 0 -or
        $result.uncertainties -isnot [array] -or $result.uncertainties.Count -ne 0 -or
        $result.packet_sha256 -cne $packetFile.Sha -or $result.transaction_id -cne $state.transaction_id -or
        $state.schema -cne 'dnppv2-review-transaction/v1' -or $state.state -cne 'COMPLETED_PASS' -or
        $state.result_sha256 -cne $resultFile.Sha -or $state.packet_sha256 -cne $packetFile.Sha -or
        $state.snapshot_sha256 -cne $result.snapshot_sha256 -or $state.base_sha -cne $BaseSha -or
        $state.head_sha -cne $NewSha -or $state.scope -cne $result.scope -or
        $packet.schema -cne 'dnppv2-review-packet/v2' -or $packet.candidate.base_sha -cne $BaseSha -or
        $packet.candidate.head_sha -cne $NewSha -or $packet.scope -cne $result.scope -or
        $packet.candidate.snapshot_sha256 -cne $result.snapshot_sha256 -or
        ($ExpectedScope -and $result.scope -cne $ExpectedScope)) {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Receipt result, packet, or transaction identity is inconsistent.')
    }
    $descriptor = New-CodeReviewCandidateDescriptorV2 -RepositoryRoot $repo -BaseSha $BaseSha -HeadSha $NewSha -Scope $result.scope -Requirement $packet.requirement
    if ($descriptor.SnapshotSha256 -cne $result.snapshot_sha256 -or
        $descriptor.BaseTree -cne $packet.candidate.base_tree -or $descriptor.HeadTree -cne $packet.candidate.head_tree -or
        @($descriptor.Entries).Count -ne @($packet.changed_files).Count) {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Git candidate descriptor differs from reviewed packet.')
    }
    for ($i=0; $i -lt $descriptor.Entries.Count; $i++) {
        if ($descriptor.Entries[$i].Path -cne $packet.changed_files[$i].path -or
            $descriptor.Entries[$i].BaseBlob -cne $packet.changed_files[$i].base_blob -or
            $descriptor.Entries[$i].HeadBlob -cne $packet.changed_files[$i].head_blob) {
            throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Packet changed-file identity differs from Git.')
        }
    }
    if (-not (Test-ReceiptWholeNumber $state.provider_post_attempt_count) -or
        -not (Test-ReceiptWholeNumber $result.provider_post_attempt_count) -or
        -not (Test-ReceiptWholeNumber $result.review_stage_count) -or
        $state.provider_post_attempt_count -lt 1 -or
        $state.provider_post_attempt_count -ne $result.provider_post_attempt_count -or
        $state.provider_post_attempt_count -ne @($state.models_attempted).Count -or
        $state.provider_post_attempt_count -ne @($result.models_attempted).Count -or
        $state.provider_post_attempt_count -ne @($state.request_evidence).Count -or
        $result.review_stage_count -ne @($result.semantic_content_sha256s).Count) {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Review dispatch or semantic cardinality differs.')
    }
    for ($i=0; $i -lt $state.provider_post_attempt_count; $i++) {
        $request = $state.request_evidence[$i]
        if ($request.post_attempt_ordinal -ne $i+1 -or $request.model -cne $state.models_attempted[$i] -or
            $request.model -cne $result.models_attempted[$i] -or $request.correlation_id -notmatch '^[0-9a-f-]{36}$') {
            throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Request dispatch history is inconsistent.')
        }
        $requestPath = Resolve-Evidence -TransactionDirectory $transactionDirectory -Relative $request.request_body_path
        $null = Verify-Evidence -Path $requestPath -Hash $request.request_body_sha256 -Length ([int]$request.request_body_byte_length)
        $expectedMeta = Resolve-Evidence -TransactionDirectory $transactionDirectory -Relative $request.expected_response_meta_path
        if ($expectedMeta -cne [IO.Path]::Combine($transactionDirectory,('responses/post-{0:D4}/response-meta.json' -f ($i+1)).Replace('/',[IO.Path]::DirectorySeparatorChar))) {
            throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Pre-recorded response path is inconsistent.')
        }
    }
    $terminalHashes = [Collections.Generic.List[string]]::new()
    foreach ($response in @($state.response_evidence)) {
        $ordinal = [int]$response.post_attempt_ordinal
        if ($ordinal -lt 1 -or $ordinal -gt $state.provider_post_attempt_count -or
            $response.stage -cne $state.request_evidence[$ordinal-1].stage -or
            $response.correlation_id -cne $state.request_evidence[$ordinal-1].correlation_id -or
            $response.wire_sha256 -notmatch '^[0-9a-f]{64}$' -or $response.wire_byte_length -lt 0) {
            throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Response evidence identity is inconsistent.')
        }
        $metaBytes = Verify-Evidence -Path (Resolve-Evidence -TransactionDirectory $transactionDirectory -Relative $response.response_meta_path) -Hash $response.response_meta_sha256
        $meta = [Text.UTF8Encoding]::new($false,$true).GetString($metaBytes) | ConvertFrom-Json -AsHashtable -ErrorAction Stop
        if ($meta.wire_sha256 -cne $response.wire_sha256 -or $meta.wire_byte_length -ne $response.wire_byte_length -or
            $meta.http_status -ne $response.http_status -or $meta.stage -cne $response.stage -or
            $meta.correlation_id -cne $response.correlation_id) {
            throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Response metadata conflicts with transaction.')
        }
        if ($response.safe_envelope_path) {
            $null = Verify-Evidence -Path (Resolve-Evidence -TransactionDirectory $transactionDirectory -Relative $response.safe_envelope_path) -Hash $response.safe_envelope_sha256
        }
        if ($response.final_content_path) {
            $null = Verify-Evidence -Path (Resolve-Evidence -TransactionDirectory $transactionDirectory -Relative $response.final_content_path) -Hash $response.final_content_sha256
            $terminalHashes.Add($response.final_content_sha256)
        }
    }
    if ($state.packet_audit_path) {
        $null = Verify-Evidence -Path (Resolve-Evidence -TransactionDirectory $transactionDirectory -Relative $state.packet_audit_path) -Hash $state.packet_audit_sha256
    }
    $replayRequests = @($state.request_evidence | Where-Object { $_.stage -ceq 'ProtocolReplay' })
    if ($replayRequests.Count -gt 1) {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'More than one protocol replay was recorded.')
    }
    if ($replayRequests.Count -eq 1) {
        $replay = $replayRequests[0]
        if ($state.packet_audit_path -cne 'packet-audit.json' -or
            $replay.packet_audit_sha256 -cne $state.packet_audit_sha256) {
            throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Replay audit lineage differs from completed PASS transaction.')
        }
        $auditBytes = Verify-Evidence -Path (Resolve-Evidence -TransactionDirectory $transactionDirectory -Relative 'packet-audit.json') -Hash $replay.packet_audit_sha256
        try { $audit = [Text.UTF8Encoding]::new($false,$true).GetString($auditBytes) | ConvertFrom-Json -AsHashtable -ErrorAction Stop }
        catch { throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Replay audit JSON is invalid.') }
        $priorResponses = @($state.response_evidence | Where-Object {
            $_.stage -ceq $replay.protocol_replay_of_stage -and $_.http_status -eq 200 -and
            $_.post_attempt_ordinal -lt $replay.post_attempt_ordinal
        } | Select-Object -Last 1)
        if ($audit.schema -cne 'dnppv2-packet-audit/v1' -or $audit.transaction_id -cne $state.transaction_id -or
            $audit.packet_sha256 -cne $packetFile.Sha -or $audit.stage -cne $replay.protocol_replay_of_stage -or
            $audit.model -cne $replay.model -or $priorResponses.Count -ne 1 -or
            $audit.final_content_sha256 -cne $replay.prior_final_content_sha256 -or
            $audit.response_wire_sha256 -cne $replay.prior_response_wire_sha256 -or
            $priorResponses[0].final_content_sha256 -cne $audit.final_content_sha256 -or
            $priorResponses[0].wire_sha256 -cne $audit.response_wire_sha256) {
            throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Replay request, audit, and prior response are not bound.')
        }
    }
    $cursor = 0
    foreach ($semanticHash in @($result.semantic_content_sha256s)) {
        while ($cursor -lt $terminalHashes.Count -and $terminalHashes[$cursor] -cne $semanticHash) { $cursor++ }
        if ($cursor -ge $terminalHashes.Count) { throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Semantic content does not match retained response order.') }
        $cursor++
    }
    $relative = { param($path) [IO.Path]::GetRelativePath($repo,$path).Replace('\','/') }
    return [ordered]@{
        schema='dnppv2-code-review-receipt/v2'; reviewerId='dnppv2-nvidia-review-gate-v2'; reviewProtocol='v2'; transactionId=$state.transaction_id
        baseSha=$BaseSha; headSha=$NewSha; baseTree=$descriptor.BaseTree; headTree=$descriptor.HeadTree; scope=$result.scope
        snapshotSha256=$result.snapshot_sha256; packetSha256=$packetFile.Sha; transactionSha256=$transactionFile.Sha; resultSha256=$resultFile.Sha
        transactionPath=(& $relative $transactionPath); packetPath=(& $relative $packetPath); resultPath=(& $relative $resultFull)
        reviewComplete=$true; verdict='PASS'; blockingFindingCount=0
    }
}

if ($Operation -eq 'Create') {
    if (-not $Scope -or -not $ResultPath) { throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'Receipt creation requires scope and result path.') }
    $null = Assert-CodeReviewCommittedCandidate -RepositoryRoot $repo -BaseSha $BaseSha -HeadSha $NewSha
    $proof = Build-VerifiedReceipt -SelectedResultPath $ResultPath -ExpectedScope $Scope
    $null = Write-CodeReviewAtomicJson -Path $receiptPath -Value $proof
    return [pscustomobject]@{ receipt_path=$receiptPath; receipt_sha256=(Get-CodeReviewBytesSha256 -Bytes ([IO.File]::ReadAllBytes($receiptPath))) }
}
if ($RemoteRef -cne 'refs/heads/main' -or $RemoteOldSha -cne $BaseSha) {
    throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Protected push ref/base does not match receipt request.')
}
$receiptFile = Read-ExactJson -Path $receiptPath
$receipt = $receiptFile.Value
if ($receipt.schema -cne 'dnppv2-code-review-receipt/v2' -or $receipt.baseSha -cne $BaseSha -or $receipt.headSha -cne $NewSha -or
    $receipt.reviewComplete -isnot [bool] -or -not (Test-ReceiptWholeNumber $receipt.blockingFindingCount) -or
    $receipt.resultPath -notmatch '^build/code-review/transactions/[0-9a-f]{40}/[0-9a-f]{64}/[0-9a-f]{32}/result\.json$') {
    throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Receipt identity or result path is invalid.')
}
$resultFull = Resolve-CodeReviewPath -RepositoryRoot $repo -Path $receipt.resultPath -InsideRepository
$expected = Build-VerifiedReceipt -SelectedResultPath $resultFull -ExpectedScope $receipt.scope
foreach ($key in $expected.Keys) {
    if ($receipt[$key] -cne $expected[$key]) {
        throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Receipt no longer matches retained evidence.')
    }
}
if ($receipt.Count -ne $expected.Count) { throw (New-CodeReviewFailure -FailureClass 'RECEIPT_VALIDATION_FAILURE' -Message 'Receipt contains unexpected fields.') }
return [pscustomobject]@{ receipt_path=$receiptPath; receipt_sha256=$receiptFile.Sha; valid=$true }
