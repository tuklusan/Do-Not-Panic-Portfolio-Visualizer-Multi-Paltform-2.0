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

function Get-CodeReviewSha256 {
    param([Parameter(Mandatory)][string]$Text)
    $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
    return ([Security.Cryptography.SHA256]::HashData($bytes) | ForEach-Object ToString x2) -join ''
}

function Get-CodeReviewBytesSha256 {
    param([Parameter(Mandatory)][byte[]]$Bytes)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function New-CodeReviewFailure {
    param([Parameter(Mandatory)][string]$FailureClass, [Parameter(Mandatory)][string]$Message)
    $exception = [InvalidOperationException]::new($Message)
    $exception.Data['DNPPVFailureClass'] = $FailureClass
    return $exception
}

function Write-CodeReviewAtomicJson {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Value)
    $bytes = ConvertTo-CodeReviewJsonBytes -Value $Value
    Write-CodeReviewAtomicBytes -Path $Path -Bytes $bytes
    return Get-CodeReviewBytesSha256 -Bytes $bytes
}

function Enter-CodeReviewLease {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [ValidateSet('Review','Health')][string]$Operation = 'Review',
        [string]$TransactionId,
        [string]$ReviewType,
        [string]$HealthModelRole,
        [string]$BaseSha,
        [string]$HeadSha,
        [string]$PacketSha256,
        [Parameter(Mandatory)][DateTimeOffset]$DeadlineUtc
    )
    $reviewRoot = [IO.Path]::Combine($RepositoryRoot, 'build', 'code-review')
    [IO.Directory]::CreateDirectory($reviewRoot) | Out-Null
    $lockPath = [IO.Path]::Combine($reviewRoot, 'active-review.lock')
    $ownerPath = [IO.Path]::Combine($reviewRoot, 'active-review.json')
    try {
        $stream = [IO.FileStream]::new($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    }
    catch [IO.IOException] {
        $owner = $null
        $ownerStartText = $null
        $ownerDeadlineText = $null
        if ([IO.File]::Exists($ownerPath)) {
            try {
                $ownerJson = [IO.File]::ReadAllText($ownerPath)
                $owner = $ownerJson | ConvertFrom-Json -ErrorAction Stop
                $ownerDocument = [Text.Json.JsonDocument]::Parse($ownerJson)
                try {
                    $ownerStartText = $ownerDocument.RootElement.GetProperty('process_start_utc').GetString()
                    $ownerDeadlineText = $ownerDocument.RootElement.GetProperty('deadline_utc').GetString()
                }
                finally { $ownerDocument.Dispose() }
            }
            catch { $owner = $null }
        }
        $detail = if ($owner) { 'review lease owned by PID {0}, transaction {1}, head {2}, deadline {3}' -f $owner.pid,$owner.transaction_id,$owner.head_sha,$owner.deadline_utc }
                  else { 'review lease is already owned' }
        $failureClass = 'LOCAL_CONCURRENCY_BUSY'
        if ($owner -and $owner.pid -match '^[0-9]+$' -and $ownerStartText -and $ownerDeadlineText) {
            $recordedStart = [DateTimeOffset]::MinValue
            $recordedDeadline = [DateTimeOffset]::MinValue
            if ([DateTimeOffset]::TryParse($ownerStartText, [ref]$recordedStart) -and
                [DateTimeOffset]::TryParse($ownerDeadlineText, [ref]$recordedDeadline) -and
                $recordedDeadline -lt [DateTimeOffset]::UtcNow) {
                try {
                    $ownerProcess = [Diagnostics.Process]::GetProcessById([int]$owner.pid)
                    try {
                        $actualStart = [DateTimeOffset]$ownerProcess.StartTime.ToUniversalTime()
                        if (-not $ownerProcess.HasExited -and [Math]::Abs(($actualStart - $recordedStart).TotalSeconds) -lt 1) {
                            $failureClass = 'LOCAL_CONCURRENCY_STALE_OWNER'
                        }
                    }
                    finally { $ownerProcess.Dispose() }
                }
                catch [ArgumentException] { }
            }
        }
        throw (New-CodeReviewFailure -FailureClass $failureClass -Message $detail)
    }
    try {
        $currentProcess = [Diagnostics.Process]::GetCurrentProcess()
        try { $startUtc = $currentProcess.StartTime.ToUniversalTime().ToString('o') }
        finally { $currentProcess.Dispose() }
        $owner = [ordered]@{
            schema = 'dnppv2-review-lease/v1'
            operation = $Operation
            pid = $PID
            process_start_utc = $startUtc
            transaction_id = $(if ($Operation -eq 'Review') { $TransactionId } else { $null })
            review_type = $(if ($Operation -eq 'Review') { $ReviewType } else { $null })
            health_model_role = $(if ($Operation -eq 'Health') { $HealthModelRole } else { $null })
            base_sha = $(if ($Operation -eq 'Review') { $BaseSha } else { $null })
            head_sha = $(if ($Operation -eq 'Review') { $HeadSha } else { $null })
            packet_sha256 = $(if ($Operation -eq 'Review') { $PacketSha256 } else { $null })
            acquired_utc = [DateTimeOffset]::UtcNow.ToString('o')
            deadline_utc = $DeadlineUtc.ToString('o')
        }
        $null = Write-CodeReviewAtomicJson -Path $ownerPath -Value $owner
        return [pscustomobject]@{ Stream = $stream; OwnerPath = $ownerPath }
    }
    catch { $stream.Dispose(); throw }
}

function Exit-CodeReviewLease {
    param([Parameter(Mandatory)]$Lease)
    try {
        if ($Lease.Stream -and $Lease.Stream.CanWrite -and [IO.File]::Exists($Lease.OwnerPath)) {
            [IO.File]::Delete($Lease.OwnerPath)
        }
    }
    finally { if ($Lease.Stream) { $Lease.Stream.Dispose() } }
}

function New-CodeReviewPreparedTransaction {
    param(
        [Parameter(Mandatory)][string]$OutputRoot,
        [Parameter(Mandatory)][ValidateSet('CODE','DOCUMENTATION','TEST_ARTIFACT')][string]$ReviewType,
        [Parameter(Mandatory)][string]$Scope,
        [string]$BaseSha,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$SnapshotSha256,
        [Parameter(Mandatory)][string]$PacketSha256,
        [Parameter(Mandatory)][byte[]]$PacketBytes,
        [Parameter(Mandatory)][DateTimeOffset]$DeadlineUtc
    )
    if ($ReviewType -eq 'CODE' -and $BaseSha -notmatch '^[0-9a-fA-F]{40}$') {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_INPUT_FAILURE' -Message 'CODE transaction requires a base SHA.')
    }
    if ($HeadSha -notmatch '^[0-9a-fA-F]{40}$' -or $PacketSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        $SnapshotSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        (Get-CodeReviewBytesSha256 -Bytes $PacketBytes) -cne $PacketSha256.ToLowerInvariant()) {
        throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'Transaction packet/candidate identity invalid.')
    }
    $id = [guid]::NewGuid().ToString('N')
    $directory = [IO.Path]::Combine($OutputRoot,'transactions',$HeadSha.ToLowerInvariant(),$PacketSha256.ToLowerInvariant(),$id)
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $now = [DateTimeOffset]::UtcNow.ToString('o')
    $transaction = [ordered]@{
        schema = 'dnppv2-review-transaction/v1'
        transaction_id = $id
        review_type = $ReviewType
        scope = $Scope
        base_sha = $(if ($ReviewType -eq 'CODE') { $BaseSha.ToLowerInvariant() } else { $null })
        head_sha = $HeadSha.ToLowerInvariant()
        snapshot_sha256 = $SnapshotSha256.ToLowerInvariant()
        packet_sha256 = $PacketSha256.ToLowerInvariant()
        state = 'PREPARED'
        started_utc = $now
        deadline_utc = $DeadlineUtc.ToString('o')
        active_model = $null
        active_attempt = $null
        provider_request_id = $null
        correlation_id = $null
        last_response_wire_sha256 = $null
        provider_post_attempt_count = 0
        models_attempted = @()
        request_evidence = @()
        response_evidence = @()
        result_sha256 = $null
        failure_class = $null
        failure_detail = $null
        packet_audit_complete = $false
        packet_audit_sha256 = $null
        packet_audit_path = $null
        last_transition_utc = $now
    }
    Write-CodeReviewAtomicBytes -Path ([IO.Path]::Combine($directory,'packet.json')) -Bytes $PacketBytes
    $null = Write-CodeReviewAtomicJson -Path ([IO.Path]::Combine($directory,'transaction.json')) -Value $transaction
    return [pscustomobject]@{ Directory = $directory; Transaction = $transaction }
}

