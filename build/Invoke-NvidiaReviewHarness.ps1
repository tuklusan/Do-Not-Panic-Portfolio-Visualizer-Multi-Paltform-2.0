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
[CmdletBinding(DefaultParameterSetName = 'Review')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Review')][ValidateSet('CODE', 'DOCUMENTATION', 'TEST_ARTIFACT')][string]$ReviewType,
    [Parameter(Mandatory = $true, ParameterSetName = 'Review')][string]$ReviewMaterialPath,
    [Parameter(ParameterSetName = 'Review')][string]$Endpoint = 'https://integrate.api.nvidia.com/v1',
    [Parameter(ParameterSetName = 'Review')][string]$Model = 'nvidia/nemotron-3-ultra-550b-a55b',
    [Parameter(ParameterSetName = 'Review')][string]$OutputDirectory = 'build/nvidia-review',
    [Parameter(ParameterSetName = 'Review')][int]$MaxRequestBytes = 1048576,
    [Parameter(ParameterSetName = 'Review')][ValidateRange(1, 32768)][int]$MaxTokens = 8192,
    [Parameter(ParameterSetName = 'Review')][ValidateRange(60, 7200)][int]$RequestTimeoutSeconds = 3600,
    [Parameter(Mandatory = $true, ParameterSetName = 'SelfTest')][switch]$SelfTest,
    [switch]$AcknowledgeEndpointOverride
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:LastNvidiaResponseAt = $null
$script:MinimumNvidiaResponseSpacingSeconds = 15
$script:NvidiaSpacingRoot = $null

$commonPath = Join-Path $PSScriptRoot 'NvidiaWorkflowCommon.ps1'
if (-not (Test-Path -LiteralPath $commonPath)) { throw "Missing Nvidia workflow common module: $commonPath" }
. $commonPath

function Get-ReviewPasses([string]$Type) {
    switch ($Type) {
        'CODE' { return @(
            @{ Id = 'CODE-A'; Focus = 'requirements, explicit acceptance criteria, functional correctness, contracts, state and data semantics' },
            @{ Id = 'CODE-B'; Focus = 'runtime failures, hostile input, safety, lifecycle, cleanup, concurrency, retries, security and recovery' },
            @{ Id = 'CODE-C'; Focus = 'integration, regression, compatibility, platform behavior, and test adequacy' }) }
        'DOCUMENTATION' { return @(
            @{ Id = 'DOCUMENTATION-A'; Focus = 'technical and factual correctness against supplied authoritative material' },
            @{ Id = 'DOCUMENTATION-B'; Focus = 'internal consistency, completeness, unambiguous mandatory behavior and cross references' },
            @{ Id = 'DOCUMENTATION-C'; Focus = 'implementation and test readiness: boundaries, contracts, lifecycle, errors, observability and acceptance proof' }) }
        default { return @(
            @{ Id = 'TEST_ARTIFACT-A'; Focus = 'direct evidence: failures, crashes, warnings, assertions, timing and missing expected proof' },
            @{ Id = 'TEST_ARTIFACT-B'; Focus = 'hidden or masked signals, false passes, contradictory evidence, flaky behavior and wrong-run artifacts' },
            @{ Id = 'TEST_ARTIFACT-C'; Focus = 'correlation of evidence to acceptance criteria and whether the claimed result is genuinely proven' }) }
    }
}

function Get-TransientStatus([object]$Exception) {
    $responseProperty = $Exception.PSObject.Properties['Response']
    if ($null -ne $responseProperty -and $null -ne $responseProperty.Value -and $null -ne $responseProperty.Value.StatusCode) { return [int]$responseProperty.Value.StatusCode }
    return $null
}

function Write-ReviewTelemetry([string]$Root, [hashtable]$Record) {
    $telemetryPath = Join-Path $Root 'telemetry.jsonl'
    ($Record | ConvertTo-Json -Depth 8 -Compress) | Add-Content -LiteralPath $telemetryPath -Encoding UTF8
}

function Convert-ReviewFieldToText([object]$Value) {
    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [string]) {
        $trimmed = $Value.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            return $null
        }

        return $trimmed
    }

    $valueType = $Value.GetType()
    if ($valueType.IsPrimitive -or $valueType.IsEnum -or
        $Value -is [decimal] -or $Value -is [datetime] -or
        $Value -is [datetimeoffset] -or $Value -is [guid]) {
        return [string]$Value
    }

    try {
        $json = $Value | ConvertTo-Json -Depth 12 -Compress
        if ([string]::IsNullOrWhiteSpace($json)) {
            return $null
        }

        return $json
    }
    catch {
        $text = [string]$Value
        if ([string]::IsNullOrWhiteSpace($text)) {
            return $null
        }

        return $text.Trim()
    }
}

