<!--
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
-->

# Portable Runtime Contract

Based on original work by Supratim Sanyal of SANYALnet Labs.

Current working date: 2026-08-13

## 1. Purpose

This document records the active DNPPV-2.0 runtime contract for product
identity, local data roots, and the locally managed YFinance loopback endpoint.

## 2. Product Identity

- Product display name: `DO NOT PANIC PORTFOLIO VISUALIZER`
- Product lane name: `DO NOT PANIC PORTFOLIO VISUALIZER 2.0`
- Publisher: `SANYALnet Labs`
- Author: `Supratim Sanyal`
- Local data folder name: `DoNotPanicPortfolioVisualizer2`

Legacy folders remain migration input only:

- `DoNotPanicPortfolioVisualizer`
- `PortfolioSaver`

## 3. Override Environment Variables

Active override:

- `DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT`

Deprecated compatibility aliases for explicit developer or automation overrides:

- `DONOTPANICPORTFOLIOVISUALIZER_LOCALDATA_ROOT`
- `PORTFOLIOSAVER_LOCALDATA_ROOT`
- `PORTFOLIOSAVER_APPDATA_ROOT`

Override precedence is:

1. `DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT`
2. `DONOTPANICPORTFOLIOVISUALIZER_LOCALDATA_ROOT`
3. `PORTFOLIOSAVER_LOCALDATA_ROOT`
4. `PORTFOLIOSAVER_APPDATA_ROOT`

## 4. Default Platform Roots

When no override is supplied, DNPPV-2.0 resolves the product root here:

- Windows: `%LOCALAPPDATA%\DoNotPanicPortfolioVisualizer2`
- Linux: `${XDG_DATA_HOME}/DoNotPanicPortfolioVisualizer2`, or
  `~/.local/share/DoNotPanicPortfolioVisualizer2` when `XDG_DATA_HOME` is not
  set
- macOS: `~/Library/Application Support/DoNotPanicPortfolioVisualizer2`

## 5. Subdirectory Contract

Under the resolved product root, DNPPV-2.0 uses:

- `Data`
- `Caches`
- `Caches/History`
- `Logs`
- `Secrets`

The historical cache contract is especially important:

- Windows path target:
  `%LOCALAPPDATA%\DoNotPanicPortfolioVisualizer2\Caches\History`

## 6. YFinance Loopback Contract

DNPPV-2.0 uses a locally managed loopback endpoint for YFinance integration.

- Host: `127.0.0.1`
- Port: `14871`
- Base URI: `http://127.0.0.1:14871/`

This port is the active migration baseline and replaces older local assumptions.

## 7. Migration Guidance

The 2.0 line must use the portable resolver and loopback contract above for
active runtime behavior. Legacy folders may be read for migration input when
that work is intentionally implemented, but they do not define the active 2.0
storage location.
