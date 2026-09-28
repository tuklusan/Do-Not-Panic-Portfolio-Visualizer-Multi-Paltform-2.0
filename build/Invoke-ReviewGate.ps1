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
    [ValidateSet('Primary','Fallback')][string]$HealthModelRole,
    [string]$PacketPath,
    [string]$TransactionPath,
    [Parameter(Mandatory)][DateTimeOffset]$DeadlineUtc,
    [string]$RepositoryRoot,
    [string]$CandidateRepositoryRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'DNPPV review infrastructure requires PowerShell 7 or later.' }
. ([IO.Path]::Combine($RepositoryRoot,'build','CodeReviewerCommon.ps1'))
$primaryModel = 'nvidia/nemotron-3-super-120b-a12b'
$fallbackModel = 'nvidia/nemotron-3.5-lightning-30b-a3b'
$reviewerId = 'dnppv2-nvidia-review-gate-v2'
$adapterPath = [IO.Path]::Combine($RepositoryRoot,'build','Invoke-NvidiaReviewHarness.ps1')

function Invoke-SelectedNvidiaStage {
    param([string]$SelectedStage,[string]$SelectedModel,[string]$FindingsJson)
    $arguments = @{
        Operation = 'Review'; Stage = $SelectedStage; Model = $SelectedModel
        RepositoryRoot = $RepositoryRoot; PacketPath = $PacketPath; TransactionPath = $TransactionPath
        DeadlineUtc = $DeadlineUtc
    }
    if ($FindingsJson) { $arguments.FindingsJson = $FindingsJson }
    return & $adapterPath @arguments
}

