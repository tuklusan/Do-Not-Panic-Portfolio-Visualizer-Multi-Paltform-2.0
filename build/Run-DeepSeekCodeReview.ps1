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
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Endpoint = "https://api.deepseek.com",
    # Project default verified against the configured DeepSeek endpoint on 2026-06-04.
    [string]$Model = "deepseek-v4-pro",
    [string]$OutputDirectory = "build/deepseek-review",
    [int]$MaxFileCharacters = 100000,
    [int]$MaxPacketCharacters = 600000,
    [int]$MaxRequestBytes = 1048576,
    [int]$MaxResponseCharacters = 1000000,
    [int]$MaxTokens = 32768,
    [int]$CleanupOlderThanDays = 7,
    [switch]$SelfTest,
    [switch]$SendForReview,
    [switch]$PacketOnly,
    [switch]$AcknowledgeSecretScan,
    [switch]$AcknowledgeEndpointOverride,
    [switch]$IncludeUntracked
)

$ErrorActionPreference = 'Stop'
$script:OutputRootForAudit = $null
$script:ReviewGateLocationPushed = $false
$script:MinimumDeepSeekSendSpacingSeconds = 20

trap {
    if ($script:ReviewGateLocationPushed) {
        Pop-Location
        $script:ReviewGateLocationPushed = $false
    }

    throw $_
}

# The default mode builds a local packet only. Passing -SendForReview sends that
# packet to the configured DeepSeek-compatible external API. Secret scanning is
# best-effort only; inspect the packet first when a change may contain confidential
# implementation details or sensitive local-only material.

function Invoke-GitLines([string[]]$Arguments) {
    $output = & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }

    return @($output)
}

function Complete-ReviewGate([int]$ExitCode) {
    if ($script:ReviewGateLocationPushed) {
        Pop-Location
        $script:ReviewGateLocationPushed = $false
    }

    exit $ExitCode
}

$deepSeekCommonPath = Join-Path $PSScriptRoot 'DeepSeekWorkflowCommon.ps1'
if (-not (Test-Path -LiteralPath $deepSeekCommonPath)) {
    throw "DeepSeek workflow common module is missing: $deepSeekCommonPath"
}
try {
    . $deepSeekCommonPath
}
catch {
    throw "Could not load DeepSeek workflow common module '$deepSeekCommonPath': $($_.Exception.Message)"
}

foreach ($requiredCommonFunction in @('Get-DeepSeekApiKey', 'Get-RepoRoot', 'Assert-NoLikelySecrets', 'Get-ValidatedDeepSeekEndpoint', 'Get-SafeDeepSeekEndpointForLog', 'Redact-LikelySecretsInText')) {
    if (-not (Get-Command $requiredCommonFunction -CommandType Function -ErrorAction SilentlyContinue)) {
        throw "DeepSeek workflow common module did not define required function '$requiredCommonFunction'."
    }
}

