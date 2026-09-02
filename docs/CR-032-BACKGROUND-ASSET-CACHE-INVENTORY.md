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

# CR-032 Background Asset Cache Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| AST-01 | Bundled starter backgrounds are immediately available and selected safely when a managed cache is empty or unavailable. | `BackgroundImageService` and `BackgroundFrameLoader` resolve bundled Avalonia assets and validate file access before rendering. |
| AST-02 | Managed background downloads are staged, validated, and atomically promoted; partial files are never selected. | `BackgroundImageService` owns supported-file selection, while the migrated managed cache path stages `.TMP` files, verifies JPEG signatures, promotes only complete files, and removes stale partials. |
| AST-03 | Concurrent warmup is serialized and does not block the UI thread. | `BackgroundFrameLoader` loads and decodes on a worker task; managed cache warmup uses `_downloadGate` and `_cacheGate` with asynchronous waits. |
| AST-04 | Cancellation stops warmup and removes the current partial download without promoting it. | Download and decode paths propagate cancellation, delete the temporary download on cancellation, and leave the existing selected background intact. |
| AST-05 | Catalog rotation adds missing images, preserves existing files, and notifies the presentation layer when new assets are available. | The migrated catalog warmup skips existing targets, writes attribution metadata, and raises `BackgroundCacheWarmupCompleted` only when the catalog changes. |
| AST-06 | Prepared images are decoded once, retain stable lifetime, and release resources on disposal. | `BackgroundFrameLoader` caches decoded `Bitmap` instances by source, handles races without leaks, and disposes every cached bitmap deterministically. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Empty/unavailable cache | Use bundled starters without failing startup. | Background cache fallback path and media tests. |
| Invalid or unsupported file | Exclude it from selectable backgrounds. | Extension filtering and image selection tests. |
| Partial download | Keep `.TMP` out of selections and clean stale remnants. | Managed-cache tests. |
| Invalid downloaded content | Delete the staged file and keep the previous cache. | JPEG signature validation path. |
| Concurrent warmups | One serialized warmup runs; callers do not corrupt cache state or block the UI. | Gate and async warmup paths. |
| Cancellation during download/decode | Cancel promptly, remove the partial, and preserve the prior usable frame. | Cancellation-aware cache and loader paths. |
| Repeated disposal | Dispose cached bitmaps once and tolerate repeated owner cleanup. | Loader disposal path. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream media/cache implementation and
tests, followed by scans of the migrated media, presentation, app, and test
paths, found no unmapped behavior for AST-01 through AST-06. The migrated
implementation preserves the upstream behavior and adds the portable staged
cache and Avalonia decoding required by the target architecture.