function Invoke-ReviewProtocolPacketAudit {
    param([Parameter(Mandatory)][string]$FailedStage,[Parameter(Mandatory)][string]$FailedModel)
    $state = Read-CodeReviewTransaction -TransactionPath $TransactionPath
    $directory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($TransactionPath))
    $resolve = {
        param([string]$Relative)
        if ([string]::IsNullOrEmpty($Relative) -or $Relative -notmatch '^[A-Za-z0-9_./-]+$' -or
            $Relative -match '(^|/)(\.|\.\.)(/|$)' -or $Relative.StartsWith('/') -or $Relative.Contains('//')) {
            throw 'Audit evidence path is not canonical.'
        }
        $path = [IO.Path]::GetFullPath([IO.Path]::Combine($directory,$Relative.Replace('/',[IO.Path]::DirectorySeparatorChar)))
        if (-not $path.StartsWith($directory + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Audit evidence escapes transaction.' }
        $cursor = $directory
        foreach ($segment in $Relative.Split('/')) {
            $cursor = [IO.Path]::Combine($cursor,$segment)
            if ([IO.File]::Exists($cursor) -or [IO.Directory]::Exists($cursor)) {
                if (([IO.File]::GetAttributes($cursor) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw 'Audit evidence traverses a link.'
                }
            }
        }
        return $path
    }
    $request = @($state.request_evidence | Where-Object { $_.stage -ceq $FailedStage } | Select-Object -Last 1)
    $response = @($state.response_evidence | Where-Object { $_.stage -ceq $FailedStage -and $_.http_status -eq 200 } | Select-Object -Last 1)
    $packetHashValid = (Get-CodeReviewBytesSha256 -Bytes $packetBytes) -ceq $state.packet_sha256
    $packetSchemaValid = $packet -is [Collections.IDictionary] -and
        $packet.Contains('schema') -and $packet.Contains('review_type') -and
        $packet.Contains('requirement') -and $packet.Contains('requirement_sha256') -and
        $packet.requirement -is [string] -and -not [string]::IsNullOrWhiteSpace($packet.requirement) -and
        $packet.schema -cin @('dnppv2-review-packet/v2','dnppv2-review-material-packet/v1') -and
        $packet.review_type -ceq $state.review_type -and
        $packet.requirement_sha256 -ceq (Get-CodeReviewSha256 -Text $packet.requirement)
    if ($packetSchemaValid) {
        try {
            $rootKeys = if ($state.review_type -eq 'CODE') {
                @('schema','review_type','scope','requirement','requirement_sha256','candidate','changed_files','context_files','packet_policy')
            } else { @('schema','review_type','scope','requirement','requirement_sha256','candidate','material') }
            Assert-CodeReviewExactKeys -Object $packet -Names $rootKeys
            if ($state.review_type -eq 'CODE') {
                Assert-CodeReviewExactKeys -Object $packet.candidate -Names @('base_sha','head_sha','base_tree','head_tree','snapshot_sha256')
                if ($packet.changed_files -isnot [array] -or $packet.changed_files.Count -eq 0 -or $packet.context_files -isnot [array]) { throw 'Packet collections invalid.' }
            }
            else { Assert-CodeReviewExactKeys -Object $packet.candidate -Names @('head_sha','head_tree') }
        }
        catch { $packetSchemaValid = $false }
    }
    $candidateValid = $false
    if ($packetSchemaValid) {
        $candidateValid = $packet.candidate.head_sha -ceq $state.head_sha -and $packet.scope -ceq $state.scope
        if ($state.review_type -eq 'CODE') {
            $candidateValid = $candidateValid -and $packet.candidate.base_sha -ceq $state.base_sha -and
                $packet.candidate.snapshot_sha256 -ceq $state.snapshot_sha256
            try {
                $descriptor = New-CodeReviewCandidateDescriptorV2 -RepositoryRoot $CandidateRepositoryRoot -BaseSha $state.base_sha -HeadSha $state.head_sha -Scope $state.scope -Requirement $packet.requirement
                $candidateValid = $candidateValid -and $descriptor.SnapshotSha256 -ceq $state.snapshot_sha256 -and
                    $descriptor.BaseTree -ceq $packet.candidate.base_tree -and $descriptor.HeadTree -ceq $packet.candidate.head_tree -and
                    @($descriptor.Entries).Count -eq @($packet.changed_files).Count
                if ($candidateValid) {
                    for ($i = 0; $i -lt $descriptor.Entries.Count; $i++) {
                        if ($descriptor.Entries[$i].Path -cne $packet.changed_files[$i].path -or
                            $descriptor.Entries[$i].HeadBlob -cne $packet.changed_files[$i].head_blob) { $candidateValid = $false; break }
                    }
                }
            }
            catch { $candidateValid = $false }
        }
        else {
            try {
                $material = New-CodeReviewMaterialPacketV1 -RepositoryRoot $CandidateRepositoryRoot -ReviewType $state.review_type -HeadSha $state.head_sha -Scope $state.scope -Requirement $packet.requirement -ReviewMaterialPath $packet.material.source_path
                $candidateValid = $candidateValid -and $packet.candidate.head_tree -ceq $material.Packet.candidate.head_tree -and
                    $state.snapshot_sha256 -ceq $material.SnapshotSha256 -and
                    $state.packet_sha256 -ceq $material.PacketSha256
            }
            catch { $candidateValid = $false }
        }
    }
    $requestJsonValid = $false; $requestContractValid = $false; $requestSizeValid = $false
    $requestSha = $null
    if ($request.Count -eq 1) {
        try {
            $requestFile = & $resolve $request[0].request_body_path
            $requestBody = [IO.File]::ReadAllBytes($requestFile)
            $requestSha = Get-CodeReviewBytesSha256 -Bytes $requestBody
            $requestSizeValid = $requestBody.Length -le 1048576 -and $requestBody.Length -eq $request[0].request_body_byte_length
            $requestObject = [Text.UTF8Encoding]::new($false,$true).GetString($requestBody) | ConvertFrom-Json -AsHashtable -ErrorAction Stop
            $requestJsonValid = $requestObject -is [Collections.IDictionary]
            if ($requestJsonValid -and $requestSha -ceq $request[0].request_body_sha256) {
                . $adapterPath -Operation Review -Stage $FailedStage -Model $FailedModel -RepositoryRoot $RepositoryRoot -PacketPath $PacketPath -TransactionPath $TransactionPath -DeadlineUtc $DeadlineUtc -BuildRequestOnly
                $findings = if ($FailedStage -eq 'Adjudication') { $requestObject.messages[1].content -split "`nFINDINGS_JSON`n",2 | Select-Object -Last 1 } else { $null }
                $expected = New-NvidiaRequestBytes -SelectedModel $FailedModel -SelectedStage $FailedStage -PacketJson ([Text.UTF8Encoding]::new($false,$true).GetString($packetBytes)) -FindingsJson $findings
                $requestContractValid = (Get-CodeReviewBytesSha256 -Bytes $expected) -ceq $requestSha
            }
        }
        catch { $requestJsonValid = $false; $requestContractValid = $false }
    }
    $secretValid = $false
    try { Assert-CodeReviewNoLikelySecrets -Text ([Text.UTF8Encoding]::new($false,$true).GetString($packetBytes)); $secretValid = $true }
    catch { }
    $envelopeValid = $false; $finishValid = $false; $parserValid = $false; $contentSha = $null; $wireSha = $null
    if ($response.Count -eq 1) {
        try {
            $safeBytes = [IO.File]::ReadAllBytes((& $resolve $response[0].safe_envelope_path))
            $contentBytes = [IO.File]::ReadAllBytes((& $resolve $response[0].final_content_path))
            $metaBytes = [IO.File]::ReadAllBytes((& $resolve $response[0].response_meta_path))
            $contentSha = Get-CodeReviewBytesSha256 -Bytes $contentBytes
            $wireSha = $response[0].wire_sha256
            $safe = [Text.UTF8Encoding]::new($false,$true).GetString($safeBytes) | ConvertFrom-Json -AsHashtable -ErrorAction Stop
            $meta = [Text.UTF8Encoding]::new($false,$true).GetString($metaBytes) | ConvertFrom-Json -AsHashtable -ErrorAction Stop
            $envelopeValid = (Get-CodeReviewBytesSha256 -Bytes $safeBytes) -ceq $response[0].safe_envelope_sha256 -and
                (Get-CodeReviewBytesSha256 -Bytes $metaBytes) -ceq $response[0].response_meta_sha256 -and
                $contentSha -ceq $response[0].final_content_sha256 -and $meta.wire_sha256 -ceq $wireSha
            $finishValid = $envelopeValid -and $safe.choices[0].finish_reason -ceq 'stop'
            $content = [Text.UTF8Encoding]::new($false,$true).GetString($contentBytes)
            try {
                if ($FailedStage -eq 'Adjudication') { $null = ConvertFrom-CodeReviewAdjudicationContent -Content $content -Packet $packet -Findings $normalizedFindings }
                else { $null = ConvertFrom-CodeReviewModelContent -Content $content -Packet $packet }
                $parserValid = $true
            }
            catch { $parserValid = $_.Exception.Data['DNPPVFailureClass'] -ceq 'MODEL_PROTOCOL_FAILURE' }
        }
        catch { $envelopeValid = $false; $finishValid = $false; $parserValid = $false }
    }
    $audit = [ordered]@{
        schema = 'dnppv2-packet-audit/v1'; transaction_id = $state.transaction_id; stage = $FailedStage; model = $FailedModel
        packet_sha256 = $state.packet_sha256; request_body_sha256 = $requestSha; response_wire_sha256 = $wireSha; final_content_sha256 = $contentSha
        packet_hash_valid = [bool]$packetHashValid; packet_schema_valid = [bool]$packetSchemaValid; candidate_identity_valid = [bool]$candidateValid
        request_json_valid = [bool]$requestJsonValid; request_contract_valid = [bool]$requestContractValid; request_size_valid = [bool]$requestSizeValid
        secret_scan_valid = [bool]$secretValid; provider_envelope_valid = [bool]$envelopeValid; terminal_finish_valid = [bool]$finishValid
        semantic_parser_replay_valid = [bool]$parserValid; audit_complete = $true; audited_utc = [DateTimeOffset]::UtcNow.ToString('o')
    }
    $auditBytes = ConvertTo-CodeReviewJsonBytes -Value $audit
    $auditName = switch ($FailedStage) {
        'ProtocolReplay' { 'packet-audit-protocol-replay.json' }
        'Adjudication' { 'packet-audit-adjudication.json' }
        default { 'packet-audit.json' }
    }
    Write-CodeReviewAtomicBytes -Path ([IO.Path]::Combine($directory,$auditName)) -Bytes $auditBytes
    $state.packet_audit_path = $auditName
    $state.packet_audit_sha256 = Get-CodeReviewBytesSha256 -Bytes $auditBytes
    $state.packet_audit_complete = $true
    $state.failure_class = 'MODEL_PROTOCOL_FAILURE'
    $state.failure_detail = 'Final content failed protocol validation; retained packet/request/response audit persisted.'
    $state.state = 'FAILED_PROTOCOL'
    $null = Write-CodeReviewTransaction -TransactionPath $TransactionPath -State $state
    return $audit
}

if ($Operation -eq 'Health') {
    if (-not $HealthModelRole) { throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'Health model role is missing.') }
    $selectedModel = if ($HealthModelRole -eq 'Primary') { $primaryModel } else { $fallbackModel }
    $arguments = @{
        Operation = 'Health'; Stage = 'Health'; Model = $selectedModel
        RepositoryRoot = $RepositoryRoot; DeadlineUtc = $DeadlineUtc
    }
    return & $adapterPath @arguments
}

if (-not $ReviewType -or -not $PacketPath -or -not $TransactionPath) {
    throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'Review policy inputs are incomplete.')
}
if (-not $CandidateRepositoryRoot) {
    throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'Candidate repository root is missing.')
}
$packetBytes = [IO.File]::ReadAllBytes($PacketPath)
$transaction = Read-CodeReviewTransaction -TransactionPath $TransactionPath
if ((Get-CodeReviewBytesSha256 -Bytes $packetBytes) -cne $transaction.packet_sha256) {
    throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'Policy packet hash mismatch.')
}
$packet = [Text.UTF8Encoding]::new($false,$true).GetString($packetBytes) | ConvertFrom-Json -AsHashtable -ErrorAction Stop
if ($packet.review_type -cne $ReviewType) { throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'Policy review type mismatch.') }

