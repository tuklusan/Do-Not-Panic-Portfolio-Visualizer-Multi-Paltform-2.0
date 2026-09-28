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

# CR-030 Configuration Connectivity and Cancellation Inventory

Pinned upstream source: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream behavior | 2.0 implementation and parity result |
| --- | --- | --- |
| CFG-01 | Configuration validation requires usable connectivity and provides a recovery/retry transition when connectivity returns. | `MainViewModel` gates validation on `IsNetworkAvailable`, probes through `IConnectivityService`, and updates the retry command and status on recovery. Equivalent behavior is implemented for Avalonia. |
| CFG-02 | Validation orders structural settings, network/feed or AI checks, and ticker checks; a failed earlier stage prevents later work. | `ValidateAsync` follows the same ordered stages and only invokes feed/AI and ticker validation when no prior errors exist. |
| CFG-03 | Every asynchronous validation operation observes the active cancellation token, including network, feed, AI, and quote work. | The migrated validation services and `ValidateSymbolsAsync` receive and propagate the token; cancellation is checked before publishing results. |
| CFG-04 | A cancellation request is responsive, leaves no validated candidate, and does not save or publish quote seeds. | `CancelValidation` cancels the active source; the cancellation handler clears `_validatedSettings`, quote seeds, and `IsValidated`, while `Save` requires a non-null validated snapshot. |
| CFG-05 | Offline, recovery, successful validation, failed validation, cancellation, and close-during-validation produce distinct user-visible state and bounded trace entries. | The Avalonia view model exposes the corresponding status/summary/log states, uses redacted validation log entries, and supports close-after-cancellation without publishing settings. |

## Failure Matrix

| Case | Required result | Evidence |
| --- | --- | --- |
| Offline before validation | Validation is blocked and settings are not saved. | `MainViewModel` connectivity gate; `ConfigurationWindowContractTests`. |
| Connectivity restored | Retry re-probes and re-enables validation. | `RefreshConnectivityAsync` and `ApplyConnectivityResult`. |
| Feed/AI validation failure | Ticker validation is not started; validated state is cleared. | Ordered `ValidateAsync` branches and service tests. |
| Cancellation during network/feed/AI/ticker work | Operation cancels, pending ticker states are reset, no settings or quote seeds publish. | Cancellation token propagation and cancellation handler. |
| Save after cancellation or edits | Save is unavailable until a fresh successful validation. | `CanSave`, `OnSettingsChanged`, and `Save`. |
| Close during validation | Cancellation is requested; close occurs only after the run has unwound. | `_closeAfterValidationCancellation` path and `Cancel`. |

## Reverse Upstream Gap Scan

Two independent scans of the pinned upstream configuration view-model, validation
services, and corresponding tests found no unmapped behavior for CFG-01 through
CFG-05. No implementation change is required for this CR; the work is a
verification and evidence closure of behavior already ported.