function Get-ReviewFindingTextValue {
    param(
        [Parameter(Mandatory = $true)][object]$Finding,
        [Parameter(Mandatory = $true)][string[]]$PropertyNames
    )

    if ($Finding -is [System.Collections.IDictionary]) {
        foreach ($propertyName in $PropertyNames) {
            foreach ($key in $Finding.Keys) {
                if ([string]::Equals([string]$key, $propertyName, [StringComparison]::OrdinalIgnoreCase)) {
                    $text = Convert-ReviewFieldToText $Finding[$key]
                    if (-not [string]::IsNullOrWhiteSpace($text)) {
                        return $text
                    }
                }
            }
        }

        return $null
    }

    foreach ($propertyName in $PropertyNames) {
        $property = $Finding.PSObject.Properties | Where-Object { $_.Name -ieq $propertyName } | Select-Object -First 1
        if ($null -ne $property) {
            $text = Convert-ReviewFieldToText $property.Value
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                return $text
            }
        }
    }

    return $null
}

function Get-ReviewObjectMemberNames([object]$Value) {
    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Collections.IDictionary]) {
        return @($Value.Keys | ForEach-Object { [string]$_ })
    }

    if ($null -eq $Value.PSObject) {
        return @()
    }

    return @($Value.PSObject.Properties | ForEach-Object { $_.Name })
}

function Normalize-ReviewFinding {
    param(
        [Parameter(Mandatory = $true)][object]$Finding,
        [Parameter(Mandatory = $true)][string]$ExpectedPass,
        [Parameter(Mandatory = $true)][int]$Index
    )

    if ($null -eq $Finding -or $null -eq $Finding.PSObject) {
        throw "Nvidia returned a malformed finding entry for $ExpectedPass."
    }

    $severity = Get-ReviewFindingTextValue -Finding $Finding -PropertyNames @('severity', 'level', 'priority')
    if ([string]::IsNullOrWhiteSpace($severity)) {
        throw "Nvidia returned an incomplete finding for ${ExpectedPass}: missing severity."
    }

    $severity = $severity.ToUpperInvariant()
    switch ($severity) {
        'CRITICAL' { $severity = 'BLOCKER' }
        'SEVERE' { $severity = 'HIGH' }
        'BLOCKER' { }
        'HIGH' { }
        'MEDIUM' { return $null }
        'LOW' { return $null }
        'INFO' { return $null }
        'WARNING' { return $null }
        'MINOR' { return $null }
        default { throw "Nvidia returned an unsupported severity '$severity' for $ExpectedPass." }
    }

    $id = Get-ReviewFindingTextValue -Finding $Finding -PropertyNames @('id', 'finding_id', 'key')
    if ([string]::IsNullOrWhiteSpace($id)) {
        $id = "$ExpectedPass-FINDING-$Index"
    }

    $problem = Get-ReviewFindingTextValue -Finding $Finding -PropertyNames @('problem', 'issue', 'summary', 'description', 'finding')
    $evidence = Get-ReviewFindingTextValue -Finding $Finding -PropertyNames @('evidence', 'details', 'rationale', 'excerpt')
    $requiredOutcome = Get-ReviewFindingTextValue -Finding $Finding -PropertyNames @('required_outcome', 'requiredOutcome', 'remediation', 'fix', 'resolution')

    $category = Get-ReviewFindingTextValue -Finding $Finding -PropertyNames @('category', 'type', 'area')
    if ([string]::IsNullOrWhiteSpace($category)) {
        $category = 'unspecified'
    }

    $requirement = Get-ReviewFindingTextValue -Finding $Finding -PropertyNames @('requirement', 'contract', 'rule')
    if ([string]::IsNullOrWhiteSpace($requirement)) {
        $requirement = 'Reviewer omitted an explicit requirement field; inspect the cited issue directly.'
    }

    $location = Get-ReviewFindingTextValue -Finding $Finding -PropertyNames @('location', 'path', 'file', 'target')
    if ([string]::IsNullOrWhiteSpace($location)) {
        $location = 'unspecified'
    }

    if ([string]::IsNullOrWhiteSpace($problem)) {
        $problem = 'Reviewer flagged a serious issue but omitted a structured problem field.'
    }

    if ([string]::IsNullOrWhiteSpace($evidence)) {
        $evidence = $problem
    }

    if ([string]::IsNullOrWhiteSpace($requiredOutcome)) {
        $requiredOutcome = 'Clarify and resolve the cited serious issue before promotion.'
    }

    return [pscustomobject]@{
        id = $id
        severity = $severity
        category = $category
        requirement = $requirement
        location = $location
        problem = $problem
        evidence = $evidence
        required_outcome = $requiredOutcome
    }
}