$selectedModel = $primaryModel
$fallbackUsed = $false
try { $reviewStage = Invoke-SelectedNvidiaStage -SelectedStage Primary -SelectedModel $selectedModel }
catch {
    $failureClass = $_.Exception.Data['DNPPVFailureClass']
    if ($failureClass -eq 'MODEL_PROTOCOL_FAILURE') {
        $null = Invoke-ReviewProtocolPacketAudit -FailedStage Primary -FailedModel $selectedModel
        throw
    }
    if ($failureClass -notin @('PROVIDER_MODEL_UNAVAILABLE','PROVIDER_MODEL_BACKEND_FAILURE')) { throw }
    if ([DateTimeOffset]::UtcNow -ge $DeadlineUtc) { throw (New-CodeReviewFailure -FailureClass 'DEADLINE_EXCEEDED' -Message 'No time remains for availability fallback.') }
    $selectedModel = $fallbackModel
    $fallbackUsed = $true
    try { $reviewStage = Invoke-SelectedNvidiaStage -SelectedStage Fallback -SelectedModel $selectedModel }
    catch {
        if ($_.Exception.Data['DNPPVFailureClass'] -eq 'MODEL_PROTOCOL_FAILURE') {
            $null = Invoke-ReviewProtocolPacketAudit -FailedStage Fallback -FailedModel $selectedModel
        }
        throw
    }
}