function Read-CodeReviewTransaction {
    param([Parameter(Mandatory)][string]$TransactionPath)
    if (-not [IO.File]::Exists($TransactionPath)) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Transaction state file is missing.')
    }
    try {
        $json = [IO.File]::ReadAllText($TransactionPath, [Text.UTF8Encoding]::new($false,$true))
        $state = $json | ConvertFrom-Json -AsHashtable -ErrorAction Stop
        $document = [Text.Json.JsonDocument]::Parse($json)
        try { $state.deadline_utc = $document.RootElement.GetProperty('deadline_utc').GetString() }
        finally { $document.Dispose() }
    }
    catch { throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Transaction state JSON is invalid.') }
    if ($state.schema -cne 'dnppv2-review-transaction/v1' -or -not $state.transaction_id) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Transaction state schema is invalid.')
    }
    return $state
}

function Write-CodeReviewTransaction {
    param([Parameter(Mandatory)][string]$TransactionPath, [Parameter(Mandatory)]$State)
    if ([IO.File]::Exists($TransactionPath)) {
        $previous = Read-CodeReviewTransaction -TransactionPath $TransactionPath
        if ($previous.state -like 'COMPLETED_*') {
            throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Completed transaction is immutable.')
        }
    }
    $State.last_transition_utc = [DateTimeOffset]::UtcNow.ToString('o')
    return Write-CodeReviewAtomicJson -Path $TransactionPath -Value $State
}

function Test-CodeReviewProtocolReplayAuthorization {
    param([Parameter(Mandatory)][string]$TransactionPath,[Parameter(Mandatory)]$Transaction,[Parameter(Mandatory)][string]$Model)
    if (-not $Transaction.packet_audit_complete -or
        $Transaction.packet_audit_sha256 -notmatch '^[0-9a-f]{64}$' -or
        $Transaction.packet_audit_path -cne 'packet-audit.json' -or
        @($Transaction.request_evidence | Where-Object { $_.stage -ceq 'ProtocolReplay' }).Count -ne 0) { return $false }
    $auditPath = [IO.Path]::Combine([IO.Path]::GetDirectoryName($TransactionPath),'packet-audit.json')
    if (-not [IO.File]::Exists($auditPath)) { return $false }
    $bytes = [IO.File]::ReadAllBytes($auditPath)
    if ((Get-CodeReviewBytesSha256 -Bytes $bytes) -cne $Transaction.packet_audit_sha256) { return $false }
    try { $audit = [Text.UTF8Encoding]::new($false,$true).GetString($bytes) | ConvertFrom-Json -AsHashtable -ErrorAction Stop }
    catch { return $false }
    if ($audit.schema -cne 'dnppv2-packet-audit/v1' -or
        $audit.transaction_id -cne $Transaction.transaction_id -or
        $audit.packet_sha256 -cne $Transaction.packet_sha256 -or
        $audit.model -cne $Model -or $audit.stage -notin @('Primary','Fallback')) { return $false }
    $auditedRequest = @($Transaction.request_evidence | Where-Object { $_.stage -ceq $audit.stage -and $_.model -ceq $Model } | Select-Object -Last 1)
    $auditedResponse = @($Transaction.response_evidence | Where-Object { $_.stage -ceq $audit.stage -and $_.http_status -eq 200 } | Select-Object -Last 1)
    if ($auditedRequest.Count -ne 1 -or $auditedResponse.Count -ne 1 -or
        $audit.request_body_sha256 -cne $auditedRequest[0].request_body_sha256 -or
        $audit.response_wire_sha256 -cne $auditedResponse[0].wire_sha256 -or
        $audit.final_content_sha256 -cne $auditedResponse[0].final_content_sha256) { return $false }
    foreach ($name in @('packet_hash_valid','packet_schema_valid','candidate_identity_valid','request_json_valid',
        'request_contract_valid','request_size_valid','secret_scan_valid','provider_envelope_valid',
        'terminal_finish_valid','semantic_parser_replay_valid','audit_complete')) {
        if ($audit[$name] -isnot [bool] -or -not $audit[$name]) { return $false }
    }
    return $true
}