function Test-ProbablyTextFile([string]$Path) {
    $extension = [IO.Path]::GetExtension($Path).ToLowerInvariant()
    if ($extension -in @('.png', '.jpg', '.jpeg', '.ico', '.gif', '.bmp', '.zip', '.7z', '.exe', '.dll', '.pdb', '.bin', '.scr')) {
        return $false
    }

    try {
        $stream = [IO.File]::OpenRead($Path)
        try {
            $length = [Math]::Min(4096, [int]$stream.Length)
            $buffer = New-Object byte[] $length
            [void]$stream.Read($buffer, 0, $length)
            return -not ($buffer -contains 0)
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Test-SecretLikePath([string]$Path) {
    $normalized = $Path.Replace('\', '/')
    return $normalized -match '(?i)(secret|credential|api[-_]?key|token|password|private)' -or
           $normalized.EndsWith('test-secrets.json', [StringComparison]::OrdinalIgnoreCase)
}

function Write-SendAudit([string]$PacketPath, [string]$EndpointValue, [string]$ModelValue) {
    if ([string]::IsNullOrWhiteSpace($script:OutputRootForAudit)) {
        throw "Cannot write DeepSeek send audit before the ignored output directory is initialized."
    }

    try {
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $PacketPath).Hash
        $safeEndpoint = Get-SafeDeepSeekEndpointForLog -Endpoint $EndpointValue
        $safeModel = Redact-LikelySecretsInText -Text ([string]$ModelValue)
        $line = "{0}`tuser={1}`tbranch={2}`tendpoint={3}`tmodel={4}`tpacketSha256={5}" -f (Get-Date -Format o), [Environment]::UserName, (& git branch --show-current), $safeEndpoint, $safeModel, $hash
        Add-Content -LiteralPath (Join-Path $script:OutputRootForAudit 'send-audit.log') -Value $line -Encoding UTF8
    }
    catch {
        Write-Warning "Could not write DeepSeek send audit log: $($_.Exception.Message)"
    }
}

function Wait-DeepSeekSendSpacing {
    if ([string]::IsNullOrWhiteSpace($script:OutputRootForAudit)) {
        throw "Cannot enforce DeepSeek send spacing before the ignored output directory is initialized."
    }

    $spacingPath = Join-Path $script:OutputRootForAudit 'last-send-at.txt'
    $mutexName = if ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT) {
        'Global\DoNotPanicPortfolioVisualizer.DeepSeekReviewGate.SendSpacing'
    }
    else {
        'DoNotPanicPortfolioVisualizer.DeepSeekReviewGate.SendSpacing'
    }
    $mutex = New-Object System.Threading.Mutex($false, $mutexName)
    $lockTaken = $false

    try {
        try {
            $lockTaken = $mutex.WaitOne([TimeSpan]::FromSeconds(30))
            if (-not $lockTaken) {
                throw "Timed out waiting for DeepSeek send-spacing lock."
            }
        }
        catch [System.Threading.AbandonedMutexException] {
            Write-Warning "DeepSeek send-spacing mutex was abandoned by a prior run; continuing with the recovered lock."
            $lockTaken = $true
        }

        if (Test-Path -LiteralPath $spacingPath) {
            try {
                $lastSendText = (Get-Content -Raw -LiteralPath $spacingPath -ErrorAction Stop).Trim()
                if (-not [string]::IsNullOrWhiteSpace($lastSendText)) {
                    $lastSend = [DateTimeOffset]::Parse($lastSendText)
                    $elapsedSeconds = ([DateTimeOffset]::Now - $lastSend).TotalSeconds
                    $remainingSeconds = [Math]::Ceiling($script:MinimumDeepSeekSendSpacingSeconds - $elapsedSeconds)
                    if ($remainingSeconds -gt 0) {
                        Write-Warning "DeepSeek gate spacing: waiting $remainingSeconds seconds before sending the next review message."
                        Start-Sleep -Seconds $remainingSeconds
                    }
                }
            }
            catch {
                Write-Warning "Could not read DeepSeek send-spacing timestamp; enforcing a full $script:MinimumDeepSeekSendSpacingSeconds-second delay and resetting the timestamp. $($_.Exception.Message)"
                Start-Sleep -Seconds $script:MinimumDeepSeekSendSpacingSeconds
            }
        }

        Set-Content -LiteralPath $spacingPath -Value ([DateTimeOffset]::Now.ToString('o')) -Encoding UTF8
    }
    finally {
        try {
            if ($lockTaken) {
                $mutex.ReleaseMutex()
            }
        }
        finally {
            $mutex.Dispose()
        }
    }
}

function New-DeepSeekReviewRequestBody {
    param(
        [Parameter(Mandatory = $true)][string]$ModelValue,
        [Parameter(Mandatory = $true)][string]$Packet,
        [Parameter(Mandatory = $true)][int]$MaxTokensValue,
        [switch]$DisableThinking
    )

    $request = [ordered]@{
        model = $ModelValue
        messages = @(
            @{
                role = 'system'
                content = 'You are a senior principal engineer doing a mandatory pre-commit code review. Be adversarial but fair. Prioritize concrete bugs, regressions, security/privacy/legal risks, missing tests, and maintainability hazards. Avoid generic praise. Verified project baseline as of 2026-07-16: Avalonia 12.1.0 is the current pinned patch, the cross-platform probe targets net10.0, official Avalonia 12 guidance recommends .NET 10, and successful local plus three-machine builds/runs prove this combination. Do not request generic confirmation of Avalonia 12.1.0/.NET 10 compatibility; report only a concrete changed-code API, package, RID, or target conflict.'
            },
            @{
                role = 'user'
                content = $Packet
            }
        )
        temperature = 0.1
        max_tokens = $MaxTokensValue
    }

    if ($DisableThinking) {
        # DeepSeek V4 defaults to thinking mode, which can spend the whole
        # token budget in reasoning_content and return empty message.content.
        $request['thinking'] = @{
            type = 'disabled'
        }
    }

    try {
        return $request | ConvertTo-Json -Depth 8
    }
    catch {
        throw "Failed to serialize DeepSeek review request body: $($_.Exception.Message)"
    }
}

function Redact-RemovedDiffSecretLines([string]$Text) {
    return [regex]::Replace(
        $Text,
        '(?im)^-(?!-).*?(?:api[_-]?key|secret|token|password)\s*[:=].*?$',
        '-[redacted secret-like removed diff line]')
}

function Assert-GitIgnored([string]$Path, [string]$FailureMessage) {
    & git check-ignore -q -- $Path
    $exitCode = $global:LASTEXITCODE
    switch ($exitCode) {
        0 { return }
        1 { throw $FailureMessage }
        default { throw "git check-ignore failed with exit code $exitCode while validating ignored path: $Path" }
    }
}

$repoRoot = Get-RepoRoot
Push-Location $repoRoot
$script:ReviewGateLocationPushed = $true

if (-not $PSBoundParameters.ContainsKey('Endpoint')) {
    $configuredEndpoint = [Environment]::GetEnvironmentVariable('DEEPSEEK_ENDPOINT')
    if (-not [string]::IsNullOrWhiteSpace($configuredEndpoint)) {
        $Endpoint = $configuredEndpoint
    }
}

if (-not $PSBoundParameters.ContainsKey('Model')) {
    $configuredModel = [Environment]::GetEnvironmentVariable('DEEPSEEK_MODEL')
    if (-not [string]::IsNullOrWhiteSpace($configuredModel)) {
        $Model = $configuredModel
    }
}

if ([string]::IsNullOrWhiteSpace($Endpoint)) {
    throw "DeepSeek review endpoint must not be empty."
}

if ([string]::IsNullOrWhiteSpace($Model)) {
    throw "DeepSeek review model must not be empty."
}

$Endpoint = Get-ValidatedDeepSeekEndpoint -Endpoint $Endpoint

if ($SelfTest) {
    $null = Invoke-GitLines @('version')
    $scriptText = Get-Content -Raw -LiteralPath $PSCommandPath
    $null = [ScriptBlock]::Create($scriptText)
    foreach ($requiredToken in @('$SendForReview', '$AcknowledgeSecretScan', '$AcknowledgeEndpointOverride', 'Get-DeepSeekApiKey', 'Assert-NoLikelySecrets', 'Write-SendAudit', 'Wait-DeepSeekSendSpacing', 'New-DeepSeekReviewRequestBody')) {
        if ($scriptText.IndexOf($requiredToken, [StringComparison]::Ordinal) -lt 0) {
            throw "DeepSeek review gate self-test failed; missing required token $requiredToken."
        }
    }
    $forbiddenTokens = @(
        ('AllowMissingKey' + 'Waiver'),
        ('Write-Waiver' + 'Audit'))
    foreach ($forbiddenToken in $forbiddenTokens) {
        if ($scriptText.IndexOf($forbiddenToken, [StringComparison]::Ordinal) -ge 0) {
            throw "DeepSeek review gate self-test failed; forbidden legacy token $forbiddenToken was found."
        }
    }

    $commonScriptText = Get-Content -Raw -LiteralPath $deepSeekCommonPath
    foreach ($forbiddenToken in $forbiddenTokens) {
        if ($commonScriptText.IndexOf($forbiddenToken, [StringComparison]::Ordinal) -ge 0) {
            throw "DeepSeek review gate self-test failed; forbidden legacy token $forbiddenToken was found in the common workflow module."
        }
    }
    if ($commonScriptText.IndexOf('function Get-DeepSeekApiKey', [StringComparison]::Ordinal) -lt 0) {
        throw "DeepSeek review gate self-test failed; common workflow module does not define Get-DeepSeekApiKey."
    }
    if ($commonScriptText.IndexOf('function Get-RepoRoot', [StringComparison]::Ordinal) -lt 0) {
        throw "DeepSeek review gate self-test failed; common workflow module does not define Get-RepoRoot."
    }
    if ($commonScriptText.IndexOf('function Get-ValidatedDeepSeekEndpoint', [StringComparison]::Ordinal) -lt 0) {
        throw "DeepSeek review gate self-test failed; common workflow module does not define Get-ValidatedDeepSeekEndpoint."
    }

    try {
        $deepSeekBodyProbe = New-DeepSeekReviewRequestBody -ModelValue 'deepseek-v4-flash' -Packet 'self-test packet' -MaxTokensValue 16 -DisableThinking |
            ConvertFrom-Json
        $genericBodyProbe = New-DeepSeekReviewRequestBody -ModelValue 'generic-model' -Packet 'self-test packet' -MaxTokensValue 16 |
            ConvertFrom-Json
    }
    catch {
        throw "DeepSeek review gate self-test failed; could not parse request body JSON: $($_.Exception.Message)"
    }

    if ($deepSeekBodyProbe.thinking.type -ne 'disabled') {
        throw "DeepSeek review gate self-test failed; DeepSeek request body does not disable thinking mode."
    }

    if ($deepSeekBodyProbe.model -ne 'deepseek-v4-flash' -or
        $deepSeekBodyProbe.messages.Count -ne 2 -or
        $deepSeekBodyProbe.messages[0].role -ne 'system' -or
        $deepSeekBodyProbe.messages[1].role -ne 'user' -or
        $deepSeekBodyProbe.temperature -ne 0.1 -or
        $deepSeekBodyProbe.max_tokens -ne 16) {
        throw "DeepSeek review gate self-test failed; DeepSeek request body has an unexpected shape."
    }

    if ($genericBodyProbe.PSObject.Properties.Name -contains 'thinking') {
        throw "DeepSeek review gate self-test failed; generic request body unexpectedly includes DeepSeek thinking controls."
    }

    $harnessPath = Join-Path $PSScriptRoot 'Invoke-DeepSeekReviewHarness.ps1'
    if (-not (Test-Path -LiteralPath $harnessPath)) {
        throw "DeepSeek review gate self-test failed; missing harness script: $harnessPath"
    }
    try {
        $harnessSelfTestOutput = @(& $harnessPath -SelfTest)
    }
    catch {
        throw "DeepSeek review gate self-test failed; review harness self-test raised an error: $($_.Exception.Message)"
    }
    if (-not ($harnessSelfTestOutput -contains 'DEEPSEEK_REVIEW_HARNESS_SELFTEST=Passed')) {
        throw "DeepSeek review gate self-test failed; review harness self-test did not report success."
    }

    $originalOutputRootForAudit = $script:OutputRootForAudit
    $originalMinimumSpacingSeconds = $script:MinimumDeepSeekSendSpacingSeconds
    $spacingSelfTestRoot = Join-Path ([IO.Path]::GetTempPath()) ("DeepSeekReviewGateSelfTest-" + [Guid]::NewGuid().ToString("N"))
    try {
        New-Item -ItemType Directory -Force -Path $spacingSelfTestRoot | Out-Null
        $script:OutputRootForAudit = $spacingSelfTestRoot
        $script:MinimumDeepSeekSendSpacingSeconds = 0
        Wait-DeepSeekSendSpacing
        $script:MinimumDeepSeekSendSpacingSeconds = 1
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        Wait-DeepSeekSendSpacing
        $stopwatch.Stop()
        $elapsedSeconds = $stopwatch.Elapsed.TotalSeconds
        # Allow 200 ms of test-host timer jitter; production spacing still uses the configured 20-second default.
        if ($elapsedSeconds -lt 0.8) {
            throw "DeepSeek review gate self-test failed; spacing helper did not enforce a non-zero delay."
        }
    }
    finally {
        $script:OutputRootForAudit = $originalOutputRootForAudit
        $script:MinimumDeepSeekSendSpacingSeconds = $originalMinimumSpacingSeconds
        Remove-Item -LiteralPath $spacingSelfTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    try {
        $uriCredentialProbe = 'mongodb+srv://' + 'reviewer:realistic-secret@cluster.example.invalid/db'
        Assert-NoLikelySecrets $uriCredentialProbe
        throw "DeepSeek review gate self-test failed; known URI credential pattern was not detected."
    }
    catch {
        if ($_.Exception.Message -notlike 'Potential secret material detected*') {
            throw
        }
    }

    try {
        Assert-NoLikelySecrets ("API_KEY=`"" + "sk-" + "selftestsecretpattern1234567890`"")
        throw "DeepSeek review gate self-test failed; known secret pattern was not detected."
    }
    catch {
        if ($_.Exception.Message -notlike 'Potential secret material detected*') {
            throw
        }
    }

    try {
        $connectionProbe = 'ConnectionString="' + 'Server=db;User Id=prod;Pass' + 'word=realistic-secret;"'
        Assert-NoLikelySecrets $connectionProbe
        throw "DeepSeek review gate self-test failed; known connection string pattern was not detected."
    }
    catch {
        if ($_.Exception.Message -notlike 'Potential secret material detected*') {
            throw
        }
    }

    Write-Output "DeepSeek review gate self-test passed."
    Complete-ReviewGate 0
}

$statusLines = Invoke-GitLines @('status', '--porcelain')
$changedFiles = New-Object System.Collections.Generic.HashSet[string]
$untrackedFiles = New-Object System.Collections.Generic.HashSet[string]
$statusEntries = Invoke-GitLines @('status', '--porcelain=1', '--untracked-files=all')
foreach ($entry in $statusEntries) {
    if ([string]::IsNullOrWhiteSpace($entry) -or $entry.Length -lt 4) {
        continue
    }

    $statusCode = $entry.Substring(0, 2)
    $pathText = $entry.Substring(3).Trim()
    if ($pathText.Contains(' -> ')) {
        $pathText = ($pathText -split ' -> ', 2)[1].Trim()
    }

    $normalizedPath = $pathText.Replace('\', '/')
    if ([string]::IsNullOrWhiteSpace($normalizedPath) -or
        $normalizedPath.StartsWith('build/deepseek-review/')) {
        continue
    }

    if ($statusCode -eq '??') {
        if ($IncludeUntracked) {
            [void]$changedFiles.Add($pathText)
            [void]$untrackedFiles.Add($pathText)
        }
    }
    else {
        [void]$changedFiles.Add($pathText)
    }
}

if ($changedFiles.Count -eq 0) {
    Write-Output "No tracked code/documentation changes found for DeepSeek review."
    Complete-ReviewGate 0
}

if ($WhatIfPreference) {
    Write-Output "WhatIf requested; no review packet was written and no DeepSeek API call was made."
    Complete-ReviewGate 0
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputRootCandidate = if ([IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $repoRoot $OutputDirectory }
$resolvedOutputRoot = [IO.Path]::GetFullPath($outputRootCandidate)
$resolvedRepoRoot = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\', '/')
$repoRootWithSeparator = $resolvedRepoRoot + [IO.Path]::DirectorySeparatorChar
if ($resolvedOutputRoot.Equals($resolvedRepoRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not $resolvedOutputRoot.StartsWith($repoRootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must resolve under the repository root."
}

$relativeOutputRoot = $resolvedOutputRoot.Substring($repoRootWithSeparator.Length).Replace('\', '/')
$outputRoot = $resolvedOutputRoot
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$script:OutputRootForAudit = $resolvedOutputRoot
$relativeOutputRootDirectory = ($relativeOutputRoot.TrimEnd('/') + '/').Replace('\', '/')
$ignoreProbePath = ($relativeOutputRoot.TrimEnd('/') + '/.deepseek-review-ignore-probe').Replace('\', '/')
Assert-GitIgnored $relativeOutputRootDirectory "DeepSeek review output directory is not ignored by git. Add build/deepseek-review/ to .gitignore before continuing."
Assert-GitIgnored $ignoreProbePath "DeepSeek review output directory probe is not ignored by git. Add build/deepseek-review/ to .gitignore before continuing."
Get-ChildItem -LiteralPath $outputRoot -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { -not $_.PSIsContainer -and $_.LastWriteTime -lt (Get-Date).AddDays(-1 * [Math]::Max(1, $CleanupOlderThanDays)) } |
    Remove-Item -Force -ErrorAction SilentlyContinue

$packetPath = Join-Path $outputRoot "deepseek-review-packet-$timestamp.txt"
$responsePath = Join-Path $outputRoot "deepseek-review-$timestamp.md"

$sections = New-Object System.Collections.Generic.List[string]
$sections.Add("# Mandatory DeepSeek code-review packet")
$sections.Add("Review the uncommitted changes in this repository before commit/push and before local or VM validation. Focus on correctness, regressions, security/privacy, reliability, UI behavior, test adequacy, and maintainability. Return findings first, ordered by severity, with exact file paths. If there are no actionable findings, say so explicitly.")
$sections.Add("# Git status")
$sections.Add(($statusLines | Out-String))
$sections.Add("# Unstaged diff")
$sections.Add(((Invoke-GitLines @('diff', '--no-ext-diff', '--unified=80')) -join "`n"))
$sections.Add("# Staged diff")
$sections.Add(((Invoke-GitLines @('diff', '--cached', '--no-ext-diff', '--unified=80')) -join "`n"))

foreach ($file in ($untrackedFiles | Sort-Object)) {
    $literalPath = Join-Path $repoRoot $file
    if (Test-SecretLikePath $file) {
        continue
    }

    if (-not (Test-Path -LiteralPath $literalPath) -or -not (Test-ProbablyTextFile $literalPath)) {
        continue
    }

    $content = Get-Content -Raw -LiteralPath $literalPath
    if ($content.Length -gt $MaxFileCharacters) {
        $truncatedCharacters = $content.Length - $MaxFileCharacters
        $content = $content.Substring(0, $MaxFileCharacters) + "`n...[truncated by Run-DeepSeekCodeReview.ps1; omitted $truncatedCharacters characters]..."
    }

    $sections.Add("# Untracked file: $file")
    $sections.Add($content)
}

$packet = Redact-RemovedDiffSecretLines ($sections -join "`n`n")
if ($packet.Length -gt $MaxPacketCharacters) {
    throw "DeepSeek review packet is $($packet.Length) characters, exceeding MaxPacketCharacters=$MaxPacketCharacters. Split the change into smaller reviewable units or rerun with an explicit larger -MaxPacketCharacters value."
}

Assert-NoLikelySecrets $packet
Write-Warning "Writing local DeepSeek review packet to $packetPath. If it contains sensitive material, delete it immediately and do not use -SendForReview."
Set-Content -LiteralPath $packetPath -Value $packet -Encoding UTF8
$relativePacketPath = (Resolve-Path -LiteralPath $packetPath -Relative).TrimStart('.', '\', '/')
Assert-GitIgnored $relativePacketPath "DeepSeek review packet is not ignored by git: $relativePacketPath. Fix .gitignore before continuing."

if ($PacketOnly -or -not $SendForReview) {
    Write-Output "DEEPSEEK_REVIEW_PACKET=$packetPath"
    Write-Output "Packet-only mode; no DeepSeek API call was made. Rerun with -SendForReview to transmit the packet."
    Complete-ReviewGate 0
}

if (-not $AcknowledgeSecretScan) {
    throw "Before using -SendForReview, inspect/redact the generated packet and rerun with -AcknowledgeSecretScan to confirm no secrets or local-only credentials are being sent externally."
}

# The shared harness owns the normal review request.  It performs all internal
# specialist and consolidation calls and returns only its compact conclusion.
$harnessPath = Join-Path $PSScriptRoot 'Invoke-DeepSeekReviewHarness.ps1'
if (-not (Test-Path -LiteralPath $harnessPath)) { throw "DeepSeek review harness is missing: $harnessPath" }
$reviewResult = & $harnessPath -ReviewType CODE -ReviewMaterialPath $packetPath -Endpoint $Endpoint -Model $Model -OutputDirectory $OutputDirectory -MaxTokens $MaxTokens -AcknowledgeEndpointOverride:$AcknowledgeEndpointOverride
if ([string]::IsNullOrWhiteSpace([string]$reviewResult)) { throw 'DeepSeek review harness did not return a compact result.' }
$reviewJson = ($reviewResult | Out-String).Trim()
try { $reviewObject = $reviewJson | ConvertFrom-Json -ErrorAction Stop }
catch { throw 'DeepSeek review harness returned malformed compact JSON.' }
if ($reviewObject.verdict -notin @('PASS', 'FAIL') -or -not [bool]$reviewObject.review_complete -or
    $reviewObject.PSObject.Properties.Name -notcontains 'blocking_findings' -or
    $reviewObject.PSObject.Properties.Name -notcontains 'root_cause_groups' -or
    $reviewObject.blocking_findings -is [string] -or $reviewObject.root_cause_groups -is [string]) { throw "DeepSeek review harness did not return a valid completed result. Verdict: $($reviewObject.verdict)" }
Set-Content -LiteralPath $responsePath -Value $reviewJson -Encoding UTF8
Write-Output "DEEPSEEK_REVIEW_PACKET=$packetPath"
Write-Output "DEEPSEEK_REVIEW_RESPONSE=$responsePath"
Write-Output "DEEPSEEK_REVIEW_RESULT=$reviewJson"
if ($reviewObject.verdict -eq 'FAIL') { Complete-ReviewGate 1 }
Complete-ReviewGate 0
