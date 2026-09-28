# Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
<#
============================================================================
DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
============================================================================
#>
[CmdletBinding()]
param(
    [string]$UpstreamRef = 'upstream',
    [string]$LedgerPath = 'docs/UPSTREAM-2.0-GAP-LEDGER.md'
)
$ErrorActionPreference = 'Stop'
$files = @(& git ls-tree -r --name-only $UpstreamRef)
$ledger = Get-Content -LiteralPath $LedgerPath -Raw
$rows = [regex]::Matches($ledger, '(?m)^\| `([^`]+)` \| (\d+) \| (MAPPED|REPLACED|RETIRED|GAP) \|')
$rowPaths = @($rows | ForEach-Object { $_.Groups[1].Value })
$missingRows = @($files | Where-Object { $_ -notin $rowPaths })
$extraRows = @($rowPaths | Where-Object { $_ -notin $files })
$zeroLineReads = 0
foreach ($file in $files) {
    $content = @(& git show "$UpstreamRef`:$file")
    foreach ($line in $content) { [void]$line.Length }
    if ($content.Count -eq 0) { $zeroLineReads++ }
}
$unresolved = @($rows | Where-Object { $_.Groups[3].Value -eq 'GAP' })
if ($missingRows.Count -or $extraRows.Count -or $zeroLineReads -or $unresolved.Count) {
    throw "UPSTREAM_GAP_SCAN=GAPS_FOUND;MISSING_ROWS=$($missingRows.Count);EXTRA_ROWS=$($extraRows.Count);EMPTY_FILES=$zeroLineReads;UNRESOLVED=$($unresolved.Count)"
}
Write-Output "UPSTREAM_GAP_SCAN=ZERO_GAPS;FILES=$($files.Count);LEDGER_ROWS=$($rows.Count);LINE_BY_LINE_READS=$($files.Count);UNRESOLVED=0"