function Start-CodeReviewPostAttempt {
    param(
        [Parameter(Mandatory)][string]$TransactionPath,
        [Parameter(Mandatory)][ValidateSet('Primary','Fallback','Adjudication','ProtocolReplay')][string]$Stage,
        [Parameter(Mandatory)][string]$Model,
        [Parameter(Mandatory)][byte[]]$RequestBytes,
        [Parameter(Mandatory)][string]$SafeEndpoint,
        [Parameter(Mandatory)][DateTimeOffset]$DeadlineUtc
    )
    $transaction = Read-CodeReviewTransaction -TransactionPath $TransactionPath
    if ($transaction.state -like 'COMPLETED_*' -or $transaction.state -like '*_DISPATCHING' -or
        $transaction.state -like '*_PENDING' -or
        $transaction.state -eq 'AMBIGUOUS_DISPATCH') {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Transaction state does not permit a new POST.')
    }
    if ($transaction.state -like 'FAILED_*') {
        $fallbackAllowed = $Stage -eq 'Fallback' -and
            $transaction.failure_class -in @('PROVIDER_MODEL_UNAVAILABLE','PROVIDER_MODEL_BACKEND_FAILURE') -and
            @($transaction.request_evidence | Where-Object { $_.stage -eq 'Fallback' }).Count -eq 0
        $replayAllowed = $Stage -eq 'ProtocolReplay' -and $transaction.state -eq 'FAILED_PROTOCOL' -and
            (Test-CodeReviewProtocolReplayAuthorization -TransactionPath $TransactionPath -Transaction $transaction -Model $Model)
        if (-not ($fallbackAllowed -or $replayAllowed)) {
            throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Terminal failure does not authorize another POST.')
        }
    }
    $ordinal = [int]$transaction.provider_post_attempt_count + 1
    $relativeRequest = 'requests/post-{0:D4}.json' -f $ordinal
    $relativeResponse = 'responses/post-{0:D4}/response-meta.json' -f $ordinal
    $directory = [IO.Path]::GetDirectoryName($TransactionPath)
    $requestPath = [IO.Path]::Combine($directory,$relativeRequest.Replace('/',[IO.Path]::DirectorySeparatorChar))
    if ([IO.File]::Exists($requestPath)) {
        throw (New-CodeReviewFailure -FailureClass 'LOCAL_PERSISTENCE_FAILURE' -Message 'Request attempt file already exists.')
    }
    Write-CodeReviewAtomicBytes -Path $requestPath -Bytes $RequestBytes
    $correlation = [guid]::NewGuid().ToString('D')
    $now = [DateTimeOffset]::UtcNow.ToString('o')
    $requestEntry = [ordered]@{
        stage = $Stage
        post_attempt_ordinal = $ordinal
        model = $Model
        correlation_id = $correlation
        request_body_path = $relativeRequest
        request_body_sha256 = Get-CodeReviewBytesSha256 -Bytes $RequestBytes
        request_body_byte_length = $RequestBytes.Length
        expected_response_meta_path = $relativeResponse
        endpoint = $SafeEndpoint
        started_utc = $now
        deadline_utc = $DeadlineUtc.ToString('o')
    }
    if ($Stage -eq 'ProtocolReplay') {
        $auditBytes = [IO.File]::ReadAllBytes([IO.Path]::Combine($directory,'packet-audit.json'))
        $audit = [Text.UTF8Encoding]::new($false,$true).GetString($auditBytes) | ConvertFrom-Json -AsHashtable -ErrorAction Stop
        $requestEntry.protocol_replay_of_stage = $audit.stage
        $requestEntry.prior_final_content_sha256 = $audit.final_content_sha256
        $requestEntry.prior_response_wire_sha256 = $audit.response_wire_sha256
        $requestEntry.packet_audit_sha256 = $transaction.packet_audit_sha256
    }
    $transaction.state = $Stage.ToUpperInvariant() + '_DISPATCHING'
    $transaction.active_model = $Model
    $transaction.active_attempt = $ordinal
    $transaction.correlation_id = $correlation
    $transaction.provider_post_attempt_count = $ordinal
    $transaction.models_attempted = @($transaction.models_attempted) + @($Model)
    $transaction.request_evidence = @($transaction.request_evidence) + @($requestEntry)
    $null = Write-CodeReviewTransaction -TransactionPath $TransactionPath -State $transaction
    return [pscustomobject]@{
        Transaction = $transaction
        Ordinal = $ordinal
        CorrelationId = $correlation
        RequestPath = $requestPath
        ExpectedResponseMetaPath = [IO.Path]::Combine($directory,$relativeResponse.Replace('/',[IO.Path]::DirectorySeparatorChar))
    }
}

function Assert-CodeReviewExactKeys {
    param([Parameter(Mandatory)]$Object,[Parameter(Mandatory)][string[]]$Names)
    if ($Object -isnot [Collections.IDictionary] -or $Object.Count -ne $Names.Count) {
        throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Semantic object property count is invalid.')
    }
    foreach ($name in $Names) {
        if (-not $Object.Contains($name)) {
            throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Semantic object property set is invalid.')
        }
    }
}

function Test-CodeReviewExactQuote {
    param([string]$Haystack,[string]$Needle)
    return -not [string]::IsNullOrEmpty($Needle) -and $null -ne $Haystack -and
        $Haystack.Contains($Needle,[StringComparison]::Ordinal)
}

function Assert-CodeReviewNoLikelySecrets {
    param([Parameter(Mandatory)][string]$Text)
    $patterns = @(
        '(?im)(api[_-]?key|secret|token|password)\s*[:=]\s*[''"](?!(test|example|placeholder|dummy|sample|REPLACE_WITH_))([A-Za-z0-9_\-+/=]{16,})[''"]',
        '(?im)(?:export\s+|set\s+)?(api[_-]?key|secret|token|password)\s*[:=]\s*(sk-[A-Za-z0-9_-]{20,}|[A-Za-z0-9_\-+/=]{32,})',
        '(?m)nvapi-[A-Za-z0-9_-]{20,}',
        '(?im)sk-(?!test|example|placeholder|dummy|sample)[A-Za-z0-9_-]{20,}',
        '(?m)(AKIA|ASIA)[0-9A-Z]{16}',
        '(?m)AIza[0-9A-Za-z\-_]{35}',
        '(?m)ghp_[A-Za-z0-9_]{30,}',
        '(?m)eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}',
        '(?im)[a-z][a-z0-9+.-]{2,}://[^/\s:@]{2,}:[^/\s:@]{8,}@',
        '(?s)-----BEGIN [A-Z ]*PRIVATE KEY-----.*?-----END [A-Z ]*PRIVATE KEY-----'
    )
    for ($patternIndex = 0; $patternIndex -lt $patterns.Count; $patternIndex++) {
        $pattern = $patterns[$patternIndex]
        if ([regex]::IsMatch($Text,$pattern)) {
            throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message "Review packet contains likely secret material (pattern $patternIndex).")
        }
    }
}

function Test-CodeReviewEvidenceQuote {
    param([Parameter(Mandatory)]$Packet,[string]$Path,[string]$Quote)
    if ([string]::IsNullOrEmpty($Quote) -or [string]::IsNullOrEmpty($Path)) { return $false }
    if ($Packet.review_type -ne 'CODE') {
        return $Packet.material.source_path -ceq $Path -and (Test-CodeReviewExactQuote -Haystack $Packet.material.content -Needle $Quote)
    }
    $changed = @($Packet.changed_files | Where-Object { $_.path -ceq $Path })
    if ($changed.Count -eq 1) {
        return (Test-CodeReviewExactQuote -Haystack $changed[0].zero_context_diff -Needle $Quote) -or
            (Test-CodeReviewExactQuote -Haystack $changed[0].review_context -Needle $Quote)
    }
    $context = @($Packet.context_files | Where-Object { $_.path -ceq $Path })
    if ($context.Count -eq 1) {
        if ($context[0].context_kind -eq 'full-head') {
            return Test-CodeReviewExactQuote -Haystack $context[0].content -Needle $Quote
        }
        foreach ($range in $context[0].ranges) {
            if (Test-CodeReviewExactQuote -Haystack $range.content -Needle $Quote) { return $true }
        }
    }
    return $false
}

