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

# CR-018 AI Access And Prompt Safety Inventory

Upstream pin: `2e2fab0f013ff3def5e4ddbac13bf17dd14e71b5`.

## Functional Inventory

| ID | Upstream source | Behavior to preserve in 2.0 |
| --- | --- | --- |
| AI-01 | `src/PortfolioSaver.Settings/Services/AiNewsAccessValidationService.cs`; `src/PortfolioSaver.Settings/ViewModels/MainWindowViewModel.cs` | Validate endpoint, model, key, bounded request, success response, and actionable failure feedback before saving AI settings. |
| AI-02 | `src/PortfolioSaver.Core/Services/OpenRouterModelResolver.cs`; `src/PortfolioSaver.Core/Validation/SettingsValidator.cs` | Normalize endpoint/model rules, reject unusable settings, and retain safe defaults. |
| AI-03 | `src/PortfolioSaver.Presentation/Services/FinanceNewsService.cs` | Apply timeout, cancellation, 401/429/5xx, malformed/empty response, and RSS fallback behavior without collapsing the scene. |
| AI-04 | `src/PortfolioSaver.Render/ViewModels/NewsFlasherViewModel.cs`; `NewsHeadlineViewModel.cs` | Present safe generated or RSS headlines with bounded content and source attribution. |
| AI-05 | `src/PortfolioSaver.Settings/Windows/MainWindow.xaml`; `MainWindow.xaml.cs` | Keep API fields, validation state, and save/apply interaction visible and correctly gated in the Avalonia configuration surface. |
| AI-06 | Upstream news prompt construction and response parsing | Treat headlines as untrusted input; generated output must not execute, alter settings, bypass fallback, or suppress provider errors. |
| AI-07 | `src/PortfolioSaver.Desktop/App.xaml.cs` startup access probe | After the desktop is visible, perform a bounded summarized-news access check, log a failed probe without preventing normal refresh retry, and preserve cancellation/cleanup behavior. |
| AI-08 | Upstream settings/configuration workflow and physical validation path | Permit a physical test run to configure summarized-news mode with a non-secret test endpoint, model, and key, then prove a summary was requested and displayed with explicit trace evidence. |

## Required Failure Matrix

The deterministic test matrix must cover missing key, invalid endpoint/model,
401, 429, 5xx, timeout, cancellation, malformed JSON, empty content, prompt
injection-like headline text, valid structured output, and RSS fallback for
every non-cancellation failure.

## Audit Exit

Two successive fresh line-by-line scans of all cited upstream AI sources must
report zero unmapped behaviors. Any genuine implementation or test gap becomes
an explicit follow-up CR before this CR closes.
