<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.

DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.

Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
-->

# CR-037 Symbol and Market Classification Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| MKT-01 | User-entered symbols are trimmed and canonicalized consistently without damaging meaningful punctuation. | `YFinanceSymbolMapper` and `SymbolNormalizer` normalize case/whitespace and preserve supported dot, slash, caret, dash, and equals forms. |
| MKT-02 | Provider request aliases map display/request symbols to the provider’s canonical symbols and map results back to the requested identity. | `YFinanceSymbolMapper` applies request aliases and `YahooSymbolValidationService` matches exact normalized response symbols before safe fallbacks. |
| MKT-03 | Asset class is inferred from provider instrument type or supported symbol shape, including equity, ETF, fund, index, future, forex, crypto, and money-market cases. | `SymbolProfileHeuristics` and provider mapping implement instrument-type precedence plus deterministic shape inference. |
| MKT-04 | Invalid symbols are identified individually, disabled in configuration editing, and excluded from validated quote seeds. | `YahooSymbolValidationService` records invalid entries; the configuration view model disables invalid ticker editors and clears invalid validation state. |
| MKT-05 | Provider market-state strings map to stable pre-market, regular, after-hours, closed, and unknown session values. | `YFinanceSymbolMapper.MapMarketSession` and quote mapping preserve the normalized `MarketSession` contract. |
| MKT-06 | Numeric values requiring instrument-specific scaling are normalized before display or graph use. | `NormalizeNumericValue` applies treasury-yield scaling and leaves ordinary quote values unchanged. |
| MKT-07 | Exchange timezone identifiers are portable across Windows and IANA forms and session boundaries remain deterministic. | `ExchangeTimeZoneResolver` maps supported identifiers for all target platforms; market-session tests cover pre-market, regular, after-hours, and closed boundaries. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Blank or whitespace symbol | Reject or omit it without provider work. | Symbol normalization and validation tests. |
| Dot/dash alias symbol | Submit provider alias and restore requested display identity. | Provider pipeline tests. |
| Unknown symbol | Mark only that editor invalid and do not publish a quote seed. | Validation result and configuration paths. |
| Provider instrument type present | Prefer provider classification over heuristic shape. | Asset-class tests. |
| Unknown market-state text | Return `Unknown`, not a guessed session. | Mapper tests. |
| Treasury yield quote | Scale yield values correctly for display. | Numeric normalization tests. |
| Windows/IANA timezone ID | Resolve equivalently on supported platforms. | Timezone resolver tests. |
| Session boundary timestamp | Return the expected market session deterministically. | Market-session tests. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream symbol, quote-provider,
market-session, timezone, and validation implementations and tests, followed by
scans of the migrated provider, core rules, presentation, and test paths, found
no unmapped behavior for MKT-01 through MKT-07.