function ConvertFrom-CodeReviewModelContent {
    param([Parameter(Mandatory)][string]$Content,[Parameter(Mandatory)]$Packet)
    try { $result = ConvertFrom-Json -InputObject $Content -AsHashtable -ErrorAction Stop }
    catch { throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Model content is not JSON.') }
    Assert-CodeReviewExactKeys -Object $result -Names @('schema','review_complete','blocking_findings','uncertainties')
    if ($result.schema -cne 'dnppv2-model-review/v2' -or $result.review_complete -isnot [bool] -or
        $result.blocking_findings -isnot [array] -or $result.uncertainties -isnot [array]) {
        throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Model review schema is invalid.')
    }
    $changedPaths = if ($Packet.review_type -eq 'CODE') { @($Packet.changed_files.path) } else { @($Packet.material.source_path) }
    $findingIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($finding in $result.blocking_findings) {
        Assert-CodeReviewExactKeys -Object $finding -Names @('id','severity','category','requirement_quote','path','evidence_path','evidence_quote','problem','required_outcome')
        if ($finding.id -isnot [string] -or $finding.id -cnotmatch '^F-[0-9]{3,}$' -or
            -not $findingIds.Add($finding.id) -or $finding.severity -cnotin @('BLOCKER','HIGH') -or
            $finding.path -cnotin $changedPaths -or
            -not (Test-CodeReviewExactQuote -Haystack $Packet.requirement -Needle $finding.requirement_quote) -or
            -not (Test-CodeReviewEvidenceQuote -Packet $Packet -Path $finding.evidence_path -Quote $finding.evidence_quote) -or
            [string]::IsNullOrWhiteSpace($finding.category) -or [string]::IsNullOrWhiteSpace($finding.problem) -or
            [string]::IsNullOrWhiteSpace($finding.required_outcome)) {
            throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Model finding is not grounded in packet authority.')
        }
    }
    $uncertaintyIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($uncertainty in $result.uncertainties) {
        Assert-CodeReviewExactKeys -Object $uncertainty -Names @('id','path','reason','required_evidence')
        if ($uncertainty.id -isnot [string] -or $uncertainty.id -cnotmatch '^U-[0-9]{3,}$' -or
            -not $uncertaintyIds.Add($uncertainty.id) -or
            ($null -ne $uncertainty.path -and $uncertainty.path -cnotin $changedPaths) -or
            [string]::IsNullOrWhiteSpace($uncertainty.reason) -or
            [string]::IsNullOrWhiteSpace($uncertainty.required_evidence)) {
            throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Model uncertainty is invalid.')
        }
    }
    return $result
}

function ConvertFrom-CodeReviewAdjudicationContent {
    param([Parameter(Mandatory)][string]$Content,[Parameter(Mandatory)]$Packet,[Parameter(Mandatory)][array]$Findings)
    try { $result = ConvertFrom-Json -InputObject $Content -AsHashtable -ErrorAction Stop }
    catch { throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Adjudication content is not JSON.') }
    Assert-CodeReviewExactKeys -Object $result -Names @('schema','review_complete','decisions','uncertainties')
    if ($result.schema -cne 'dnppv2-model-adjudication/v1' -or $result.review_complete -isnot [bool] -or
        $result.decisions -isnot [array] -or $result.uncertainties -isnot [array] -or
        $result.decisions.Count -ne $Findings.Count) {
        throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Adjudication schema is invalid.')
    }
    $findingIds = @($Findings | ForEach-Object { $_.id })
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($decision in $result.decisions) {
        Assert-CodeReviewExactKeys -Object $decision -Names @('id','decision','evidence_path','evidence_quote','reason')
        if ($decision.id -cnotin $findingIds -or -not $seen.Add([string]$decision.id) -or
            $decision.decision -cnotin @('CONFIRMED','REJECTED') -or
            -not (Test-CodeReviewEvidenceQuote -Packet $Packet -Path $decision.evidence_path -Quote $decision.evidence_quote) -or
            [string]::IsNullOrWhiteSpace($decision.reason)) {
            throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Adjudication decision is invalid.')
        }
    }
    $targetPaths = if ($Packet.review_type -eq 'CODE') { @($Packet.changed_files.path) } else { @($Packet.material.source_path) }
    $seenUncertainty = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($uncertainty in $result.uncertainties) {
        Assert-CodeReviewExactKeys -Object $uncertainty -Names @('id','path','reason','required_evidence')
        if ($uncertainty.id -isnot [string] -or $uncertainty.id -cnotmatch '^U-[0-9]{3,}$' -or
            -not $seenUncertainty.Add($uncertainty.id) -or
            ($null -ne $uncertainty.path -and $uncertainty.path -cnotin $targetPaths) -or
            [string]::IsNullOrWhiteSpace($uncertainty.reason) -or
            [string]::IsNullOrWhiteSpace($uncertainty.required_evidence)) {
            throw (New-CodeReviewFailure -FailureClass 'MODEL_PROTOCOL_FAILURE' -Message 'Adjudication uncertainty is invalid.')
        }
    }
    return $result
}

function ConvertTo-CodeReviewJsonBytes {
    param([Parameter(Mandatory)]$Value)
    $json = ConvertTo-Json -InputObject $Value -Depth 100 -Compress
    return ,[Text.UTF8Encoding]::new($false).GetBytes($json)
}

function Write-CodeReviewAtomicBytes {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][byte[]]$Bytes)
    $parent = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Path))
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $leaf = [IO.Path]::GetFileName($Path)
    $nonce = [guid]::NewGuid().ToString('N')
    $temporary = [IO.Path]::Combine($parent, ".$leaf.$nonce.tmp")
    try {
        [IO.File]::WriteAllBytes($temporary, $Bytes)
        [IO.File]::Move($temporary, $Path, $true)
    }
    finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}

function ConvertFrom-CodeReviewTextBytes {
    param([Parameter(Mandatory)][byte[]]$Bytes)
    try {
        if ($Bytes.Length -ge 3 -and $Bytes[0] -eq 0xEF -and $Bytes[1] -eq 0xBB -and $Bytes[2] -eq 0xBF) {
            if ([Array]::IndexOf($Bytes, [byte]0, 3) -ge 0) { return $null }
            return [Text.UTF8Encoding]::new($false, $true).GetString($Bytes, 3, $Bytes.Length - 3)
        }
        if ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE) {
            return [Text.UnicodeEncoding]::new($false, $false, $true).GetString($Bytes, 2, $Bytes.Length - 2)
        }
        if ($Bytes.Length -ge 2 -and $Bytes[0] -eq 0xFE -and $Bytes[1] -eq 0xFF) {
            return [Text.UnicodeEncoding]::new($true, $false, $true).GetString($Bytes, 2, $Bytes.Length - 2)
        }
        if ([Array]::IndexOf($Bytes, [byte]0) -ge 0) { return $null }
        return [Text.UTF8Encoding]::new($false, $true).GetString($Bytes)
    }
    catch [Text.DecoderFallbackException] { return $null }
}

function Resolve-CodeReviewRepositoryRoot {
    param([string]$RepositoryRoot = $PSScriptRoot)
    if ($RepositoryRoot -match '^[A-Za-z]:[^\\/]') { throw 'LOCAL_PATH_FAILURE: drive-relative repository path.' }
    $root = (& git -C $RepositoryRoot rev-parse --show-toplevel 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $root) { throw 'LOCAL_PATH_FAILURE: repository root unavailable.' }
    return [IO.Path]::GetFullPath($root)
}

function Resolve-CodeReviewPath {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$Path,
        [switch]$InsideRepository
    )
    if ($Path -match '^[A-Za-z]:[^\\/]') { throw 'LOCAL_PATH_FAILURE: drive-relative path.' }
    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $full = if ([IO.Path]::IsPathFullyQualified($Path)) { [IO.Path]::GetFullPath($Path) }
            else { [IO.Path]::GetFullPath([IO.Path]::Combine($root, $Path)) }
    if ($InsideRepository) {
        $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
        if (-not ($full.Equals($root, $comparison) -or $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, $comparison))) {
            throw 'LOCAL_PATH_FAILURE: path is outside repository.'
        }
    }
    return $full
}

