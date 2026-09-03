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
    [string]$Endpoint = "https://integrate.api.nvidia.com/v1",
    [string]$Model = "nvidia/nemotron-3-ultra-550b-a55b",
    [ValidateRange(30, 7200)][int]$TimeoutSeconds = 3600,
    [switch]$AcknowledgeEndpointOverride
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$commonPath = Join-Path $PSScriptRoot 'NvidiaWorkflowCommon.ps1'
if (-not (Test-Path -LiteralPath $commonPath)) { throw "Missing required module: $commonPath" }
. $commonPath

if (-not $PSBoundParameters.ContainsKey('Endpoint')) {
    $configuredEndpoint = [Environment]::GetEnvironmentVariable('NVIDIA_ENDPOINT')
    if (-not [string]::IsNullOrWhiteSpace($configuredEndpoint)) { $Endpoint = $configuredEndpoint }
}

if (-not $PSBoundParameters.ContainsKey('Model')) {
    $configuredModel = [Environment]::GetEnvironmentVariable('NVIDIA_MODEL')
    if (-not [string]::IsNullOrWhiteSpace($configuredModel)) { $Model = $configuredModel }
}

if ([string]::IsNullOrWhiteSpace($Endpoint)) { throw 'Nvidia endpoint must not be empty.' }
if ([string]::IsNullOrWhiteSpace($Model)) { throw 'Nvidia model must not be empty.' }
if (-not $Endpoint.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Nvidia workflow gate requires an HTTPS endpoint.'
}
$trustedDefaultEndpoint = 'https://integrate.api.nvidia.com/v1'
if (-not $Endpoint.TrimEnd('/').Equals($trustedDefaultEndpoint, [StringComparison]::OrdinalIgnoreCase) -and
    -not $AcknowledgeEndpointOverride) {
    throw "Nvidia workflow gate endpoint '$Endpoint' differs from the trusted default '$trustedDefaultEndpoint'. Rerun with -AcknowledgeEndpointOverride only if this destination is intentional."
}

$repoRoot = Get-RepoRoot
$apiKey = Get-NvidiaApiKey -RepositoryRoot $repoRoot
$uri = [Uri]::new(([string]$Endpoint).TrimEnd('/') + '/chat/completions')
Write-Output "NVIDIA_WORKFLOW_GATE_TARGET=$(([Uri]$uri).GetLeftPart([UriPartial]::Authority));MODEL=$Model"

$body = @{
    model = $Model
    messages = @(
        @{ role = 'system'; content = 'You are a workflow availability probe. Your final answer content must be exactly OK.' },
        @{ role = 'user'; content = 'Return exactly OK.' }
    )
    max_tokens = 128
    temperature = 0
    stream = $false
    # nvidia/nvidia/nemotron-3-ultra-550b-a55b may exhaust its output allowance in reasoning_content and
    # return empty final content unless thinking is disabled explicitly.
    chat_template_kwargs = @{ enable_thinking = $false }
} | ConvertTo-Json -Depth 8

$response = $null
$retryDelaysSeconds = @(5, 10, 20, 40)
for ($attempt = 1; $attempt -le ($retryDelaysSeconds.Count + 1); $attempt++) {
    try {
        $response = Invoke-RestMethod -Method Post -Uri $uri -Headers @{
            Authorization = "Bearer $apiKey"
            'Content-Type' = 'application/json'
        } -Body $body -TimeoutSec $TimeoutSeconds
        break
    }
    catch {
        $status = $null
        if ($null -ne $_.Exception.Response -and $null -ne $_.Exception.Response.StatusCode) {
            $status = [int]$_.Exception.Response.StatusCode
        }

        $isTransient = $null -eq $status -or $status -in @(404, 408, 425, 429) -or $status -ge 500
        if (-not $isTransient -or $attempt -gt $retryDelaysSeconds.Count) {
            throw "Nvidia API access is mandatory for this project's workflow, but the live access probe failed. Hard stop: do not commit, push, or run local/VM validation until Nvidia access is restored. $($_.Exception.Message)"
        }

        $delay = [int][Math]::Min(60, [Math]::Pow(2, $attempt - 1) * 5)
        Write-Warning "Nvidia workflow gate probe attempt $attempt failed with transient HTTP status $status; retrying in $delay seconds."
        Start-Sleep -Seconds $delay
    }
}

if ($null -eq $response.PSObject.Properties['choices'] -or @($response.choices).Count -eq 0) {
    throw 'Nvidia workflow gate received a response without choices. Hard stop.'
}

$choice = @($response.choices)[0]
if ($null -eq $choice) {
    throw 'Nvidia workflow gate received a response with a null first choice. Hard stop.'
}
if ($null -eq $choice.message) {
    throw 'Nvidia workflow gate received a response with no message. Hard stop.'
}
$content = [string]$choice.message.content
$finishReason = [string]$choice.finish_reason
if ([string]::IsNullOrWhiteSpace($content) -and
    [string]::IsNullOrWhiteSpace($finishReason)) {
    throw 'Nvidia workflow gate received an empty or malformed response. Hard stop.'
}
if ([string]::IsNullOrWhiteSpace($content) -or
    -not $content.Trim().Equals('OK', [StringComparison]::OrdinalIgnoreCase) -or
    -not $finishReason.Equals('stop', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Nvidia workflow gate probe returned unexpected content or finish reason. content='$content'; finish_reason='$finishReason'. Hard stop."
}

Write-Output 'NVIDIA_WORKFLOW_GATE=Passed'

