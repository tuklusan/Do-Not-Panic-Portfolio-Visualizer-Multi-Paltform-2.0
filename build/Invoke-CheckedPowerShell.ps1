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
[CmdletBinding(DefaultParameterSetName = 'Execute')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Execute')]
    [ValidateNotNullOrEmpty()]
    [string]$CommandText,

    [Parameter(ParameterSetName = 'Execute')]
    [switch]$AllowCmdShell,

    [Parameter(ParameterSetName = 'Execute')]
    [switch]$AllowNonZeroNativeExitCode,

    [Parameter(ParameterSetName = 'Execute')]
    [switch]$EchoCommand,

    [Parameter(Mandatory = $true, ParameterSetName = 'SelfTest')]
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-CheckedScriptExecution {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [bool]$AllowCmd,
        [bool]$AllowNonZeroExit,
        [bool]$Echo
    )

    $syntaxChecker = Join-Path $PSScriptRoot 'Test-PowerShellSyntax.ps1'
    if (-not (Test-Path -LiteralPath $syntaxChecker -PathType Leaf)) {
        throw "Missing PowerShell syntax checker: $syntaxChecker"
    }

    & $syntaxChecker -CommandText $Text -AllowCmdShell:$AllowCmd

    if ($Echo) {
        Write-Output 'CHECKED_POWERSHELL_COMMAND_BEGIN'
        Write-Output $Text
        Write-Output 'CHECKED_POWERSHELL_COMMAND_END'
    }

    $scriptBlock = [scriptblock]::Create($Text)
    $global:LASTEXITCODE = 0
    $nativePreferenceWasDefined = Test-Path variable:global:PSNativeCommandUseErrorActionPreference
    $previousNativePreference = if ($nativePreferenceWasDefined) {
        $global:PSNativeCommandUseErrorActionPreference
    }
    else {
        $null
    }
    $nativeFailureExitCode = $null
    try {
        $global:PSNativeCommandUseErrorActionPreference = -not $AllowNonZeroExit
        & $scriptBlock
    }
    catch {
        if ($_.Exception.GetType().Name -eq 'NativeCommandExitException') {
            $nativeFailureExitCode = $global:LASTEXITCODE
        }
        else {
            throw
        }
    }
    finally {
        if ($nativePreferenceWasDefined) {
            $global:PSNativeCommandUseErrorActionPreference = $previousNativePreference
        }
        else {
            Remove-Variable PSNativeCommandUseErrorActionPreference -Scope Global -ErrorAction SilentlyContinue
        }
    }
    $nativeExitCode = if ($null -ne $nativeFailureExitCode) {
        $nativeFailureExitCode
    }
    else {
        $global:LASTEXITCODE
    }

    if (-not $AllowNonZeroExit -and $nativeExitCode -ne 0) {
        throw "Checked PowerShell command completed with native exit code $nativeExitCode."
    }

    Write-Output "CHECKED_POWERSHELL_EXECUTION=Passed;NATIVE_EXIT_CODE=$nativeExitCode"
}

function Invoke-CheckedPowerShellSelfTest {
    $probeCommand = @'
Write-Output "SELFTEST_OK"
'@

    $probeOutput = @(Invoke-CheckedScriptExecution -Text $probeCommand -AllowCmd:$false -AllowNonZeroExit:$false -Echo:$false)
    if ($probeOutput -notcontains 'SELFTEST_OK') {
        throw 'Checked PowerShell wrapper self-test failed; probe output was not observed.'
    }

    if ($probeOutput -notcontains 'CHECKED_POWERSHELL_EXECUTION=Passed;NATIVE_EXIT_CODE=0') {
        throw 'Checked PowerShell wrapper self-test failed; success marker was not observed.'
    }

    try {
        $null = Invoke-CheckedScriptExecution -Text 'cmd /c echo blocked' -AllowCmd:$false -AllowNonZeroExit:$false -Echo:$false
        throw 'Checked PowerShell wrapper self-test failed; cmd.exe hop was accepted unexpectedly.'
    }
    catch {
        if ($_.Exception.Message -notlike '*cmd.exe shell hop*') {
            throw
        }
    }

    try {
        $null = Invoke-CheckedScriptExecution -Text 'dotnet definitely-not-a-command' -AllowCmd:$false -AllowNonZeroExit:$false -Echo:$false
        throw 'Checked PowerShell wrapper self-test failed; a failing native command was accepted unexpectedly.'
    }
    catch {
        if ($_.Exception.Message -ne 'Checked PowerShell command completed with native exit code 1.') {
            throw
        }
    }
    $global:LASTEXITCODE = 0

    Write-Output 'CHECKED_POWERSHELL_SELFTEST=Passed'
}

switch ($PSCmdlet.ParameterSetName) {
    'SelfTest' {
        Invoke-CheckedPowerShellSelfTest
        return
    }
    'Execute' {
        Invoke-CheckedScriptExecution -Text $CommandText -AllowCmd:$AllowCmdShell.IsPresent -AllowNonZeroExit:$AllowNonZeroNativeExitCode.IsPresent -Echo:$EchoCommand.IsPresent
        return
    }
    default {
        throw "Unsupported parameter set: $($PSCmdlet.ParameterSetName)"
    }
}