function ConvertFrom-ReviewJson([string]$Content, [string]$ExpectedPass) {
    try { $parsed = $Content | ConvertFrom-Json -ErrorAction Stop }
    catch { throw "Nvidia returned malformed JSON for $ExpectedPass." }

    if ($null -eq $parsed -or $parsed -is [string] -or $null -eq $parsed.PSObject) {
        throw "Nvidia returned an unexpected JSON root for $ExpectedPass."
    }

    $propertyNames = @(Get-ReviewObjectMemberNames $parsed)
    $passName = Convert-ReviewFieldToText $parsed.pass
    if ([string]::IsNullOrWhiteSpace($passName) -or
        -not [string]::Equals($passName, $ExpectedPass, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Nvidia returned a mismatched pass marker for $ExpectedPass."
    }

    $hasFindings = $propertyNames -contains 'findings'
    $hasBlockingFindings = $propertyNames -contains 'blocking_findings'
    if ($parsed.review_complete -isnot [bool]) {
        throw "Nvidia returned a non-boolean review_complete field for $ExpectedPass."
    }

    if ($propertyNames -notcontains 'review_complete' -or
        (-not $hasFindings -and -not $hasBlockingFindings) -or -not $parsed.review_complete) {
        throw "Nvidia returned incomplete JSON for $ExpectedPass."
    }

    if ([string]::Equals($ExpectedPass, 'CONSOLIDATION', [StringComparison]::OrdinalIgnoreCase) -and
        $propertyNames -notcontains 'root_cause_groups') {
        throw 'Nvidia consolidation JSON omitted root_cause_groups.'
    }

    $rawFindings = if ($hasBlockingFindings) { $parsed.blocking_findings } else { $parsed.findings }
    if ($null -eq $rawFindings) {
        $reviewFindings = @()
    }
    elseif ($rawFindings -is [string]) {
        throw "Nvidia returned a non-array findings payload for $ExpectedPass."
    }
    elseif ($rawFindings -is [System.Collections.IDictionary] -or $rawFindings -is [pscustomobject]) {
        $findingPropertyNames = @(Get-ReviewObjectMemberNames $rawFindings)
        if ($findingPropertyNames.Count -eq 0) {
            $reviewFindings = @()
        }
        elseif ($findingPropertyNames -contains 'severity' -or $findingPropertyNames -contains 'id') {
            $reviewFindings = @($rawFindings)
        }
        else {
            throw "Nvidia returned a non-array findings payload for $ExpectedPass."
        }
    }
    else {
        $reviewFindings = @($rawFindings)
    }

    $normalizedFindings = @()
    for ($index = 0; $index -lt $reviewFindings.Count; $index++) {
        $findingCandidate = $reviewFindings[$index]
        if ($null -eq $findingCandidate) {
            throw "Nvidia returned a null finding for ${ExpectedPass} at index $($index + 1)."
        }

        if ((-not ($findingCandidate -is [System.Collections.IDictionary])) -and
            (-not ($findingCandidate -is [pscustomobject]))) {
            throw "Nvidia returned a non-object finding for ${ExpectedPass} at index $($index + 1)."
        }

        try {
            $normalizedFinding = Normalize-ReviewFinding -Finding $findingCandidate -ExpectedPass $ExpectedPass -Index ($index + 1)
            if ($null -ne $normalizedFinding) {
                $normalizedFindings += $normalizedFinding
            }
        }
        catch {
            $rawFinding = Convert-ReviewFieldToText $findingCandidate
            if ([string]::IsNullOrWhiteSpace($rawFinding)) {
                $rawFinding = '<unserializable finding payload>'
            }

            throw "Nvidia returned an invalid finding for ${ExpectedPass} at index $($index + 1): $($_.Exception.Message) Raw payload: $rawFinding"
        }
    }

    if ($hasBlockingFindings) {
        $parsed.blocking_findings = $normalizedFindings
    }
    else {
        $parsed.findings = $normalizedFindings
    }

    if ($propertyNames -contains 'root_cause_groups') {
        $rootCauseGroups = $parsed.root_cause_groups
        if ($null -eq $rootCauseGroups) {
            $parsed.root_cause_groups = @()
        }
        elseif ($rootCauseGroups -is [string]) {
            throw "Nvidia returned a non-array root_cause_groups payload for $ExpectedPass."
        }
        elseif ($rootCauseGroups -is [System.Collections.IDictionary] -or $rootCauseGroups -is [pscustomobject]) {
            $parsed.root_cause_groups = @($rootCauseGroups)
        }
        else {
            $parsed.root_cause_groups = @($rootCauseGroups)
        }
    }

    return $parsed
}

function Get-SanitizedNvidiaErrorMessage([object]$Exception) {
    $message = [string]$Exception.Message
    if ([string]::IsNullOrWhiteSpace($message)) {
        return 'Nvidia request failed without an error message.'
    }

    return Redact-LikelySecretsInText -Text $message
}

function Get-SanitizedNvidiaExceptionSummary([object]$Exception) {
    $parts = New-Object System.Collections.Generic.List[string]
    $current = $Exception
    $depth = 0
    while ($null -ne $current -and $depth -lt 6) {
        $message = Get-SanitizedNvidiaErrorMessage $current
        if (-not [string]::IsNullOrWhiteSpace($message)) {
            [void]$parts.Add(("{0}: {1}" -f $current.GetType().Name, $message))
        }

        $current = $current.InnerException
        $depth++
    }

    if ($parts.Count -eq 0) {
        return 'No exception detail was available.'
    }

    return (($parts -join ' | ') -replace '\s+', ' ').Trim()
}

function Write-AtomicTextFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $tempPath = Join-Path $directory ([IO.Path]::GetRandomFileName())
    try {
        Set-Content -LiteralPath $tempPath -Value $Content -Encoding UTF8
        Move-Item -LiteralPath $tempPath -Destination $Path -Force
    }
    finally {
        Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-NvidiaRequestWithSpacing {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Request,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds
    )

    if ([string]::IsNullOrWhiteSpace($script:NvidiaSpacingRoot)) {
        throw 'Nvidia spacing root was not initialized before request dispatch.'
    }

    $spacingPath = Join-Path $script:NvidiaSpacingRoot 'last-harness-response-at.txt'
    $mutexName = if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        # Windows named mutexes use the Global\ prefix to synchronize across
        # sessions on the same host. Other platforms use the plain name.
        'Global\DoNotPanicPortfolioVisualizer.NvidiaReviewHarness.ResponseSpacing'
    }
    else {
        'DoNotPanicPortfolioVisualizer.NvidiaReviewHarness.ResponseSpacing'
    }

    $mutex = New-Object System.Threading.Mutex($false, $mutexName)
    $lockTaken = $false
    $observedResponse = $false

    try {
        try {
            $lockTaken = $mutex.WaitOne([TimeSpan]::FromSeconds([Math]::Min(120, [Math]::Max(30, $TimeoutSeconds / 2))))
            if (-not $lockTaken) {
                throw 'Timed out waiting for the Nvidia response-spacing mutex.'
            }
        }
        catch [System.Threading.AbandonedMutexException] {
            # WaitOne grants ownership to the current thread after an abandoned
            # mutex so the caller can safely repair and release it.
            $lockTaken = $true
        }

        if (Test-Path -LiteralPath $spacingPath) {
            try {
                $lastResponseText = (Get-Content -Raw -LiteralPath $spacingPath -ErrorAction Stop).Trim()
                if (-not [string]::IsNullOrWhiteSpace($lastResponseText)) {
                    $lastResponse = [DateTimeOffset]::Parse($lastResponseText)
                    $elapsedSeconds = ([DateTimeOffset]::UtcNow - $lastResponse).TotalSeconds
                    $remainingSeconds = [Math]::Ceiling($script:MinimumNvidiaResponseSpacingSeconds - $elapsedSeconds)
                    if ($remainingSeconds -gt 0) {
                        Start-Sleep -Seconds $remainingSeconds
                    }
                }
            }
            catch {
                Start-Sleep -Seconds $script:MinimumNvidiaResponseSpacingSeconds
            }
        }

        try {
            $response = & $Request
            $observedResponse = $true
            return $response
        }
        catch {
            if ($null -ne (Get-TransientStatus $_.Exception)) {
                $observedResponse = $true
            }

            throw
        }
        finally {
            if ($observedResponse) {
                $script:LastNvidiaResponseAt = [DateTimeOffset]::UtcNow
                try {
                    Write-AtomicTextFile -Path $spacingPath -Content ($script:LastNvidiaResponseAt.ToString('o'))
                }
                catch {
                    Write-Warning 'Could not update Nvidia response-spacing timestamp.'
                }
            }
        }
    }
    finally {
        try {
            if ($lockTaken) {
                try {
                    $mutex.ReleaseMutex()
                }
                catch [System.ApplicationException] {
                }
                catch [System.Threading.SynchronizationLockException] {
                }
            }
        }
        finally {
            $mutex.Dispose()
        }
    }
}