try { $semantic = ConvertFrom-CodeReviewModelContent -Content $reviewStage.content -Packet $packet }
catch {
    if ($_.Exception.Data['DNPPVFailureClass'] -eq 'MODEL_PROTOCOL_FAILURE') {
        $audit = Invoke-ReviewProtocolPacketAudit -FailedStage $(if ($fallbackUsed) { 'Fallback' } else { 'Primary' }) -FailedModel $selectedModel
        $state = Read-CodeReviewTransaction -TransactionPath $TransactionPath
        if ((Test-CodeReviewProtocolReplayAuthorization -TransactionPath $TransactionPath -Transaction $state -Model $selectedModel) -and
            [DateTimeOffset]::UtcNow.AddSeconds(15) -lt $DeadlineUtc) {
            try {
                $reviewStage = Invoke-SelectedNvidiaStage -SelectedStage ProtocolReplay -SelectedModel $selectedModel
                $semantic = ConvertFrom-CodeReviewModelContent -Content $reviewStage.content -Packet $packet
            }
            catch {
                $replayFailure = $_.Exception.Data['DNPPVFailureClass']
                if ($replayFailure -eq 'MODEL_PROTOCOL_FAILURE') {
                    $replayAudit = Invoke-ReviewProtocolPacketAudit -FailedStage ProtocolReplay -FailedModel $selectedModel
                    if (-not $replayAudit.provider_envelope_valid) { throw }
                    return [pscustomobject]@{
                        reviewer_id=$reviewerId; review_type=$ReviewType; review_complete=$false; verdict='INCONCLUSIVE'
                        blocking_findings=@(); uncertainties=@([ordered]@{ id='U-PROTOCOL-REPLAY'; path=$null; reason='Both same-model responses failed semantic protocol validation.'; required_evidence='A deliberate corrected review attempt.' })
                        review_stage_count=0; fallback_used=$fallbackUsed; model=$selectedModel; semantic_content_sha256s=@()
                    }
                }
                throw
            }
        }
        else { throw }
    }
    else { throw }
}
$stageCount = 1
$semanticHashes = @((Get-CodeReviewSha256 -Text $reviewStage.content))
$findings = @()
$uncertainties = @($semantic.uncertainties)
$complete = [bool]$semantic.review_complete
$verdict = 'INCONCLUSIVE'
if ($complete -and $uncertainties.Count -eq 0) {
    if ($semantic.blocking_findings.Count -eq 0) {
        $verdict = 'PASS'
    }
    else {
        if ([DateTimeOffset]::UtcNow -ge $DeadlineUtc) {
            $verdict = 'INCONCLUSIVE'
        }
        else {
            $normalizedFindings = @($semantic.blocking_findings | ForEach-Object {
                [ordered]@{
                    id = $_.id; severity = $_.severity; category = $_.category; requirement_quote = $_.requirement_quote
                    path = $_.path; evidence_path = $_.evidence_path; evidence_quote = $_.evidence_quote
                    problem = $_.problem; required_outcome = $_.required_outcome
                }
            })
            $findingsJson = [Text.UTF8Encoding]::new($false).GetString((ConvertTo-CodeReviewJsonBytes -Value $normalizedFindings))
            try {
                $adjudicationStage = Invoke-SelectedNvidiaStage -SelectedStage Adjudication -SelectedModel $selectedModel -FindingsJson $findingsJson
                $adjudication = ConvertFrom-CodeReviewAdjudicationContent -Content $adjudicationStage.content -Packet $packet -Findings $normalizedFindings
                if ($adjudication.review_complete -and $adjudication.uncertainties.Count -eq 0) {
                    $stageCount = 2
                    $semanticHashes = @($semanticHashes) + @((Get-CodeReviewSha256 -Text $adjudicationStage.content))
                    $confirmed = @($adjudication.decisions | Where-Object { $_.decision -ceq 'CONFIRMED' } | ForEach-Object { $_.id })
                    $findings = @($normalizedFindings | Where-Object { $_.id -cin $confirmed })
                    $verdict = if ($findings.Count -gt 0) { 'FAIL' } else { 'PASS' }
                }
                else { $uncertainties = @($adjudication.uncertainties) }
            }
            catch {
                $failureClass = $_.Exception.Data['DNPPVFailureClass']
                if ($failureClass -eq 'MODEL_PROTOCOL_FAILURE') {
                    $null = Invoke-ReviewProtocolPacketAudit -FailedStage Adjudication -FailedModel $selectedModel
                }
                elseif ($failureClass -ne 'DEADLINE_EXCEEDED') { throw }
                $verdict = 'INCONCLUSIVE'
            }
        }
    }
}

return [pscustomobject]@{
    reviewer_id = $reviewerId
    review_type = $ReviewType
    review_complete = $complete
    verdict = $verdict
    blocking_findings = $findings
    uncertainties = $uncertainties
    review_stage_count = $stageCount
    fallback_used = $fallbackUsed
    model = $selectedModel
    semantic_content_sha256s = $semanticHashes
}
