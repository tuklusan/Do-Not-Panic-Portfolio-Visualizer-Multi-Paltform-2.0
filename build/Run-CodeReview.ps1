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
    [Parameter(Mandatory)][ValidateSet('CODE','DOCUMENTATION','TEST_ARTIFACT')][string]$ReviewType,
    [string]$RepositoryRoot,
    [string]$BaseSha,
    [Parameter(Mandatory)][string]$HeadSha,
    [Parameter(Mandatory)][string]$Scope,
    [string]$Requirement,
    [string]$RequirementPath,
    [string[]]$ContextPath = @(),
    [string]$ContextSpecPath,
    [string]$ReviewMaterialPath,
    [string]$OutputDirectory,
    [ValidateRange(60,14400)][int]$ReviewTimeoutSeconds = 1800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try {
    $orchestrator = [IO.Path]::Combine($PSScriptRoot,'Invoke-CodeReviewHarness.ps1')
    $arguments = @{
        Operation = 'Review'; ReviewType = $ReviewType; HeadSha = $HeadSha; Scope = $Scope
        ReviewTimeoutSeconds = $ReviewTimeoutSeconds
    }
    foreach ($name in @('RepositoryRoot','BaseSha','Requirement','RequirementPath','ContextSpecPath',
        'ReviewMaterialPath','OutputDirectory')) {
        if ($PSBoundParameters.ContainsKey($name)) { $arguments[$name] = $PSBoundParameters[$name] }
    }
    if ($ContextPath.Count -gt 0) { $arguments.ContextPath = $ContextPath }
    $results = @(& $orchestrator @arguments)
    if ($results.Count -ne 1 -or $results[0].schema -cne 'dnppv2-review-result/v3' -or
        $results[0].verdict -notin @('PASS','FAIL','INCONCLUSIVE')) {
        throw 'Provider-neutral orchestrator returned no single typed result.'
    }
    $json = ConvertTo-Json -InputObject $results[0] -Depth 100 -Compress
    [Console]::Out.WriteLine($json)
    exit 0
}
catch {
    $class = $_.Exception.Data['DNPPVFailureClass']
    if (-not $class -or $class -notmatch '^[A-Z][A-Z0-9_]{2,63}$') { $class = 'LOCAL_UNEXPECTED_FAILURE' }
    $transaction = $_.Exception.Data['DNPPVTransactionPath']
    if ($transaction -isnot [string] -or $transaction -notmatch '^(build/code-review|artifacts/.+/review)/transactions/[0-9a-f]{40}/[0-9a-f]{64}/[0-9a-f]{32}/transaction\.json$') {
        $transaction = $null
    }
    $message = 'Review infrastructure failed; inspect the retained transaction evidence.'
    $conflictingIds = $_.Exception.Data['DNPPVConflictingTransactionIds']
    if ($class -ceq 'LOCAL_PERSISTENCE_FAILURE' -and $conflictingIds -is [string[]] -and
        $conflictingIds.Count -gt 1 -and @($conflictingIds | Where-Object { $_ -cnotmatch '^[0-9a-f]{32}$' }).Count -eq 0) {
        $message = 'Multiple nonterminal same-packet transactions exist: ' + ($conflictingIds -join ', ') + '.'
    }
    $errorObject = [ordered]@{
        schema = 'dnppv2-review-error/v1'
        failure_class = $class
        transaction_path = $transaction
        message = $message
    }
    [Console]::Error.WriteLine((ConvertTo-Json -InputObject $errorObject -Depth 10 -Compress))
    exit 1
}