function Invoke-CodeReviewRawGit {
    param([Parameter(Mandatory)][string]$RepositoryRoot, [Parameter(Mandatory)][string[]]$Arguments)
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'git'
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    $start.Environment['LC_ALL'] = 'C'
    foreach ($argument in @('-C', $RepositoryRoot) + $Arguments) { [void]$start.ArgumentList.Add([string]$argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $output = [IO.MemoryStream]::new()
    try {
        if (-not $process.Start()) { throw 'PACKET_VALIDATION_FAILURE: git did not start.' }
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.StandardOutput.BaseStream.CopyTo($output)
        $process.WaitForExit()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "PACKET_VALIDATION_FAILURE: git exited $($process.ExitCode): $stderr" }
        return ,$output.ToArray()
    }
    finally { $output.Dispose(); $process.Dispose() }
}

function Assert-CodeReviewCommittedCandidate {
    param([Parameter(Mandatory)][string]$RepositoryRoot,[Parameter(Mandatory)][string]$BaseSha,[Parameter(Mandatory)][string]$HeadSha)
    $canonicalRoot = Resolve-CodeReviewRepositoryRoot -RepositoryRoot $RepositoryRoot
    if ($BaseSha -notmatch '^[0-9a-fA-F]{40}$' -or $HeadSha -notmatch '^[0-9a-fA-F]{40}$') {
        throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'Candidate SHA must be a full commit ID.')
    }
    $currentBytes = Invoke-CodeReviewRawGit -RepositoryRoot $canonicalRoot -Arguments @('rev-parse','--verify','HEAD^{commit}')
    $current = [Text.UTF8Encoding]::new($false,$true).GetString($currentBytes).Trim()
    if ($current -cne $HeadSha.ToLowerInvariant()) {
        throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'Candidate HEAD does not equal current checkout HEAD.')
    }
    foreach ($sha in @($BaseSha,$HeadSha)) {
        $resolvedBytes = Invoke-CodeReviewRawGit -RepositoryRoot $canonicalRoot -Arguments @('rev-parse','--verify',"$sha^{commit}")
        $resolved = [Text.UTF8Encoding]::new($false,$true).GetString($resolvedBytes).Trim()
        if ($resolved -cne $sha.ToLowerInvariant()) {
            throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'Candidate contains a non-commit or unresolved ID.')
        }
    }
    & git -C $canonicalRoot merge-base --is-ancestor $BaseSha $HeadSha 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'Base is not an ancestor of HEAD.')
    }
    $statusBytes = Invoke-CodeReviewRawGit -RepositoryRoot $canonicalRoot -Arguments @('status','--porcelain=v1','-z','--untracked-files=all')
    if ($statusBytes.Length -ne 0) {
        throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'CODE review requires a clean index and working tree.')
    }
    return $canonicalRoot
}

function Get-CodeReviewBlobBytes {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$BlobSha
    )
    if ($BlobSha -notmatch '^[0-9a-fA-F]{40}$') { throw 'PACKET_VALIDATION_FAILURE: malformed blob SHA.' }
    return ,(Invoke-CodeReviewRawGit -RepositoryRoot $RepositoryRoot -Arguments @('cat-file','blob',$BlobSha))
}

function Get-CodeReviewTextDiff {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$BaseSha,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$Path,
        [ValidateSet(0,80)][int]$ContextLines = 0
    )
    $arguments = @(
        '-c','core.quotepath=true','diff','--no-renames','--no-ext-diff',
        '--no-textconv','--no-color','--text','--full-index',
        '--diff-algorithm=myers','--no-indent-heuristic',
        '--inter-hunk-context=0',"--unified=$ContextLines",
        '--src-prefix=a/','--dst-prefix=b/',
        $BaseSha,$HeadSha,'--',$Path
    )
    $bytes = Invoke-CodeReviewRawGit -RepositoryRoot $RepositoryRoot -Arguments $arguments
    try { $diff = [Text.UTF8Encoding]::new($false,$true).GetString($bytes) }
    catch [Text.DecoderFallbackException] { throw 'PACKET_VALIDATION_FAILURE: textual diff is not UTF-8.' }
    $diff = [regex]::Replace($diff, '(?m)^(@@ -[0-9]+(?:,[0-9]+)? \+[0-9]+(?:,[0-9]+)? @@)[^\r\n]*', '$1')
    return $diff
}

function ConvertFrom-CodeReviewRawDiff {
    param([Parameter(Mandatory)][byte[]]$Bytes)
    if ($Bytes.Length -eq 0 -or $Bytes[-1] -ne 0) { throw 'PACKET_VALIDATION_FAILURE: raw diff lacks final NUL.' }
    $tokens = [Collections.Generic.List[byte[]]]::new()
    $start = 0
    for ($i = 0; $i -lt $Bytes.Length; $i++) {
        if ($Bytes[$i] -eq 0) {
            $length = $i - $start
            $token = [byte[]]::new($length)
            if ($length -gt 0) { [Array]::Copy($Bytes, $start, $token, 0, $length) }
            $tokens.Add($token)
            $start = $i + 1
        }
    }
    # The last NUL closes the final path; there is no extra token in this splitter.
    if ($tokens.Count % 2 -ne 0) { throw 'PACKET_VALIDATION_FAILURE: odd raw diff token count.' }
    $entries = [Collections.Generic.List[object]]::new()
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    for ($i = 0; $i -lt $tokens.Count; $i += 2) {
        $metaBytes = $tokens[$i]
        $pathBytes = $tokens[$i + 1]
        if ($metaBytes.Length -eq 0 -or $pathBytes.Length -eq 0) { throw 'PACKET_VALIDATION_FAILURE: empty raw diff token.' }
        foreach ($b in $metaBytes) { if ($b -gt 127) { throw 'PACKET_VALIDATION_FAILURE: non-ASCII metadata.' } }
        $meta = [Text.Encoding]::ASCII.GetString($metaBytes)
        if ($meta -cnotmatch '^:([0-7]{6}) ([0-7]{6}) ([0-9a-fA-F]{40}) ([0-9a-fA-F]{40}) ([ADMT])$') {
            throw 'PACKET_VALIDATION_FAILURE: malformed raw diff metadata.'
        }
        $baseMode, $headMode, $baseBlob, $headBlob, $status = $Matches[1..5]
        $baseBlob = $baseBlob.ToLowerInvariant(); $headBlob = $headBlob.ToLowerInvariant()
        $zero = '0' * 40
        $baseAbsent = $baseMode -eq '000000' -and $baseBlob -eq $zero
        $headAbsent = $headMode -eq '000000' -and $headBlob -eq $zero
        if (($baseMode -eq '000000') -xor ($baseBlob -eq $zero) -or
            ($headMode -eq '000000') -xor ($headBlob -eq $zero)) {
            throw 'PACKET_VALIDATION_FAILURE: partial absent side.'
        }
        $kind = { param($mode) if ($mode -eq '160000') { 'gitlink' } elseif ($mode -eq '120000') { 'symlink' } elseif ($mode -in @('100644','100755')) { 'regular' } else { throw 'PACKET_VALIDATION_FAILURE: unsupported mode.' } }
        $baseKind = if ($baseAbsent) { $null } else { & $kind $baseMode }
        $headKind = if ($headAbsent) { $null } else { & $kind $headMode }
        $valid = switch ($status) {
            A { $baseAbsent -and -not $headAbsent }
            D { -not $baseAbsent -and $headAbsent }
            M { -not $baseAbsent -and -not $headAbsent -and $baseKind -eq $headKind -and ($baseMode -ne $headMode -or $baseBlob -ne $headBlob) }
            T { -not $baseAbsent -and -not $headAbsent -and $baseKind -ne $headKind }
        }
        if (-not $valid) { throw 'PACKET_VALIDATION_FAILURE: status/side mismatch.' }
        try { $path = $utf8.GetString($pathBytes) }
        catch [Text.DecoderFallbackException] { $path = $null }
        $entries.Add([pscustomobject]@{
            Status = $status; Path = $path; PathBytes = $pathBytes
            BaseMode = $(if ($baseAbsent) { $null } else { $baseMode })
            HeadMode = $(if ($headAbsent) { $null } else { $headMode })
            BaseBlob = $(if ($baseAbsent) { $null } else { $baseBlob })
            HeadBlob = $(if ($headAbsent) { $null } else { $headBlob })
            BaseKind = $baseKind; HeadKind = $headKind
        })
    }
    return $entries.ToArray()
}

