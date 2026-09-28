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
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('linux', 'windows')]
    [string]$Platform,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RemoteHost,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RemoteUser,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Password,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$LocalPublishDir,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$RemotePublishDir,

    [Parameter()]
    [string]$LocalArtifactRoot = (Join-Path $env:TEMP ("dnppv2-product-scene-validation-{0}-{1:yyyyMMdd-HHmmss}" -f $Platform, (Get-Date))),

    [Parameter()]
    [ValidateRange(30, 14400)]
    [int]$TimeoutSeconds = 180,

    [Parameter()]
    [ValidateRange(2, 180)]
    [int]$SceneWarmupSeconds = 30,

    [Parameter()]
    [ValidateRange(0, 240)]
    [int]$SoakDurationMinutes = 0,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$WindowsTaskName = 'DNPPV_ProductSceneValidation',

    [Parameter()]
    [switch]$DuplicateInstanceCheck,

    [Parameter()]
    [switch]$ForceNewsFailure,

    [Parameter()]
    [string]$OpenRouterApiKey = $(if ($env:DNPPV_OPENROUTER_API_KEY) { $env:DNPPV_OPENROUTER_API_KEY } elseif ($env:OPENROUTER_API_KEY) { $env:OPENROUTER_API_KEY } else { $env:OPENROUTER_AI_API_KEY }),

    [Parameter()]
    [switch]$SkipDeployment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The scene needs time to populate all four lanes and live overlays before a
# screenshot becomes visual acceptance evidence. Never shorten that readiness
# period through an individual invocation.
$SceneWarmupSeconds = [Math]::Max(30, $SceneWarmupSeconds)

$enginePath = Join-Path $PSScriptRoot 'Invoke-ConfigWindowValidation.ps1'
if (-not (Test-Path -LiteralPath $enginePath -PathType Leaf)) {
    throw "Product-scene validation engine is missing: $enginePath"
}

# Physical product evidence always uses the ordinary product shell. Fixtures
# remain available only in focused diagnostic runs and never pass through here.
$engineParameters = @{
    Platform                 = $Platform
    RemoteHost               = $RemoteHost
    RemoteUser               = $RemoteUser
    Password                 = $Password
    LocalPublishDir          = $LocalPublishDir
    RemotePublishDir         = $RemotePublishDir
    LocalArtifactRoot        = $LocalArtifactRoot
    TimeoutSeconds           = $TimeoutSeconds
    SceneWarmupSeconds       = $SceneWarmupSeconds
    SoakDurationMinutes      = $SoakDurationMinutes
    WindowsTaskName          = $WindowsTaskName
    ProductScene             = $true
    CinematicPlaybackTrace   = $true
}

if (-not [string]::IsNullOrWhiteSpace($OpenRouterApiKey)) {
    $engineParameters.OpenRouterApiKey = $OpenRouterApiKey
}

if ($DuplicateInstanceCheck.IsPresent) {
    $engineParameters.DuplicateInstanceFixture = $true
}

if ($ForceNewsFailure.IsPresent) {
    $engineParameters.ForceNewsFailure = $true
}

if ($SkipDeployment.IsPresent) {
    $engineParameters.SkipDeployment = $true
}

& $enginePath @engineParameters
Write-Output "PRODUCT_SCENE_VALIDATION=Passed;PLATFORM=$Platform;ARTIFACT_ROOT=$LocalArtifactRoot"
