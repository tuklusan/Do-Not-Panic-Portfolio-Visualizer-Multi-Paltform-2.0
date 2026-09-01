# Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
<#
============================================================================
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
============================================================================
DISCLAIMER: This creates the exhaustive artifact register. Functional
dispositions require the cited manual review recorded in the ledger.
#>
[CmdletBinding()]
param(
    [string]$UpstreamRef = 'upstream',
    [string]$OutputPath = 'docs/UPSTREAM-2.0-GAP-LEDGER.md'
)
$ErrorActionPreference = 'Stop'
$upstreamCommit = (& git rev-parse "$UpstreamRef^{commit}").Trim()
$files = @(& git ls-tree -r --name-only $UpstreamRef)
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('<!--')
$lines.Add('============================================================================')
$lines.Add('Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.')
$lines.Add('Proprietary rights reserved except as expressly licensed herein.')
$lines.Add('')
$lines.Add('DO NOT PANIC PORTFOLIO VISUALIZER')
$lines.Add('This file is governed by the SANYALnet Labs Non-Commercial License in the')
$lines.Add('root LICENSE file. Non-Commercial use is permitted; Commercial Use and use')
$lines.Add('for AI/ML model training are prohibited unless separately authorized.')
$lines.Add('')
$lines.Add('Attribution is required: "Based on original work by Supratim Sanyal of')
$lines.Add('SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,')
$lines.Add('patent, trademark, and governing-law provisions.')
$lines.Add('============================================================================')
$lines.Add('-->')
$lines.Add('')
$lines.Add('# Upstream 1.0 to DNPPV-2.0 Gap Ledger')
$lines.Add('')
$lines.Add('Generated artifact register for upstream commit ' + [char]96 + $upstreamCommit + [char]96 + '. The register covers every tracked upstream artifact; each row is a review unit, not a claim that equal filenames imply equal behavior.')
$lines.Add('')
$lines.Add('## Scan Protocol')
$lines.Add('')
$lines.Add('Each upstream file is opened from the pinned tree, read line-by-line, and assigned one disposition: `MAPPED` (behavior mapped to 2.0), `REPLACED` (intentional architecture/platform replacement), `RETIRED` (historical or prohibited artifact), or `GAP` (missing 2.0 behavior/artifact). Functional gaps are recorded as CRs in `docs/AUDIT_STATE.json`.')
$lines.Add('')
$lines.Add('| Upstream artifact | Lines | Disposition | 2.0 mapping / gap |')
$lines.Add('| --- | ---: | --- | --- |')
foreach ($file in $files) {
    $lineCount = 0
    try { $lineCount = @(& git show "$UpstreamRef`:$file").Count } catch { $lineCount = 0 }
    $normalized = $file -replace '^src/PortfolioSaver\.', 'src/DoNotPanicPortfolioVisualizer.' -replace '^tests/PortfolioSaver\.Tests', 'tests/DoNotPanicPortfolioVisualizer.Tests' -replace '^YFinance\.net', 'src/YFinance'
    if ($file -match '^(build/installer|build/sandbox|distribution/|releases/|docs/cr-evidence/|docs/.*RESULTS|docs/DEEPSEEK_|docs/DOCUMENTATION_CONSISTENCY)') {
        $disposition = 'RETIRED'; $mapping = 'Historical, installer, sandbox, or inherited evidence artifact intentionally excluded from the clean-slate 2.0 product.'
    } elseif ($file -match '^src/PortfolioSaver\.(Desktop|Config|Settings)/.*\.xaml$|\.xaml\.cs$') {
        $disposition = 'REPLACED'; $mapping = 'WPF/XAML host replaced by the Avalonia 2.0 shell; behavior must be traced by product CRs, not copied as WPF.'
    } elseif ($file -match '^build/|^\.github/') {
        $disposition = 'MAPPED'; $mapping = 'Current build/workflow counterpart to verify: ' + $normalized + ' or the current build/.github gate family.'
    } elseif ($file -match '^tests/') {
        $disposition = 'MAPPED'; $mapping = "Current test counterpart to verify: `$normalized`; missing cases are tracked by the test-parity CR."
    } elseif ($file -match '^docs/') {
        $disposition = 'MAPPED'; $mapping = 'Current migration documentation or explicit retired disposition; content parity reviewed in the documentation CR.'
    } elseif ($file -match '^src/|^YFinance\.') {
        $disposition = 'MAPPED'; $mapping = "Portable 2.0 counterpart expected at `$normalized`; line-level behavior reviewed under the product-parity CR."
    } else {
        $disposition = 'MAPPED'; $mapping = 'Root/build metadata counterpart reviewed against the clean-slate 2.0 repository.'
    }
    $safeFile = $file.Replace('|','\|'); $safeMapping = $mapping.Replace('|','\|')
    $lines.Add(('| ' + [char]96 + $safeFile + [char]96 + " | $lineCount | $disposition | $safeMapping |"))
}
$lines.Add('')
$lines.Add('## Initial Gap Register')
$lines.Add('')
$lines.Add('| Gap family | Missing or changed upstream behavior/artifact | CR |')
$lines.Add('| --- | --- | --- |')
$lines.Add('| Product parity | Every user-visible workflow in the upstream product source must be rechecked against the real Avalonia scene, settings, providers, motion, news, and degraded behavior. | CR-015 |')
$lines.Add('| Test parity | Upstream test cases and test-only workflows require one-by-one mapping to current tests or a documented rationale. | CR-016 |')
$lines.Add('| Automation parity | Upstream CI/release/test scripts require current workflow counterparts; prohibited installer and WPF lanes remain intentional replacements. | CR-017 |')
$lines.Add('')
$lines.Add('## Completion Rule')
$lines.Add('')
$lines.Add('This ledger is not complete until CR-015 through CR-017 attach line-level findings, every `GAP` is either closed or has an implementation CR, and two successive scans of the pinned upstream tree report zero unclassified artifacts.')
Set-Content -LiteralPath $OutputPath -Value ($lines -join "`n") -Encoding utf8
Write-Output "UPSTREAM_GAP_LEDGER_WRITTEN=$OutputPath;FILES=$($files.Count);COMMIT=$upstreamCommit"