function New-CodeReviewCandidateDescriptorV2 {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$BaseSha,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$Scope,
        [Parameter(Mandatory)][string]$Requirement
    )
    if ($BaseSha -notmatch '^[0-9a-fA-F]{40}$' -or $HeadSha -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'PACKET_VALIDATION_FAILURE: candidate SHA is malformed.'
    }
    $baseShaLower = $BaseSha.ToLowerInvariant()
    $headShaLower = $HeadSha.ToLowerInvariant()
    $baseTree = (& git -C $RepositoryRoot rev-parse "$baseShaLower^{tree}" 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $baseTree -notmatch '^[0-9a-fA-F]{40}$') { throw 'PACKET_VALIDATION_FAILURE: base tree unavailable.' }
    $headTree = (& git -C $RepositoryRoot rev-parse "$headShaLower^{tree}" 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $headTree -notmatch '^[0-9a-fA-F]{40}$') { throw 'PACKET_VALIDATION_FAILURE: head tree unavailable.' }
    $raw = Invoke-CodeReviewRawGit -RepositoryRoot $RepositoryRoot -Arguments @(
        'diff-tree','-r','--no-commit-id','--raw','-z','--no-renames','--no-abbrev',$baseShaLower,$headShaLower
    )
    $entries = @(ConvertFrom-CodeReviewRawDiff -Bytes $raw)
    if ($entries.Count -eq 0) { throw 'PACKET_VALIDATION_FAILURE: candidate has no changed entries.' }
    $orderedEntries = @($entries | Sort-Object -Stable -Property @{ Expression = {
        [Convert]::ToHexString($_.PathBytes)
    } })
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('schema=dnppv2-committed-candidate/v2')
    $lines.Add("base=$baseShaLower")
    $lines.Add("head=$headShaLower")
    $lines.Add("base_tree=$($baseTree.ToLowerInvariant())")
    $lines.Add("head_tree=$($headTree.ToLowerInvariant())")
    $lines.Add('scope=' + [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Scope)))
    $lines.Add('requirement_sha256=' + (Get-CodeReviewSha256 -Text $Requirement))
    foreach ($entry in $orderedEntries) {
        $lines.Add('status=' + $entry.Status)
        $lines.Add('path_b64=' + [Convert]::ToBase64String($entry.PathBytes))
        $lines.Add('base_blob=' + $(if ($entry.BaseBlob) { $entry.BaseBlob } else { 'NONE' }))
        $lines.Add('head_blob=' + $(if ($entry.HeadBlob) { $entry.HeadBlob } else { 'NONE' }))
        $lines.Add('base_mode=' + $(if ($entry.BaseMode) { $entry.BaseMode } else { 'NONE' }))
        $lines.Add('head_mode=' + $(if ($entry.HeadMode) { $entry.HeadMode } else { 'NONE' }))
    }
    $descriptor = ($lines -join "`n") + "`n"
    return [pscustomobject]@{
        Descriptor = $descriptor
        SnapshotSha256 = Get-CodeReviewSha256 -Text $descriptor
        BaseTree = $baseTree.ToLowerInvariant()
        HeadTree = $headTree.ToLowerInvariant()
        Entries = $orderedEntries
    }
}

function New-CodeReviewChangedFilePacketEntry {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$BaseSha,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)]$Entry
    )
    $baseBytes = if ($Entry.BaseBlob -and $Entry.BaseKind -ne 'gitlink') {
        Get-CodeReviewBlobBytes -RepositoryRoot $RepositoryRoot -BlobSha $Entry.BaseBlob
    } else { $null }
    $headBytes = if ($Entry.HeadBlob -and $Entry.HeadKind -ne 'gitlink') {
        Get-CodeReviewBlobBytes -RepositoryRoot $RepositoryRoot -BlobSha $Entry.HeadBlob
    } else { $null }
    $baseText = if ($Entry.BaseKind -eq 'regular') { ConvertFrom-CodeReviewTextBytes -Bytes $baseBytes } else { $null }
    $headText = if ($Entry.HeadKind -eq 'regular') { ConvertFrom-CodeReviewTextBytes -Bytes $headBytes } else { $null }
    $baseKind = if ($Entry.BaseKind -eq 'regular') { if ($null -ne $baseText) { 'text' } else { 'binary' } } else { $Entry.BaseKind }
    $headKind = if ($Entry.HeadKind -eq 'regular') { if ($null -ne $headText) { 'text' } else { 'binary' } } else { $Entry.HeadKind }
    $textDiffEligible = $Entry.Status -ne 'T' -and
        ($null -eq $baseKind -or $baseKind -eq 'text') -and
        ($null -eq $headKind -or $headKind -eq 'text')
    if ($textDiffEligible) {
        $zeroDiff = Get-CodeReviewTextDiff -RepositoryRoot $RepositoryRoot -BaseSha $BaseSha -HeadSha $HeadSha -Path $Entry.Path
    }
    else {
        $zeroDiff = 'status={0};base_mode={1};head_mode={2};base_kind={3};head_kind={4};base_blob={5};head_blob={6}' -f
            $Entry.Status, $(if ($Entry.BaseMode) { $Entry.BaseMode } else { 'NONE' }),
            $(if ($Entry.HeadMode) { $Entry.HeadMode } else { 'NONE' }),
            $(if ($baseKind) { $baseKind } else { 'NONE' }),
            $(if ($headKind) { $headKind } else { 'NONE' }),
            $(if ($Entry.BaseBlob) { $Entry.BaseBlob } else { 'NONE' }),
            $(if ($Entry.HeadBlob) { $Entry.HeadBlob } else { 'NONE' })
    }
    $reviewContext = $null
    $contextKind = 'metadata-only'
    $omitted = $false
    $selectedText = if ($null -ne $headText) { $headText } else { $baseText }
    $selectedBytes = if ($null -ne $headText) { $headBytes } else { $baseBytes }
    if ($null -ne $selectedText) {
        if ($selectedBytes.Length -le 65536) {
            $reviewContext = $selectedText
            $contextKind = if ($null -ne $headText) { 'full-head' } else { 'full-base' }
        }
        elseif ($textDiffEligible) {
            $reviewContext = Get-CodeReviewTextDiff -RepositoryRoot $RepositoryRoot -BaseSha $BaseSha -HeadSha $HeadSha -Path $Entry.Path -ContextLines 80
            $contextKind = 'context-diff'
            $omitted = $true
        }
        else {
            $reviewContext = ($selectedText -split '\r\n|\n|\r' | Select-Object -First 80) -join "`n"
            $contextKind = 'context-diff'
            $omitted = $true
        }
    }
    if ($contextKind -eq 'metadata-only' -and ($Entry.BaseKind -eq 'symlink' -or $Entry.HeadKind -eq 'symlink')) {
        $linkBytes = if ($Entry.HeadKind -eq 'symlink') { $headBytes } else { $baseBytes }
        $linkText = ConvertFrom-CodeReviewTextBytes -Bytes $linkBytes
        if ($null -ne $linkText) { $reviewContext = 'symlink_target=' + $linkText }
    }
    return [ordered]@{
        status = $Entry.Status
        path = $Entry.Path
        path_b64 = [Convert]::ToBase64String($Entry.PathBytes)
        base_blob = $Entry.BaseBlob
        head_blob = $Entry.HeadBlob
        base_mode = $Entry.BaseMode
        head_mode = $Entry.HeadMode
        base_kind = $baseKind
        head_kind = $headKind
        zero_context_diff = $zeroDiff
        review_context = $reviewContext
        review_context_kind = $contextKind
        content_omitted = $omitted
    }
}