function Test-IsNvidiaModel([string]$TargetModel) {
    return -not [string]::IsNullOrWhiteSpace($TargetModel) -and
        $TargetModel.StartsWith('nvidia/', [StringComparison]::OrdinalIgnoreCase)
}

function New-NvidiaHarnessRequestBody {
    param(
        [Parameter(Mandatory = $true)][string]$System,
        [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$TargetModel,
        [Parameter(Mandatory = $true)][int]$TokenLimit
    )

    $body = [ordered]@{
        model = $TargetModel
        messages = @(@{ role = 'system'; content = $System }, @{ role = 'user'; content = $User })
        temperature = 0.1
        response_format = @{ type = 'json_object' }
        max_tokens = $TokenLimit
        stream = $false
    }

    if (Test-IsNvidiaModel $TargetModel) {
        # Nvidia V4 can spend the output budget in reasoning/thinking content
        # and leave message.content empty or truncated for our JSON contract.
        # Disable thinking for review-harness requests so the gate returns a
        # bounded JSON verdict reliably on larger packets.
        $body['chat_template_kwargs'] = @{ enable_thinking = $false }
    }

    return $body | ConvertTo-Json -Depth 10 -Compress
}

function Invoke-HarnessSelfTest {
    try {
        $deepSeekBodyProbe = New-NvidiaHarnessRequestBody -System 'self-test system' -User 'self-test user' -TargetModel 'nvidia/nemotron-3-ultra-550b-a55b' -TokenLimit 16 |
            ConvertFrom-Json -ErrorAction Stop
        $genericBodyProbe = New-NvidiaHarnessRequestBody -System 'self-test system' -User 'self-test user' -TargetModel 'generic-model' -TokenLimit 16 |
            ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "Nvidia review harness self-test failed; could not parse request body JSON: $($_.Exception.Message)"
    }

    if ($deepSeekBodyProbe.chat_template_kwargs.enable_thinking -ne $false) {
        throw 'Nvidia review harness self-test failed; Nvidia request body does not disable thinking mode.'
    }

    if ($deepSeekBodyProbe.PSObject.Properties.Name -contains 'reasoning_effort') {
        throw 'Nvidia review harness self-test failed; request body unexpectedly preserved reasoning_effort while thinking is disabled.'
    }

    if ($deepSeekBodyProbe.temperature -ne 0.1 -or $deepSeekBodyProbe.max_tokens -ne 16) {
        throw 'Nvidia review harness self-test failed; Nvidia request body has an unexpected shape.'
    }

    if ($genericBodyProbe.PSObject.Properties.Name -contains 'chat_template_kwargs') {
        throw 'Nvidia review harness self-test failed; generic request body unexpectedly includes Nvidia thinking controls.'
    }

    try {
        $normalizedProbe = ConvertFrom-ReviewJson -Content '{"pass":"CODE-A","review_complete":true,"findings":{"severity":"HIGH","problem":"Missing null guard."}}' -ExpectedPass 'CODE-A'
        $arrayProbe = ConvertFrom-ReviewJson -Content '{"pass":"CODE-A","review_complete":true,"findings":[{"id":"A-1","severity":"BLOCKER","problem":"Broken contract.","location":"src/File.cs"}]}' -ExpectedPass 'CODE-A'
        $blockingProbe = ConvertFrom-ReviewJson -Content '{"pass":"CONSOLIDATION","review_complete":true,"blocking_findings":[{"severity":"HIGH","problem":"Missing evidence field."}],"root_cause_groups":[]}' -ExpectedPass 'CONSOLIDATION'
    }
    catch {
        throw "Nvidia review harness self-test failed; partial finding normalization raised an error: $($_.Exception.Message)"
    }

    if (@($normalizedProbe.findings).Count -ne 1) {
        throw 'Nvidia review harness self-test failed; partial finding normalization did not preserve the finding.'
    }

    $normalizedFinding = @($normalizedProbe.findings)[0]
    if ($normalizedFinding.id -ne 'CODE-A-FINDING-1' -or
        $normalizedFinding.severity -ne 'HIGH' -or
        $normalizedFinding.problem -ne 'Missing null guard.' -or
        [string]::IsNullOrWhiteSpace([string]$normalizedFinding.required_outcome)) {
        throw 'Nvidia review harness self-test failed; partial finding normalization produced an unexpected shape.'
    }

    if (@($arrayProbe.findings).Count -ne 1 -or @($blockingProbe.blocking_findings).Count -ne 1) {
        throw 'Nvidia review harness self-test failed; array or blocking-findings normalization did not preserve the expected finding count.'
    }

    $filteredSeverityProbe = ConvertFrom-ReviewJson -Content '{"pass":"CODE-A","review_complete":true,"findings":{"severity":"MEDIUM","problem":"Wrong severity."}}' -ExpectedPass 'CODE-A'
    if (@($filteredSeverityProbe.findings).Count -ne 0) {
        throw 'Nvidia review harness self-test failed; non-blocking severities were not filtered out.'
    }

    try {
        $null = ConvertFrom-ReviewJson -Content '{"pass":"CODE-A","review_complete":true,"findings":{"foo":"bar"}}' -ExpectedPass 'CODE-A'
        throw 'Nvidia review harness self-test failed; malformed findings payload was accepted unexpectedly.'
    }
    catch {
        if ($_.Exception.Message -notlike 'Nvidia returned a non-array findings payload*') {
            throw
        }
    }

    Write-Output 'NVIDIA_REVIEW_HARNESS_SELFTEST=Passed'
}

function Assert-GitIgnoredPath([string]$Path, [string]$FailureMessage) {
    if ($null -eq (Get-Command git -ErrorAction SilentlyContinue)) {
        throw "git is required to verify ignored review-harness paths: $Path"
    }

    & git check-ignore -q -- $Path
    $exitCode = $global:LASTEXITCODE

    switch ($exitCode) {
        0 { return }
        1 { throw $FailureMessage }
        default { throw "git check-ignore failed with exit code $exitCode while validating ignored path: $Path" }
    }
}

function Test-IsTransientHttpStatus($Status) {
    if ($null -eq $Status) {
        return $false
    }

    return $Status -in @(404, 408, 425, 429) -or $Status -ge 500
}

function Test-IsRetryableReviewException([object]$Exception, $Status) {
    if (Test-IsTransientHttpStatus $Status) {
        return $true
    }

    for ($current = $Exception; $null -ne $current; $current = $current.InnerException) {
        if ($current -is [System.Net.Http.HttpRequestException] -or
            $current -is [System.Net.Sockets.SocketException] -or
            $current -is [System.IO.IOException] -or
            $current -is [TimeoutException] -or
            $current -is [System.Threading.Tasks.TaskCanceledException] -or
            $current -is [System.Net.WebException]) {
            return $true
        }
    }

    return $false
}

function Test-IsRetryableHarnessFailureMessage([string]$Message) {
    if ([string]::IsNullOrWhiteSpace($Message)) {
        return $false
    }

    return $Message -like 'Nvidia response was absent or truncated.*' -or
        $Message -like 'Nvidia response was absent.*' -or
        $Message -like 'Nvidia response content was empty.*' -or
        $Message -like 'Nvidia response was missing finish_reason.*' -or
        $Message -like '*property ''choices'' cannot be found*' -or
        $Message -like '*property ''message'' cannot be found*'
}

function Get-TransientRetryDelaySeconds([int]$AttemptIndex) {
    $boundedIndex = [Math]::Max(0, [Math]::Min($AttemptIndex, 4))
    $jitterSeconds = Get-Random -Minimum 0 -Maximum 4
    return [int][Math]::Min(60, ([Math]::Pow(2, $boundedIndex) * 5) + $jitterSeconds)
}

function Invoke-NvidiaJsonRequest {
    param([Parameter(Mandatory = $true)][string]$System, [Parameter(Mandatory = $true)][string]$User,
        [Parameter(Mandatory = $true)][string]$ApiKey, [Parameter(Mandatory = $true)][string]$TargetEndpoint,
        [Parameter(Mandatory = $true)][string]$TargetModel, [Parameter(Mandatory = $true)][int]$TokenLimit,
        [Parameter(Mandatory = $true)][int]$TimeoutSeconds)

    $body = New-NvidiaHarnessRequestBody -System $System -User $User -TargetModel $TargetModel -TokenLimit $TokenLimit
    if ([Text.Encoding]::UTF8.GetByteCount($body) -gt $MaxRequestBytes) { throw 'Review request exceeds MaxRequestBytes; split the reviewed material into coherent shards before retrying.' }

    $retryDelays = @(0, 1, 2, 3)
    for ($attempt = 0; $attempt -le $retryDelays.Count; $attempt++) {
        try {
            $response = Invoke-NvidiaRequestWithSpacing -TimeoutSeconds $TimeoutSeconds -Request {
                Invoke-RestMethod -Method Post -Uri ($TargetEndpoint.TrimEnd('/') + '/chat/completions') -Headers @{ Authorization = "Bearer $ApiKey"; 'Content-Type' = 'application/json' } -Body $body -TimeoutSec $TimeoutSeconds
            }
            $choice = @($response.choices)[0]
            if ($null -eq $choice) { throw 'Nvidia response was absent.' }

            $finishReason = Convert-ReviewFieldToText $choice.finish_reason
            if ([string]::IsNullOrWhiteSpace($finishReason)) {
                throw 'Nvidia response was missing finish_reason.'
            }

            if (-not [string]::Equals($finishReason, 'stop', [StringComparison]::OrdinalIgnoreCase)) {
                if ([string]::Equals($finishReason, 'length', [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'Nvidia response was absent or truncated.'
                }

                throw "Nvidia response finished with unsupported reason '$finishReason'."
            }

            $content = [string]$choice.message.content
            if ([string]::IsNullOrWhiteSpace($content)) { throw 'Nvidia response content was empty.' }
            return @{ Content = $content; Usage = $response.usage; Attempts = $attempt + 1 }
        }
        catch {
            $status = Get-TransientStatus $_.Exception
            $safeMessage = Get-SanitizedNvidiaErrorMessage $_.Exception
            $shouldRetry = ((Test-IsRetryableReviewException -Exception $_.Exception -Status $status) -or
                (Test-IsRetryableHarnessFailureMessage -Message $safeMessage)) -and $attempt -lt $retryDelays.Count
            if (-not $shouldRetry) {
                $statusText = if ($null -ne $status) { "status=$status; " } else { [string]::Empty }
                throw "Nvidia review request failed after $($attempt + 1) attempt(s): ${statusText}error=$($_.Exception.GetType().Name). See ignored Nvidia telemetry for local diagnostics."
            }
            Start-Sleep -Seconds (Get-TransientRetryDelaySeconds -AttemptIndex $attempt)
        }
    }
    throw 'Nvidia review retry exhaustion.'
}

switch ($PSCmdlet.ParameterSetName) {
    'SelfTest' {
        Invoke-HarnessSelfTest
    }
    'Review' { }
    default { throw "Unsupported parameter set: $($PSCmdlet.ParameterSetName)" }
}

if ($PSCmdlet.ParameterSetName -ne 'Review') {
    return
}

$repoRoot = Get-RepoRoot
$materialCandidate = if ([IO.Path]::IsPathRooted($ReviewMaterialPath)) { $ReviewMaterialPath } else { Join-Path $repoRoot $ReviewMaterialPath }
$resolvedMaterialPath = [IO.Path]::GetFullPath($materialCandidate)
$resolvedRepoRoot = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\', '/')
$repoRootWithSeparator = $resolvedRepoRoot + [IO.Path]::DirectorySeparatorChar
if (-not $resolvedMaterialPath.StartsWith($repoRootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) { throw 'Review material must resolve under the repository root.' }
if (-not (Test-Path -LiteralPath $resolvedMaterialPath -PathType Leaf)) { throw "Review material is not a file: $ReviewMaterialPath" }
$material = Get-Content -Raw -LiteralPath $resolvedMaterialPath
Assert-NoLikelySecrets -Text $material
$head = (& git rev-parse HEAD 2>$null).Trim()
$materialHash = (Get-FileHash -LiteralPath $resolvedMaterialPath -Algorithm SHA256).Hash.ToLowerInvariant()
$snapshotId = "${head}:$materialHash"
$outputCandidate = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repoRoot $OutputDirectory }
$outputRoot = [IO.Path]::GetFullPath($outputCandidate)
if (-not $outputRoot.StartsWith($repoRootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) { throw 'OutputDirectory must resolve under the repository root.' }
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$script:NvidiaSpacingRoot = $outputRoot
$relativeOutputDirectory = $outputRoot.Substring($repoRootWithSeparator.Length).Replace('\', '/').TrimEnd('/')
$relativeTelemetryPath = $outputRoot.Substring($repoRootWithSeparator.Length).Replace('\', '/').TrimEnd('/') + '/telemetry.jsonl'
Push-Location $repoRoot
try {
    Assert-GitIgnoredPath $relativeOutputDirectory 'Nvidia harness output directory must be ignored by git.'
    Assert-GitIgnoredPath $relativeTelemetryPath 'Nvidia harness output directory must be ignored by git.'
}
finally { Pop-Location }

$Endpoint = Get-ValidatedNvidiaEndpoint -Endpoint $Endpoint
if (-not $Endpoint.Equals('https://integrate.api.nvidia.com/v1', [StringComparison]::OrdinalIgnoreCase) -and -not $AcknowledgeEndpointOverride) { throw 'Endpoint override requires -AcknowledgeEndpointOverride.' }
$apiKey = Get-NvidiaApiKey -RepositoryRoot $repoRoot
$sharedSystem = "You are an independent strict $ReviewType reviewer. Return JSON only. Review the entire supplied immutable snapshot, continue after every finding, silently self-challenge before responding, and report only high-confidence BLOCKER or HIGH issues. Exclude praise, style, cosmetic refactoring, tutorials, and speculative redesign. Do not expose reasoning. Each finding requires id, severity, category, requirement, location, problem, evidence, required_outcome. The snapshot identity is $snapshotId."
$sharedUser = "Immutable review material follows. Do not assume test success proves correctness.\n\n$material"
$started = [DateTimeOffset]::UtcNow
$results = New-Object System.Collections.Generic.List[object]
$callCount = 0
$findings = [System.Collections.Generic.List[object]]::new()
try {
    foreach ($pass in Get-ReviewPasses $ReviewType) {
        $passSystem = $sharedSystem + " Required schema for this pass: {`"pass`":`"$($pass.Id)`",`"review_complete`":true,`"findings`":[],`"uncertainties`":[]}. The pass field must be exactly `"$($pass.Id)`"."
        $response = Invoke-NvidiaJsonRequest -System $passSystem -User ($sharedUser + "\n\nPass-specific scope: " + $pass.Focus) -ApiKey $apiKey -TargetEndpoint $Endpoint -TargetModel $Model -TokenLimit $MaxTokens -TimeoutSeconds $RequestTimeoutSeconds
        $callCount += $response.Attempts
        [void]$results.Add((ConvertFrom-ReviewJson -Content $response.Content -ExpectedPass $pass.Id))
    }
    $specialistJson = $results | ConvertTo-Json -Depth 20 -Compress
    $consolidationSystem = "You are the adversarial consolidation stage for a strict $ReviewType gate. Return JSON only. Recheck each proposed BLOCKER/HIGH finding against the immutable snapshot. Remove duplicates and unsupported or stale claims, group root causes, preserve any independently valid serious finding even if only one specialist found it, and add a serious issue only with concrete evidence. Required schema: {`"pass`":`"CONSOLIDATION`",`"review_complete`":true,`"blocking_findings`":[],`"root_cause_groups`":[],`"uncertainties`":[]}. The pass field must be exactly `"CONSOLIDATION`"."
    $consolidation = Invoke-NvidiaJsonRequest -System $consolidationSystem -User ($sharedUser + "\n\nSpecialist JSON:\n" + $specialistJson) -ApiKey $apiKey -TargetEndpoint $Endpoint -TargetModel $Model -TokenLimit $MaxTokens -TimeoutSeconds $RequestTimeoutSeconds
    $callCount += $consolidation.Attempts
    $final = ConvertFrom-ReviewJson -Content $consolidation.Content -ExpectedPass 'CONSOLIDATION'
    $findings = [System.Collections.Generic.List[object]]::new()
    $finalFindings = if ($final.PSObject.Properties.Name -contains 'blocking_findings') { $final.blocking_findings } else { $final.findings }
    foreach ($finding in @($finalFindings)) {
        if ($null -ne $finding) { [void]$findings.Add($finding) }
    }
    $verdict = if ($findings.Count -eq 0) { 'PASS' } else { 'FAIL' }
    $result = [ordered]@{ schema_version = 1; review_type = $ReviewType; snapshot_id = $snapshotId; verdict = $verdict; review_complete = $true; blocking_findings = $findings; root_cause_groups = @($final.root_cause_groups); prior_findings = @() }
}
catch {
    $result = [ordered]@{ schema_version = 1; review_type = $ReviewType; snapshot_id = $snapshotId; verdict = 'REVIEW_UNAVAILABLE'; review_complete = $false; reason = 'Nvidia review could not be completed reliably. See local invocation error.' }
    $errorSummary = Get-SanitizedNvidiaExceptionSummary $_.Exception
    Write-ReviewTelemetry -Root $outputRoot -Record @{ timestamp = [DateTimeOffset]::UtcNow.ToString('o'); review_type = $ReviewType; snapshot_id = $snapshotId; calls = $callCount; verdict = 'REVIEW_UNAVAILABLE'; error_class = $_.Exception.GetType().Name; error_status = (Get-TransientStatus $_.Exception); error_summary = $errorSummary }
    throw ([System.Exception]::new(("Nvidia review failed. See ignored telemetry for local diagnostics. " + $errorSummary), $_.Exception))
}

Write-ReviewTelemetry -Root $outputRoot -Record @{ timestamp = [DateTimeOffset]::UtcNow.ToString('o'); review_type = $ReviewType; snapshot_id = $snapshotId; calls = $callCount; specialist_passes = @((Get-ReviewPasses $ReviewType | ForEach-Object Id)); final_serious_finding_count = $findings.Count; final_verdict = $result.verdict; elapsed_ms = ([DateTimeOffset]::UtcNow - $started).TotalMilliseconds }
$result | ConvertTo-Json -Depth 20 -Compress

