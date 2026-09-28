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
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Policy routing test requires PowerShell 7 or later.' }
$projectRoot = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..'))
$testParent = [IO.Path]::Combine($projectRoot,'build','code-review','test-fixtures')
$fixture = [IO.Path]::Combine($testParent,[guid]::NewGuid().ToString('N'))
$fixtureBuild = [IO.Path]::Combine($fixture,'build')
[IO.Directory]::CreateDirectory($fixtureBuild) | Out-Null
try {
    [IO.File]::Copy([IO.Path]::Combine($PSScriptRoot,'CodeReviewerCommon.ps1'),[IO.Path]::Combine($fixtureBuild,'CodeReviewerCommon.ps1'))
    [IO.File]::Copy([IO.Path]::Combine($PSScriptRoot,'Invoke-NvidiaReviewHarness.ps1'),[IO.Path]::Combine($fixtureBuild,'RealNvidiaAdapter.ps1'))
    $stub = @'
param([string]$Operation,[string]$Stage,[string]$Model,[string]$RepositoryRoot,[string]$PacketPath,[string]$TransactionPath,[DateTimeOffset]$DeadlineUtc,[string]$FindingsJson,[switch]$BuildRequestOnly)
$requestedOperation = $Operation; $requestedStage = $Stage; $requestedBuildOnly = [bool]$BuildRequestOnly
$requestedFindingsJson = $FindingsJson
$requestedPacketPath = $PacketPath; $requestedTransactionPath = $TransactionPath
. ([IO.Path]::Combine($RepositoryRoot,'build','RealNvidiaAdapter.ps1')) -Operation Health -Stage Health -Model $Model -RepositoryRoot $RepositoryRoot -DeadlineUtc $DeadlineUtc -BuildRequestOnly
$Operation = $requestedOperation; $Stage = $requestedStage; $BuildRequestOnly = $requestedBuildOnly
$FindingsJson = $requestedFindingsJson
$PacketPath = $requestedPacketPath; $TransactionPath = $requestedTransactionPath
if ($BuildRequestOnly) { return }
$mode = [IO.File]::ReadAllText([IO.Path]::Combine($RepositoryRoot,'mode.txt')).Trim()
[IO.File]::AppendAllText([IO.Path]::Combine($RepositoryRoot,'calls.txt'),"$Stage|$Model`n")
if ($Operation -eq 'Health') { return [pscustomobject]@{ success=$true; stage='Health'; model=$Model; provider_post_attempt_delta=1 } }
if ($mode -in @('protocol-complete','protocol-length','protocol-noncanonical','protocol-linked','protocol-replay-malformed','adjudication-malformed')) {
    $packetJson = [IO.File]::ReadAllText($PacketPath,[Text.UTF8Encoding]::new($false,$true))
    $requestBytes = New-NvidiaRequestBytes -SelectedModel $Model -SelectedStage $Stage -PacketJson $packetJson -FindingsJson $FindingsJson
    $attempt = Start-CodeReviewPostAttempt -TransactionPath $TransactionPath -Stage $Stage -Model $Model -RequestBytes $requestBytes -SafeEndpoint 'https://integrate.api.nvidia.com/v1/chat/completions' -DeadlineUtc $DeadlineUtc
    $content = if ($Stage -eq 'Primary' -or $mode -eq 'protocol-replay-malformed') { '{"schema":"wrong"}' } else { '{"schema":"dnppv2-model-review/v2","review_complete":true,"blocking_findings":[],"uncertainties":[]}' }
    if ($mode -eq 'adjudication-malformed') {
        if ($Stage -eq 'Primary') {
            $packetObject = $packetJson | ConvertFrom-Json -AsHashtable
            $finding = [ordered]@{ id='F-001'; severity='HIGH'; category='correctness'; requirement_quote='Verify'; path=$packetObject.material.source_path; evidence_path=$packetObject.material.source_path; evidence_quote='review purpose'; problem='Concrete defect'; required_outcome='Correct behavior' }
            $content = [Text.UTF8Encoding]::new($false).GetString((ConvertTo-CodeReviewJsonBytes -Value ([ordered]@{ schema='dnppv2-model-review/v2'; review_complete=$true; blocking_findings=@($finding); uncertainties=@() })))
        }
        else { $content = '{"schema":"wrong"}' }
    }
    $finish = if ($mode -eq 'protocol-length') { 'length' } else { 'stop' }
    $wire = ConvertTo-CodeReviewJsonBytes -Value ([ordered]@{ id='fixture'; object='chat.completion'; created=1; model=$Model; choices=@([ordered]@{ index=0; finish_reason=$finish; message=[ordered]@{role='assistant';content=$content} }) })
    $http = [pscustomobject]@{ Status=200; Bytes=$wire; SafeHeaders=@{} }
    $evidence = Save-NvidiaResponseEvidence -TransactionPath $TransactionPath -SelectedStage $Stage -SelectedModel $Model -PostAttemptOrdinal $attempt.Ordinal -CorrelationId $attempt.CorrelationId -HttpResult $http
    $null = Record-NvidiaResponseEvidence -TransactionPath $TransactionPath -SelectedStage $Stage -PostAttemptOrdinal $attempt.Ordinal -CorrelationId $attempt.CorrelationId -HttpResult $http -Evidence $evidence
    if ($mode -eq 'protocol-noncanonical') {
        $state = Read-CodeReviewTransaction -TransactionPath $TransactionPath
        $state.request_evidence[0].request_body_path = 'requests//post-0001.json'
        $null = Write-CodeReviewTransaction -TransactionPath $TransactionPath -State $state
    }
    if ($mode -eq 'protocol-linked') {
        $outside = [IO.Path]::Combine($RepositoryRoot,'outside-request')
        [IO.Directory]::CreateDirectory($outside) | Out-Null
        [IO.File]::WriteAllBytes([IO.Path]::Combine($outside,'request.json'),$requestBytes)
        $link = [IO.Path]::Combine([IO.Path]::GetDirectoryName($TransactionPath),'linked')
        if ($IsWindows) { $null = New-Item -ItemType Junction -Path $link -Target $outside }
        else { $null = [IO.Directory]::CreateSymbolicLink($link,$outside) }
        $state = Read-CodeReviewTransaction -TransactionPath $TransactionPath
        $state.request_evidence[0].request_body_path = 'linked/request.json'
        $null = Write-CodeReviewTransaction -TransactionPath $TransactionPath -State $state
    }
    if ($finish -ne 'stop') { throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Stubbed non-stop terminal finish.') }
    return [pscustomobject]@{ content=$content; provider_post_attempt_delta=1 }
}
if ($Stage -eq 'Primary' -and $mode -in @('fallback','fallback-protocol')) {
    throw (New-CodeReviewFailure -FailureClass 'PROVIDER_MODEL_BACKEND_FAILURE' -Message 'Stubbed explicit provider backend failure.')
}
if ($Stage -eq 'Fallback' -and $mode -eq 'fallback-protocol') {
    throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Stubbed fallback final-content failure.')
}
if ($Stage -eq 'Primary' -and $mode -eq 'rate-limit') {
    throw (New-CodeReviewFailure -FailureClass 'PROVIDER_RATE_LIMIT' -Message 'Stubbed explicit provider 429.')
}
if ($Stage -eq 'Primary' -and $mode -eq 'protocol') {
    throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Stubbed malformed final content.')
}
return [pscustomobject]@{ content='{"schema":"dnppv2-model-review/v2","review_complete":true,"blocking_findings":[],"uncertainties":[]}' }
'@
    [IO.File]::WriteAllText([IO.Path]::Combine($fixtureBuild,'Invoke-NvidiaReviewHarness.ps1'),$stub,[Text.UTF8Encoding]::new($false))
    . ([IO.Path]::Combine($PSScriptRoot,'CodeReviewerCommon.ps1'))
    $policy = [IO.Path]::Combine($PSScriptRoot,'Invoke-ReviewGate.ps1')
    $deadline = [DateTimeOffset]::UtcNow.AddMinutes(2)
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'mode.txt'),'clean',[Text.UTF8Encoding]::new($false))
    $primaryHealth = & $policy -Operation Health -HealthModelRole Primary -DeadlineUtc $deadline -RepositoryRoot $fixture
    $fallbackHealth = & $policy -Operation Health -HealthModelRole Fallback -DeadlineUtc $deadline -RepositoryRoot $fixture
    if ($primaryHealth.model -cne 'nvidia/nemotron-3-super-120b-a12b' -or
        $fallbackHealth.model -cne 'nvidia/nemotron-3.5-lightning-30b-a3b') { throw 'Health role model mapping failed.' }
    $packetBytes = ConvertTo-CodeReviewJsonBytes -Value ([ordered]@{
        schema='dnppv2-review-packet/v2'; review_type='CODE'; scope='TEST'; requirement='test'
        requirement_sha256=(Get-CodeReviewSha256 -Text 'test')
        candidate=[ordered]@{ base_sha=('a' * 40); head_sha=('b' * 40); base_tree=('d' * 40); head_tree=('e' * 40); snapshot_sha256=('c' * 64) }
        changed_files=@([ordered]@{path='sample.txt'}); context_files=@(); packet_policy=[ordered]@{}
    })
    $packetSha = Get-CodeReviewBytesSha256 -Bytes $packetBytes
    foreach ($mode in @('clean','fallback','rate-limit','protocol','fallback-protocol')) {
        [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'mode.txt'),$mode,[Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'calls.txt'),'',[Text.UTF8Encoding]::new($false))
        $prepared = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'review')) -ReviewType CODE -Scope TEST -BaseSha ('a' * 40) -HeadSha ('b' * 40) -SnapshotSha256 ('c' * 64) -PacketSha256 $packetSha -PacketBytes $packetBytes -DeadlineUtc $deadline
        $args = @{ Operation='Review'; ReviewType='CODE'; PacketPath=([IO.Path]::Combine($prepared.Directory,'packet.json')); TransactionPath=([IO.Path]::Combine($prepared.Directory,'transaction.json')); DeadlineUtc=$deadline; RepositoryRoot=$fixture; CandidateRepositoryRoot=$fixture }
        if ($mode -in @('rate-limit','protocol','fallback-protocol')) {
            $rejected = $false
            try { $null = & $policy @args }
            catch {
                $expectedClass = if ($mode -eq 'rate-limit') { 'PROVIDER_RATE_LIMIT' } else { 'MODEL_PROTOCOL_FAILURE' }
                $rejected = $_.Exception.Data['DNPPVFailureClass'] -ceq $expectedClass
                if (-not $rejected) { throw "Policy $mode failed as $($_.Exception.GetType().Name): $($_.Exception.Message)" }
            }
            if (-not $rejected) { throw "Policy $mode incorrectly triggered fallback or success." }
        }
        else {
            $result = & $policy @args
            if ($result.verdict -cne 'PASS' -or $result.review_stage_count -ne 1 -or
                $result.fallback_used -ne ($mode -eq 'fallback')) { throw "Policy $mode result is invalid." }
        }
        $calls = @([IO.File]::ReadAllLines([IO.Path]::Combine($fixture,'calls.txt')))
        $expectedCount = if ($mode -in @('fallback','fallback-protocol')) { 2 } else { 1 }
        if ($calls.Count -ne $expectedCount -or $calls[0] -cne 'Primary|nvidia/nemotron-3-super-120b-a12b' -or
            ($mode -in @('fallback','fallback-protocol') -and $calls[1] -cne 'Fallback|nvidia/nemotron-3.5-lightning-30b-a3b')) {
            throw "Policy $mode stage/model call trace is invalid."
        }
        if ($mode -in @('protocol','fallback-protocol')) {
            $auditPath = [IO.Path]::Combine($prepared.Directory,'packet-audit.json')
            if (-not [IO.File]::Exists($auditPath)) { throw 'Protocol failure did not persist a packet audit.' }
            $auditBytes = [IO.File]::ReadAllBytes($auditPath)
            $audit = [Text.UTF8Encoding]::new($false,$true).GetString($auditBytes) | ConvertFrom-Json -AsHashtable
            $state = Read-CodeReviewTransaction -TransactionPath $args.TransactionPath
            if ($mode -eq 'fallback-protocol' -and ($audit.stage -cne 'Fallback' -or $audit.model -cne 'nvidia/nemotron-3.5-lightning-30b-a3b')) {
                throw 'Fallback protocol failure audited the wrong model or stage.'
            }
            if ($state.state -cne 'FAILED_PROTOCOL' -or -not $state.packet_audit_complete -or
                $state.packet_audit_sha256 -cne (Get-CodeReviewBytesSha256 -Bytes $auditBytes) -or
                -not $audit.packet_hash_valid -or -not $audit.packet_schema_valid -or
                $audit.candidate_identity_valid -or $audit.request_contract_valid -or $audit.provider_envelope_valid) {
                throw 'Protocol packet audit did not preserve the exact incomplete-evidence diagnosis.'
            }
            if ($mode -eq 'fallback-protocol') { Write-Output 'REVIEW_POLICY_FALLBACK_PROTOCOL_AUDITED=Passed' }
        }
    }
    $materialPath = [IO.Path]::Combine($fixture,'purpose.txt')
    [IO.File]::WriteAllText($materialPath,"review purpose`n",[Text.UTF8Encoding]::new($false))
    $head = (& git -C $projectRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Fixture HEAD lookup failed.' }
    $material = New-CodeReviewMaterialPacketV1 -RepositoryRoot $projectRoot -ReviewType DOCUMENTATION -HeadSha $head -Scope TEST -Requirement 'Verify the stated document purpose.' -ReviewMaterialPath $materialPath
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'mode.txt'),'protocol-complete',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'calls.txt'),'',[Text.UTF8Encoding]::new($false))
    $prepared = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'review')) -ReviewType DOCUMENTATION -Scope TEST -HeadSha $head -SnapshotSha256 $material.SnapshotSha256 -PacketSha256 $material.PacketSha256 -PacketBytes $material.Bytes -DeadlineUtc $deadline
    $transactionPath = [IO.Path]::Combine($prepared.Directory,'transaction.json')
    $result = & $policy -Operation Review -ReviewType DOCUMENTATION -PacketPath ([IO.Path]::Combine($prepared.Directory,'packet.json')) -TransactionPath $transactionPath -DeadlineUtc $deadline -RepositoryRoot $fixture -CandidateRepositoryRoot $projectRoot
    $auditBytes = [IO.File]::ReadAllBytes([IO.Path]::Combine($prepared.Directory,'packet-audit.json'))
    $audit = [Text.UTF8Encoding]::new($false,$true).GetString($auditBytes) | ConvertFrom-Json -AsHashtable
    $state = Read-CodeReviewTransaction -TransactionPath $transactionPath
    $calls = @([IO.File]::ReadAllLines([IO.Path]::Combine($fixture,'calls.txt')))
    if ($result.verdict -cne 'PASS' -or $result.fallback_used -or $calls.Count -ne 2 -or
        $calls[0] -cne 'Primary|nvidia/nemotron-3-super-120b-a12b' -or
        $calls[1] -cne 'ProtocolReplay|nvidia/nemotron-3-super-120b-a12b' -or
        $state.packet_audit_sha256 -cne (Get-CodeReviewBytesSha256 -Bytes $auditBytes) -or
        @($state.request_evidence | Where-Object { $_.stage -ceq 'ProtocolReplay' }).Count -ne 1 -or
        $state.request_evidence[1].prior_final_content_sha256 -cne $audit.final_content_sha256) {
        throw 'Active policy same-model protocol replay contract failed.'
    }
    foreach ($name in @('packet_hash_valid','packet_schema_valid','candidate_identity_valid','request_json_valid','request_contract_valid','request_size_valid','secret_scan_valid','provider_envelope_valid','terminal_finish_valid','semantic_parser_replay_valid','audit_complete')) {
        if (-not $audit[$name]) { throw "Active policy packet audit field $name is false." }
    }
    Write-Output 'REVIEW_POLICY_ACTIVE_PROTOCOL_AUDIT_REPLAY=Passed'
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'calls.txt'),'',[Text.UTF8Encoding]::new($false))
    $tampered = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'review')) -ReviewType DOCUMENTATION -Scope TEST -HeadSha $head -SnapshotSha256 $material.SnapshotSha256 -PacketSha256 $material.PacketSha256 -PacketBytes $material.Bytes -DeadlineUtc $deadline
    [IO.File]::WriteAllText($materialPath,"changed after packet creation`n",[Text.UTF8Encoding]::new($false))
    $tamperedTransactionPath = [IO.Path]::Combine($tampered.Directory,'transaction.json')
    $tamperRejected = $false
    try {
        $null = & $policy -Operation Review -ReviewType DOCUMENTATION -PacketPath ([IO.Path]::Combine($tampered.Directory,'packet.json')) -TransactionPath $tamperedTransactionPath -DeadlineUtc $deadline -RepositoryRoot $fixture -CandidateRepositoryRoot $projectRoot
    }
    catch { $tamperRejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'MODEL_PROTOCOL_FAILURE' }
    $tamperAudit = [IO.File]::ReadAllText([IO.Path]::Combine($tampered.Directory,'packet-audit.json')) | ConvertFrom-Json -AsHashtable
    $tamperCalls = @([IO.File]::ReadAllLines([IO.Path]::Combine($fixture,'calls.txt')))
    if (-not $tamperRejected -or $tamperAudit.candidate_identity_valid -or $tamperCalls.Count -ne 1) {
        throw 'Changed review material incorrectly authorized protocol replay.'
    }
    Write-Output 'REVIEW_POLICY_CHANGED_MATERIAL_NO_REPLAY=Passed'
    [IO.File]::WriteAllText($materialPath,"review purpose`n",[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'mode.txt'),'protocol-length',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'calls.txt'),'',[Text.UTF8Encoding]::new($false))
    $truncated = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'review')) -ReviewType DOCUMENTATION -Scope TEST -HeadSha $head -SnapshotSha256 $material.SnapshotSha256 -PacketSha256 $material.PacketSha256 -PacketBytes $material.Bytes -DeadlineUtc $deadline
    $truncatedTransactionPath = [IO.Path]::Combine($truncated.Directory,'transaction.json')
    $lengthRejected = $false
    try {
        $null = & $policy -Operation Review -ReviewType DOCUMENTATION -PacketPath ([IO.Path]::Combine($truncated.Directory,'packet.json')) -TransactionPath $truncatedTransactionPath -DeadlineUtc $deadline -RepositoryRoot $fixture -CandidateRepositoryRoot $projectRoot
    }
    catch { $lengthRejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'MODEL_PROTOCOL_FAILURE' }
    $lengthAudit = [IO.File]::ReadAllText([IO.Path]::Combine($truncated.Directory,'packet-audit.json')) | ConvertFrom-Json -AsHashtable
    $lengthState = Read-CodeReviewTransaction -TransactionPath $truncatedTransactionPath
    $lengthCalls = @([IO.File]::ReadAllLines([IO.Path]::Combine($fixture,'calls.txt')))
    if (-not $lengthRejected -or $lengthAudit.terminal_finish_valid -or -not $lengthAudit.provider_envelope_valid -or
        $lengthCalls.Count -ne 1 -or $lengthState.response_evidence.Count -ne 1 -or
        -not [IO.File]::Exists([IO.Path]::Combine($truncated.Directory,$lengthState.response_evidence[0].final_content_path.Replace('/',[IO.Path]::DirectorySeparatorChar)))) {
        throw 'Truncated terminal output incorrectly replayed or lost evidence.'
    }
    Write-Output 'REVIEW_POLICY_NONSTOP_FINISH_NO_REPLAY=Passed'
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'mode.txt'),'protocol-noncanonical',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'calls.txt'),'',[Text.UTF8Encoding]::new($false))
    $noncanonical = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'review')) -ReviewType DOCUMENTATION -Scope TEST -HeadSha $head -SnapshotSha256 $material.SnapshotSha256 -PacketSha256 $material.PacketSha256 -PacketBytes $material.Bytes -DeadlineUtc $deadline
    $noncanonicalPath = [IO.Path]::Combine($noncanonical.Directory,'transaction.json')
    $noncanonicalRejected = $false
    try {
        $null = & $policy -Operation Review -ReviewType DOCUMENTATION -PacketPath ([IO.Path]::Combine($noncanonical.Directory,'packet.json')) -TransactionPath $noncanonicalPath -DeadlineUtc $deadline -RepositoryRoot $fixture -CandidateRepositoryRoot $projectRoot
    }
    catch { $noncanonicalRejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'MODEL_PROTOCOL_FAILURE' }
    $noncanonicalAudit = [IO.File]::ReadAllText([IO.Path]::Combine($noncanonical.Directory,'packet-audit.json')) | ConvertFrom-Json -AsHashtable
    $noncanonicalCalls = @([IO.File]::ReadAllLines([IO.Path]::Combine($fixture,'calls.txt')))
    if (-not $noncanonicalRejected -or $noncanonicalAudit.request_contract_valid -or $noncanonicalCalls.Count -ne 1) {
        throw 'Noncanonical request evidence path authorized an unchanged protocol replay.'
    }
    Write-Output 'REVIEW_POLICY_NONCANONICAL_EVIDENCE_NO_REPLAY=Passed'
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'mode.txt'),'protocol-linked',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'calls.txt'),'',[Text.UTF8Encoding]::new($false))
    $linked = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'review')) -ReviewType DOCUMENTATION -Scope TEST -HeadSha $head -SnapshotSha256 $material.SnapshotSha256 -PacketSha256 $material.PacketSha256 -PacketBytes $material.Bytes -DeadlineUtc $deadline
    $linkedPath = [IO.Path]::Combine($linked.Directory,'transaction.json')
    $linkedRejected = $false
    try { $null = & $policy -Operation Review -ReviewType DOCUMENTATION -PacketPath ([IO.Path]::Combine($linked.Directory,'packet.json')) -TransactionPath $linkedPath -DeadlineUtc $deadline -RepositoryRoot $fixture -CandidateRepositoryRoot $projectRoot }
    catch { $linkedRejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'MODEL_PROTOCOL_FAILURE' }
    $linkedAudit = [IO.File]::ReadAllText([IO.Path]::Combine($linked.Directory,'packet-audit.json')) | ConvertFrom-Json -AsHashtable
    $linkedCalls = @([IO.File]::ReadAllLines([IO.Path]::Combine($fixture,'calls.txt')))
    if (-not $linkedRejected -or $linkedAudit.request_contract_valid -or $linkedCalls.Count -ne 1) {
        throw 'Linked request evidence incorrectly authorized protocol replay.'
    }
    Write-Output 'REVIEW_POLICY_LINKED_EVIDENCE_NO_REPLAY=Passed'
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'mode.txt'),'protocol-complete',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'calls.txt'),'',[Text.UTF8Encoding]::new($false))
    $malformedPacket = [Text.UTF8Encoding]::new($false,$true).GetString($material.Bytes) | ConvertFrom-Json -AsHashtable
    $malformedPacket.Remove('requirement')
    $malformedBytes = ConvertTo-CodeReviewJsonBytes -Value $malformedPacket
    $malformedSha = Get-CodeReviewBytesSha256 -Bytes $malformedBytes
    $malformed = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'review')) -ReviewType DOCUMENTATION -Scope TEST -HeadSha $head -SnapshotSha256 $material.SnapshotSha256 -PacketSha256 $malformedSha -PacketBytes $malformedBytes -DeadlineUtc $deadline
    $malformedPath = [IO.Path]::Combine($malformed.Directory,'transaction.json')
    $malformedRejected = $false
    try {
        $null = & $policy -Operation Review -ReviewType DOCUMENTATION -PacketPath ([IO.Path]::Combine($malformed.Directory,'packet.json')) -TransactionPath $malformedPath -DeadlineUtc $deadline -RepositoryRoot $fixture -CandidateRepositoryRoot $projectRoot
    }
    catch { $malformedRejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'MODEL_PROTOCOL_FAILURE' }
    $malformedAuditPath = [IO.Path]::Combine($malformed.Directory,'packet-audit.json')
    if (-not [IO.File]::Exists($malformedAuditPath)) { throw 'Malformed packet did not produce the mandatory audit record.' }
    $malformedAudit = [IO.File]::ReadAllText($malformedAuditPath) | ConvertFrom-Json -AsHashtable
    $malformedCalls = @([IO.File]::ReadAllLines([IO.Path]::Combine($fixture,'calls.txt')))
    if (-not $malformedRejected -or $malformedAudit.packet_schema_valid -or $malformedCalls.Count -ne 1) {
        throw 'Malformed packet schema incorrectly authorized a protocol replay.'
    }
    Write-Output 'REVIEW_POLICY_MALFORMED_PACKET_AUDITED_NO_REPLAY=Passed'
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'mode.txt'),'adjudication-malformed',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'calls.txt'),'',[Text.UTF8Encoding]::new($false))
    $adjudicationPrepared = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'review')) -ReviewType DOCUMENTATION -Scope TEST -HeadSha $head -SnapshotSha256 $material.SnapshotSha256 -PacketSha256 $material.PacketSha256 -PacketBytes $material.Bytes -DeadlineUtc $deadline
    $adjudicationPath = [IO.Path]::Combine($adjudicationPrepared.Directory,'transaction.json')
    $adjudicationResult = & $policy -Operation Review -ReviewType DOCUMENTATION -PacketPath ([IO.Path]::Combine($adjudicationPrepared.Directory,'packet.json')) -TransactionPath $adjudicationPath -DeadlineUtc $deadline -RepositoryRoot $fixture -CandidateRepositoryRoot $projectRoot
    $adjudicationAuditPath = [IO.Path]::Combine($adjudicationPrepared.Directory,'packet-audit-adjudication.json')
    if (-not [IO.File]::Exists($adjudicationAuditPath)) { throw 'Malformed adjudication did not persist a packet audit.' }
    $adjudicationAudit = [IO.File]::ReadAllText($adjudicationAuditPath) | ConvertFrom-Json -AsHashtable
    $adjudicationCalls = @([IO.File]::ReadAllLines([IO.Path]::Combine($fixture,'calls.txt')))
    if ($adjudicationResult.verdict -cne 'INCONCLUSIVE' -or $adjudicationAudit.stage -cne 'Adjudication' -or
        $adjudicationCalls.Count -ne 2 -or -not $adjudicationAudit.packet_hash_valid -or
        -not $adjudicationAudit.request_contract_valid -or -not $adjudicationAudit.provider_envelope_valid -or
        -not $adjudicationAudit.semantic_parser_replay_valid) {
        throw 'Malformed adjudication was not audited before INCONCLUSIVE.'
    }
    Write-Output 'REVIEW_POLICY_ADJUDICATION_PROTOCOL_AUDITED=Passed'
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'mode.txt'),'protocol-replay-malformed',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText([IO.Path]::Combine($fixture,'calls.txt'),'',[Text.UTF8Encoding]::new($false))
    $replayPrepared = New-CodeReviewPreparedTransaction -OutputRoot ([IO.Path]::Combine($fixture,'review')) -ReviewType DOCUMENTATION -Scope TEST -HeadSha $head -SnapshotSha256 $material.SnapshotSha256 -PacketSha256 $material.PacketSha256 -PacketBytes $material.Bytes -DeadlineUtc $deadline
    $replayTransactionPath = [IO.Path]::Combine($replayPrepared.Directory,'transaction.json')
    $replayResult = & $policy -Operation Review -ReviewType DOCUMENTATION -PacketPath ([IO.Path]::Combine($replayPrepared.Directory,'packet.json')) -TransactionPath $replayTransactionPath -DeadlineUtc $deadline -RepositoryRoot $fixture -CandidateRepositoryRoot $projectRoot
    $initialAuditPath = [IO.Path]::Combine($replayPrepared.Directory,'packet-audit.json')
    $finalAuditPath = [IO.Path]::Combine($replayPrepared.Directory,'packet-audit-protocol-replay.json')
    $finalAudit = [IO.File]::ReadAllText($finalAuditPath) | ConvertFrom-Json -AsHashtable
    $replayState = Read-CodeReviewTransaction -TransactionPath $replayTransactionPath
    $replayCalls = @([IO.File]::ReadAllLines([IO.Path]::Combine($fixture,'calls.txt')))
    if ($replayResult.verdict -cne 'INCONCLUSIVE' -or $replayResult.review_complete -or
        $replayResult.review_stage_count -ne 0 -or $replayResult.semantic_content_sha256s.Count -ne 0 -or
        $replayCalls.Count -ne 2 -or -not [IO.File]::Exists($initialAuditPath) -or
        $finalAudit.stage -cne 'ProtocolReplay' -or -not $finalAudit.provider_envelope_valid -or
        $replayState.packet_audit_path -cne 'packet-audit-protocol-replay.json') {
        throw 'Second malformed same-model response did not end with retained two-attempt INCONCLUSIVE evidence.'
    }
    Write-Output 'REVIEW_POLICY_SECOND_PROTOCOL_FAILURE_INCONCLUSIVE=Passed'
    $codeRoot = [IO.Path]::Combine($fixture,'code-candidate')
    $codeBuild = [IO.Path]::Combine($codeRoot,'build')
    [IO.Directory]::CreateDirectory($codeBuild) | Out-Null
    $utf8 = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText([IO.Path]::Combine($codeRoot,'.gitignore'),"build/`nmode.txt`ncalls.txt`n",$utf8)
    [IO.File]::WriteAllText([IO.Path]::Combine($codeRoot,'sample.txt'),"before`n",$utf8)
    & git -C $codeRoot init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'CODE replay fixture init failed.' }
    & git -C $codeRoot config core.autocrlf false
    & git -C $codeRoot config user.name 'Review Policy Test'
    & git -C $codeRoot config user.email 'review-policy@example.invalid'
    & git -C $codeRoot add -- .gitignore sample.txt
    & git -C $codeRoot commit --quiet -m base
    if ($LASTEXITCODE -ne 0) { throw 'CODE replay fixture base commit failed.' }
    $codeBase = (& git -C $codeRoot rev-parse HEAD).Trim()
    [IO.File]::WriteAllText([IO.Path]::Combine($codeRoot,'sample.txt'),"after`n",$utf8)
    & git -C $codeRoot add -- sample.txt
    & git -C $codeRoot commit --quiet -m head
    if ($LASTEXITCODE -ne 0) { throw 'CODE replay fixture head commit failed.' }
    $codeHead = (& git -C $codeRoot rev-parse HEAD).Trim()
    foreach ($name in @('CodeReviewerCommon.ps1','Invoke-CodeReviewHarness.ps1','Invoke-ReviewGate.ps1','Assert-CodeReviewReceipt.ps1')) {
        [IO.File]::Copy([IO.Path]::Combine($PSScriptRoot,$name),[IO.Path]::Combine($codeBuild,$name))
    }
    [IO.File]::Copy([IO.Path]::Combine($PSScriptRoot,'Invoke-NvidiaReviewHarness.ps1'),[IO.Path]::Combine($codeBuild,'RealNvidiaAdapter.ps1'))
    [IO.File]::WriteAllText([IO.Path]::Combine($codeBuild,'Invoke-NvidiaReviewHarness.ps1'),$stub,$utf8)
    [IO.File]::WriteAllText([IO.Path]::Combine($codeRoot,'mode.txt'),'protocol-complete',$utf8)
    [IO.File]::WriteAllText([IO.Path]::Combine($codeRoot,'calls.txt'),'',$utf8)
    $codeResult = & ([IO.Path]::Combine($codeBuild,'Invoke-CodeReviewHarness.ps1')) -Operation Review -ReviewType CODE -RepositoryRoot $codeRoot -BaseSha $codeBase -HeadSha $codeHead -Scope CODE-REPLAY -Requirement 'Verify changed sample behavior.' -ReviewTimeoutSeconds 120
    if ($codeResult.verdict -cne 'PASS' -or $codeResult.provider_post_attempt_count -ne 2) { throw 'CODE replay fixture did not complete PASS with two attempts.' }
    $codeTransactionRoot = [IO.Path]::Combine($codeRoot,'build','code-review','transactions',$codeHead,$codeResult.packet_sha256,$codeResult.transaction_id)
    $codeTransactionPath = [IO.Path]::Combine($codeTransactionRoot,'transaction.json')
    $codeResultPath = [IO.Path]::Combine($codeTransactionRoot,'result.json')
    $receiptScript = [IO.Path]::Combine($codeBuild,'Assert-CodeReviewReceipt.ps1')
    $receipt = & $receiptScript -Operation Create -RepositoryRoot $codeRoot -BaseSha $codeBase -NewSha $codeHead -Scope CODE-REPLAY -ResultPath $codeResultPath
    if (-not $receipt.receipt_sha256) { throw 'Valid CODE replay did not create a receipt.' }
    $validatedReceipt = & $receiptScript -Operation Validate -RepositoryRoot $codeRoot -BaseSha $codeBase -NewSha $codeHead -RemoteOldSha $codeBase
    if (-not $validatedReceipt.valid) { throw 'Valid CODE replay receipt did not validate.' }
    $tamperedState = [IO.File]::ReadAllText($codeTransactionPath) | ConvertFrom-Json -AsHashtable
    $replayEntry = @($tamperedState.request_evidence | Where-Object { $_.stage -ceq 'ProtocolReplay' })
    if ($replayEntry.Count -ne 1) { throw 'CODE replay request lineage is missing.' }
    $replayEntry[0].prior_final_content_sha256 = 'f' * 64
    $null = Write-CodeReviewAtomicJson -Path $codeTransactionPath -Value $tamperedState
    $tamperedRejected = $false
    try { $null = & $receiptScript -Operation Create -RepositoryRoot $codeRoot -BaseSha $codeBase -NewSha $codeHead -Scope CODE-REPLAY -ResultPath $codeResultPath }
    catch { $tamperedRejected = $_.Exception.Data['DNPPVFailureClass'] -ceq 'RECEIPT_VALIDATION_FAILURE' -and $_.Exception.Message -match 'Replay request, audit, and prior response' }
    if (-not $tamperedRejected) { throw 'Tampered CODE replay lineage was not rejected before receipt creation.' }
    Write-Output 'REVIEW_POLICY_CODE_REPLAY_RECEIPT_LINEAGE=Passed'
    Write-Output 'REVIEW_POLICY_ACTIVE_ROUTING=Passed'
}
finally {
    $target = [IO.Path]::GetFullPath($fixture)
    $parent = [IO.Path]::GetFullPath($testParent)
    if (-not $target.StartsWith($parent + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($target) -notmatch '^[0-9a-f]{32}$') { throw 'Refusing policy fixture cleanup outside exact test root.' }
    if ([IO.Directory]::Exists($target)) { Remove-Item -LiteralPath $target -Recurse -Force }
}