function Get-CodeReviewHeadContextEntry {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string[]]$ChangedPaths
    )
    $absolute = Resolve-CodeReviewPath -RepositoryRoot $RepositoryRoot -Path $Path -InsideRepository
    $relative = [IO.Path]::GetRelativePath($RepositoryRoot, $absolute).Replace('\', '/')
    if ($relative -eq '.' -or $relative.StartsWith('../', [StringComparison]::Ordinal)) {
        throw 'LOCAL_PATH_FAILURE: context path is not a file within repository.'
    }
    if ($ChangedPaths -ccontains $relative) { throw 'PACKET_VALIDATION_FAILURE: changed path cannot be context.' }
    $record = Invoke-CodeReviewRawGit -RepositoryRoot $RepositoryRoot -Arguments @('ls-tree','-z',$HeadSha,'--',$relative)
    if ($record.Length -lt 2 -or $record[-1] -ne 0) { throw 'PACKET_VALIDATION_FAILURE: context is not present at HEAD.' }
    $recordText = try { [Text.UTF8Encoding]::new($false,$true).GetString($record,0,$record.Length - 1) }
                  catch { throw 'PACKET_VALIDATION_FAILURE: context tree record is not UTF-8.' }
    $pattern = '(?s)^([0-7]{6}) blob ([0-9a-fA-F]{40})\t(.+)$'
    if ($recordText -cnotmatch $pattern -or $Matches[3] -cne $relative) {
        throw 'PACKET_VALIDATION_FAILURE: context tree identity mismatch.'
    }
    $mode = $Matches[1]
    $blob = $Matches[2].ToLowerInvariant()
    if ($mode -notin @('100644','100755')) { throw 'PACKET_VALIDATION_FAILURE: context is not a regular file.' }
    $bytes = Get-CodeReviewBlobBytes -RepositoryRoot $RepositoryRoot -BlobSha $blob
    $content = ConvertFrom-CodeReviewTextBytes -Bytes $bytes
    if ($null -eq $content) { throw 'PACKET_VALIDATION_FAILURE: context is binary.' }
    return [pscustomobject]@{ Path = $relative; PathBytes = [Text.UTF8Encoding]::new($false,$true).GetBytes($relative)
        Mode = $mode; Blob = $blob; Bytes = $bytes; Content = $content }
}

function Get-CodeReviewContextRanges {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$ContextSpecPath,
        [Parameter(Mandatory)][string[]]$ChangedPaths,
        [string[]]$FullContextPaths = @()
    )
    $specAbsolute = Resolve-CodeReviewPath -RepositoryRoot $RepositoryRoot -Path $ContextSpecPath -InsideRepository
    if (-not [IO.File]::Exists($specAbsolute)) { throw 'LOCAL_PATH_FAILURE: context spec does not exist.' }
    $specBytes = [IO.File]::ReadAllBytes($specAbsolute)
    $specText = ConvertFrom-CodeReviewTextBytes -Bytes $specBytes
    if ($null -eq $specText) { throw 'PACKET_VALIDATION_FAILURE: context spec is not text.' }
    try { $spec = ConvertFrom-Json -InputObject $specText -AsHashtable -ErrorAction Stop }
    catch { throw 'PACKET_VALIDATION_FAILURE: context spec JSON is malformed.' }
    if ($spec -isnot [Collections.IDictionary] -or $spec.Count -ne 2 -or
        -not $spec.Contains('schema') -or -not $spec.Contains('ranges') -or
        $spec.schema -cne 'dnppv2-review-context/v1' -or $spec.ranges -isnot [array]) {
        throw 'PACKET_VALIDATION_FAILURE: context spec schema is invalid.'
    }
    $grouped = [Collections.Generic.Dictionary[string,object]]::new($(if ($IsWindows) { [StringComparer]::OrdinalIgnoreCase } else { [StringComparer]::Ordinal }))
    foreach ($range in $spec.ranges) {
        if ($range -isnot [Collections.IDictionary] -or $range.Count -ne 3 -or
            -not $range.Contains('path') -or -not $range.Contains('start_line') -or -not $range.Contains('end_line') -or
            $range.path -isnot [string] -or $range.start_line -isnot [long] -or $range.end_line -isnot [long]) {
            throw 'PACKET_VALIDATION_FAILURE: context range schema is invalid.'
        }
        if ($range.start_line -lt 1 -or $range.end_line -lt $range.start_line) {
            throw 'PACKET_VALIDATION_FAILURE: context line range is invalid.'
        }
        $identity = Get-CodeReviewHeadContextEntry -RepositoryRoot $RepositoryRoot -HeadSha $HeadSha -Path $range.path -ChangedPaths $ChangedPaths
        if ($FullContextPaths -ccontains $identity.Path) { throw 'PACKET_VALIDATION_FAILURE: full and ranged context duplicate.' }
        if (-not $grouped.ContainsKey($identity.Path)) {
            $grouped.Add($identity.Path, [pscustomobject]@{ Identity = $identity; Ranges = [Collections.Generic.List[object]]::new() })
        }
        $grouped[$identity.Path].Ranges.Add([pscustomobject]@{ Start = [int]$range.start_line; End = [int]$range.end_line })
    }
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($path in $grouped.Keys) {
        $item = $grouped[$path]
        $lineMatches = [regex]::Matches($item.Identity.Content, '[^\r\n]*(?:\r\n|\r|\n)|[^\r\n]+\z')
        $sorted = @($item.Ranges | Sort-Object Start,End)
        $merged = [Collections.Generic.List[object]]::new()
        foreach ($range in $sorted) {
            if ($range.End -gt $lineMatches.Count) { throw 'PACKET_VALIDATION_FAILURE: context range passes EOF.' }
            if ($merged.Count -gt 0 -and $range.Start -le $merged[-1].End + 1) {
                $merged[-1].End = [Math]::Max($merged[-1].End, $range.End)
            }
            else { $merged.Add([pscustomobject]@{ Start = $range.Start; End = $range.End }) }
        }
        $ranges = @($merged | ForEach-Object {
            if (($_.End - $_.Start + 1) -gt 400) { throw 'PACKET_VALIDATION_FAILURE: merged context exceeds 400 lines.' }
            $builder = [Text.StringBuilder]::new()
            for ($line = $_.Start - 1; $line -lt $_.End; $line++) { [void]$builder.Append($lineMatches[$line].Value) }
            [ordered]@{ start_line = $_.Start; end_line = $_.End; content = $builder.ToString() }
        })
        $entries.Add([ordered]@{
            path = $path
            path_b64 = [Convert]::ToBase64String($item.Identity.PathBytes)
            head_blob = $item.Identity.Blob
            head_mode = $item.Identity.Mode
            kind = 'text'
            context_kind = 'ranges'
            content = $null
            ranges = $ranges
        })
    }
    return [pscustomobject]@{ Entries = $entries.ToArray(); SpecSha256 = Get-CodeReviewBytesSha256 -Bytes $specBytes }
}

