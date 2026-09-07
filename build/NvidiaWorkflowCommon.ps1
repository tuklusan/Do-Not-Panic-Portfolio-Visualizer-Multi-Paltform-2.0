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
Set-StrictMode -Version Latest

function Get-NvidiaSecretPatterns {
    return @(
        '(?im)(api[_-]?key|secret|token|password)\s*[:=]\s*[''"](?!(test|example|placeholder|dummy|sample|REPLACE_WITH_))([A-Za-z0-9_\-+/=]{16,})[''"]',
        '(?im)(?:export\s+|set\s+)?(api[_-]?key|secret|token|password)\s*[:=]\s*(sk-[A-Za-z0-9_-]{20,}|[A-Za-z0-9_\-+/=]{32,})',
        '(?im)(?:nvidia[_-]?api[_-]?key|api[_-]?key)\s*[:=]\s*[''\"]?(nvapi-[A-Za-z0-9_-]{20,})[''\"]?',
        '(?m)nvapi-[A-Za-z0-9_-]{20,}',
        '(?im)Authorization\s*[:=]\s*[''"]Bearer\s+(sk-[A-Za-z0-9_-]{20,}|[A-Za-z0-9_\-+/=]{32,})[''"]',
        '(?im)sk-(?!test|example|placeholder|dummy|sample)[A-Za-z0-9_-]{20,}',
        '(?m)AKIA[0-9A-Z]{16}',
        '(?m)ASIA[0-9A-Z]{16}',
        '(?m)AIza[0-9A-Za-z\-_]{35}',
        '(?m)ghp_[A-Za-z0-9_]{30,}',
        '(?m)eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}',
        '(?im)[a-z][a-z0-9+.-]{2,}://[^/\s:@]{2,}:[^/\s:@]{8,}@',
        '(?im)(connectionstrings?|machinekey|clientsecret|tenantsecret)\s*[:=]\s*[''"](?!(test|example|placeholder|dummy|sample))[^''"]*(password|pwd|secret|accesskey|api[_-]?key|validationkey|decryptionkey)[^''"]{8,}[''"]',
        '(?im)(?<![.\w])(password|pwd|user id|uid)\s*=\s*[^;\r\n]{8,};',
        '(?s)-----BEGIN [A-Z ]*PRIVATE KEY-----.*?-----END [A-Z ]*PRIVATE KEY-----'
    )
}

function Redact-LikelySecretsInText {
    param([Parameter(Mandatory = $true)][string]$Text)

    $sanitized = $Text
    $replacements = @(
        @{ Pattern = '(?i)(Authorization\s*[:=]\s*Bearer\s+)[A-Za-z0-9_\-+/=]+'; Replacement = '${1}[redacted]' },
        @{ Pattern = '(?i)(Bearer\s+)[A-Za-z0-9_\-+/=]+'; Replacement = '${1}[redacted]' },
        @{ Pattern = '(?i)(\b(api[_-]?key|token|secret|password)\b\s*[:=]\s*["'']?)[^"''\s;&]+'; Replacement = '${1}[redacted]' },
        @{ Pattern = '(?i)([?&](api[_-]?key|token|secret|password)=)[^&\s]+'; Replacement = '${1}[redacted]' },
        @{ Pattern = '(?i)sk-[A-Za-z0-9_-]{20,}'; Replacement = 'sk-[redacted]' },
        @{ Pattern = '(?i)nvapi-[A-Za-z0-9_-]{20,}'; Replacement = 'nvapi-[redacted]' },
        @{ Pattern = '(?m)(AKIA|ASIA)[0-9A-Z]{16}'; Replacement = '$1[redacted]' },
        @{ Pattern = '(?m)AIza[0-9A-Za-z\-_]{35}'; Replacement = 'AIza[redacted]' },
        @{ Pattern = '(?m)ghp_[A-Za-z0-9_]{30,}'; Replacement = 'ghp_[redacted]' },
        @{ Pattern = '(?m)eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}'; Replacement = '[redacted-jwt]' },
        @{ Pattern = '(?s)-----BEGIN [A-Z ]*PRIVATE KEY-----.*?-----END [A-Z ]*PRIVATE KEY-----'; Replacement = '[redacted-private-key]' }
    )

    foreach ($replacement in $replacements) {
        $sanitized = [regex]::Replace($sanitized, $replacement.Pattern, $replacement.Replacement)
    }

    return $sanitized
}

function Get-ValidatedNvidiaEndpoint {
    param([Parameter(Mandatory = $true)][string]$Endpoint)

    if ([string]::IsNullOrWhiteSpace($Endpoint)) {
        throw 'Nvidia review endpoint must not be empty.'
    }

    $trimmed = $Endpoint.Trim()
    $uri = $null
    if (-not [Uri]::TryCreate($trimmed, [UriKind]::Absolute, [ref]$uri)) {
        throw 'Nvidia review endpoint must be an absolute HTTPS URI.'
    }

    if (-not [string]::Equals($uri.Scheme, 'https', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Nvidia review endpoint must use HTTPS.'
    }

    if (-not [string]::IsNullOrWhiteSpace($uri.UserInfo)) {
        throw 'Nvidia review endpoint must not include embedded credentials.'
    }

    $builder = [System.UriBuilder]::new($uri)
    $builder.UserName = [string]::Empty
    $builder.Password = [string]::Empty
    $builder.Query = [string]::Empty
    $builder.Fragment = [string]::Empty
    return $builder.Uri.AbsoluteUri.TrimEnd('/')
}

function Get-SafeNvidiaEndpointForLog {
    param([Parameter(Mandatory = $true)][string]$Endpoint)

    try {
        return Get-ValidatedNvidiaEndpoint -Endpoint $Endpoint
    }
    catch {
        return '[invalid-endpoint-redacted]'
    }
}

function Get-RepoRoot {
    $root = & git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
        throw 'git repository root could not be resolved.'
    }

    return $root.Trim()
}

function Get-NvidiaApiKey {
    param([Parameter(Mandatory = $true)][string]$RepositoryRoot)

    $key = [Environment]::GetEnvironmentVariable('NVIDIA_API_KEY_CODING')
    if (-not [string]::IsNullOrWhiteSpace($key)) { return $key }

    # Local-only ignored test secret overlay. This file must never be committed.
    $secretsPath = Join-Path $RepositoryRoot 'build\vm\test-secrets.json'
    if (Test-Path -LiteralPath $secretsPath) {
        try {
            $secrets = Get-Content -Raw -LiteralPath $secretsPath | ConvertFrom-Json
            if ($secrets.PSObject.Properties.Name -contains 'NvidiaApiKeyCoding' -and
                -not [string]::IsNullOrWhiteSpace([string]$secrets.NvidiaApiKeyCoding)) {
                return [string]$secrets.NvidiaApiKeyCoding
            }
        }
        catch {
            Write-Warning 'Invalid JSON in the local ignored Nvidia secrets file; fix or delete the file if local key resolution needs it.'
        }
    }

    throw "Nvidia API access is mandatory for this project's workflow, but no working Nvidia key was found in the configured local key sources. Hard stop: do not commit, push, or run local/VM validation until Nvidia access is available."
}

function Assert-NoLikelySecrets {
    param([Parameter(Mandatory = $true)][string]$Text)

    foreach ($pattern in Get-NvidiaSecretPatterns) {
        if ($Text -match $pattern) { throw 'Potential secret material detected in the review packet. Inspect the pending changes and remove secrets before sending to Nvidia.' }
    }
}