function New-CodeReviewPacketV2 {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$BaseSha,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$Scope,
        [Parameter(Mandatory)][string]$Requirement,
        [string[]]$ContextPath = @(),
        [string]$ContextSpecPath
    )
    $candidate = New-CodeReviewCandidateDescriptorV2 -RepositoryRoot $RepositoryRoot -BaseSha $BaseSha -HeadSha $HeadSha -Scope $Scope -Requirement $Requirement
    if ($candidate.Entries.Count -eq 0) {
        throw (New-CodeReviewFailure -FailureClass 'PACKET_VALIDATION_FAILURE' -Message 'CODE candidate has no changed paths.')
    }
    if (@($candidate.Entries | Where-Object { $null -eq $_.Path }).Count -gt 0) {
        throw 'PACKET_VALIDATION_FAILURE: Git path is not UTF-8.'
    }
    $changed = @($candidate.Entries | ForEach-Object {
        New-CodeReviewChangedFilePacketEntry -RepositoryRoot $RepositoryRoot -BaseSha $BaseSha -HeadSha $HeadSha -Entry $_
    })
    $context = [Collections.Generic.List[object]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new($(if ($IsWindows) { [StringComparer]::OrdinalIgnoreCase } else { [StringComparer]::Ordinal }))
    foreach ($path in $ContextPath) {
        $identity = Get-CodeReviewHeadContextEntry -RepositoryRoot $RepositoryRoot -HeadSha $HeadSha -Path $path -ChangedPaths @($candidate.Entries.Path)
        if (-not $seen.Add($identity.Path)) { throw 'PACKET_VALIDATION_FAILURE: duplicate context path.' }
        if ($identity.Bytes.Length -gt 65536) { throw 'PACKET_VALIDATION_FAILURE: large context requires ranges.' }
        $context.Add([ordered]@{
            path = $identity.Path
            path_b64 = [Convert]::ToBase64String($identity.PathBytes)
            head_blob = $identity.Blob
            head_mode = $identity.Mode
            kind = 'text'
            context_kind = 'full-head'
            content = $identity.Content
            ranges = @()
        })
    }
    $contextSpecSha = $null
    if ($ContextSpecPath) {
        $ranged = Get-CodeReviewContextRanges -RepositoryRoot $RepositoryRoot -HeadSha $HeadSha -ContextSpecPath $ContextSpecPath -ChangedPaths @($candidate.Entries.Path) -FullContextPaths @($seen)
        $contextSpecSha = $ranged.SpecSha256
        foreach ($entry in $ranged.Entries) { $context.Add($entry) }
    }
    $sortedContext = @($context | Sort-Object -Stable -Property @{ Expression = { [Convert]::ToHexString([Text.Encoding]::UTF8.GetBytes($_.path)) } })
    $packet = [ordered]@{
        schema = 'dnppv2-review-packet/v2'
        review_type = 'CODE'
        scope = $Scope
        requirement = $Requirement
        requirement_sha256 = Get-CodeReviewSha256 -Text $Requirement
        candidate = [ordered]@{
            base_sha = $BaseSha.ToLowerInvariant()
            head_sha = $HeadSha.ToLowerInvariant()
            base_tree = $candidate.BaseTree
            head_tree = $candidate.HeadTree
            snapshot_sha256 = $candidate.SnapshotSha256
        }
        changed_files = $changed
        context_files = $sortedContext
        packet_policy = [ordered]@{
            zero_context_diff = $true
            small_text_full_content_limit_bytes = 65536
            large_text_context_lines = 80
            context_spec_sha256 = $contextSpecSha
        }
    }
    $bytes = ConvertTo-CodeReviewJsonBytes -Value $packet
    Assert-CodeReviewNoLikelySecrets -Text ([Text.UTF8Encoding]::new($false,$true).GetString($bytes))
    return [pscustomobject]@{
        Packet = $packet
        Bytes = $bytes
        PacketSha256 = Get-CodeReviewBytesSha256 -Bytes $bytes
        Candidate = $candidate
    }
}

function New-CodeReviewMaterialPacketV1 {
    param(
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][ValidateSet('DOCUMENTATION','TEST_ARTIFACT')][string]$ReviewType,
        [Parameter(Mandatory)][string]$HeadSha,
        [Parameter(Mandatory)][string]$Scope,
        [Parameter(Mandatory)][string]$Requirement,
        [Parameter(Mandatory)][string]$ReviewMaterialPath
    )
    if ($HeadSha -notmatch '^[0-9a-fA-F]{40}$') { throw 'PACKET_VALIDATION_FAILURE: head SHA invalid.' }
    $materialPath = Resolve-CodeReviewPath -RepositoryRoot $RepositoryRoot -Path $ReviewMaterialPath -InsideRepository
    if (-not [IO.File]::Exists($materialPath)) { throw 'LOCAL_PATH_FAILURE: review material not found.' }
    $bytes = [IO.File]::ReadAllBytes($materialPath)
    $content = ConvertFrom-CodeReviewTextBytes -Bytes $bytes
    if ($null -eq $content) { throw 'PACKET_VALIDATION_FAILURE: review material is binary.' }
    $relative = [IO.Path]::GetRelativePath($RepositoryRoot, $materialPath).Replace('\','/')
    $headLower = $HeadSha.ToLowerInvariant()
    $headTree = (& git -C $RepositoryRoot rev-parse "$headLower^{tree}" 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $headTree -notmatch '^[0-9a-fA-F]{40}$') {
        throw 'PACKET_VALIDATION_FAILURE: head tree unavailable.'
    }
    $materialSha = Get-CodeReviewBytesSha256 -Bytes $bytes
    $requirementSha = Get-CodeReviewSha256 -Text $Requirement
    $packet = [ordered]@{
        schema = 'dnppv2-review-material-packet/v1'
        review_type = $ReviewType
        scope = $Scope
        requirement = $Requirement
        requirement_sha256 = $requirementSha
        candidate = [ordered]@{ head_sha = $headLower; head_tree = $headTree.ToLowerInvariant() }
        material = [ordered]@{
            source_path = $relative
            sha256 = $materialSha
            byte_length = $bytes.Length
            content = $content
        }
    }
    $packetBytes = ConvertTo-CodeReviewJsonBytes -Value $packet
    Assert-CodeReviewNoLikelySecrets -Text ([Text.UTF8Encoding]::new($false,$true).GetString($packetBytes))
    $packetSha = Get-CodeReviewBytesSha256 -Bytes $packetBytes
    $descriptor = @(
        'schema=dnppv2-review-material-snapshot/v1',
        "review_type=$ReviewType", "scope=$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Scope)))",
        "requirement_sha256=$requirementSha", "head=$headLower", "head_tree=$($headTree.ToLowerInvariant())",
        "material_sha256=$materialSha", "material_byte_length=$($bytes.Length)", "packet_sha256=$packetSha"
    ) -join "`n"
    $descriptor += "`n"
    return [pscustomobject]@{
        Packet = $packet
        Bytes = $packetBytes
        PacketSha256 = $packetSha
        SnapshotSha256 = Get-CodeReviewSha256 -Text $descriptor
        MaterialSha256 = $materialSha
    }
}
