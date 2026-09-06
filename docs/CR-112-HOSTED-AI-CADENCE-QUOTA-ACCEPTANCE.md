<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
Proprietary rights reserved except as expressly licensed herein.
Based on original work by Supratim Sanyal of SANYALnet Labs.
DO NOT PANIC PORTFOLIO VISUALIZER
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
patent, trademark, and governing-law provisions.
-->

# CR-112: Hosted AI Cadence And Quota-Safe Acceptance

## Status

Open. Discovered while consuming hosted run `34029467480` after the product
cadence was aligned with upstream.

## Objective

Prove that every hosted real-product lane uses the upstream news schedule:
RSS refresh and optional AI replacement occur only during the same refresh
operation, with a minimum 30-minute interval between refresh operations. The
acceptance harness must not create extra AI calls merely to satisfy a short
soak window, and must distinguish provider HTTP 429 quota responses from a
product cadence violation.

The product cadence must remain upstream-compatible. Any burst reduction for
parallel CI lanes must be implemented by the workflow's lane scheduling or
evidence policy, never by shortening or otherwise changing the product's
news/AI cadence.

## Functional Inventory

| ID | Requirement | Proof |
| --- | --- | --- |
| CAD-01 | The product clamps configured news refresh to the upstream 30-minute minimum and performs RSS and optional AI work in one refresh operation. | Source scan and focused service tests. |
| CAD-02 | A refresh failure or HTTP 429 preserves usable RSS and does not immediately start an additional refresh. | Trace assertions and failure-matrix tests. |
| CAD-03 | A hosted lane records RSS, AI request, AI result, status, and cadence evidence without secrets. | Lane manifest, circular traces, and receipt validator. |
| CAD-04 | A concurrent matrix does not falsely classify provider quota exhaustion as a product cadence defect, while still retaining the failed AI evidence for follow-up. | Matrix acceptance and aggregate review. |
| CAD-05 | The 21-lane serialized matrix waits for queued work and consumes every lane's screenshots, two circular traces, RSS/AI evidence, and review receipt. | Workflow gate and authoritative run. |
| CAD-06 | Upstream forward and reverse scans identify zero unmapped cadence behavior in two successive passes. | Source-cited inventory and repeated migration gate. |

## Source And Evidence Map

The upstream reference is the pinned commit
`65a53bbbf0cf9af1058363f8939d464ca03858f8` in the upstream 1.0 repository.
The migration contract is `docs/UPSTREAM_ACCEPTANCE_BASELINE.md`, especially
its 30-minute summarized-news minimum, RSS-first fallback, and retry/cache
rules. The relevant upstream scheduler and AI path are
`src/PortfolioSaver.Presentation/ViewModels/VisualizerSceneViewModel.cs`
(`RunNewsRefreshLoopAsync`/`RefreshNewsAsync`) and
`src/PortfolioSaver.Presentation/Services/FinanceNewsService.cs`
(`GetPlaybackSnapshotCoreAsync`/`GetAiSummaryAsync`).

The v2 counterparts are
`src/DoNotPanicPortfolioVisualizer.Presentation/ViewModels/ProductSceneViewModel.cs`
(`RunNewsRefreshLoopAsync`/`RefreshNewsAsync`) and
`src/DoNotPanicPortfolioVisualizer.Presentation/Services/FinanceNewsService.cs`
(`GetPlaybackSnapshotCoreAsync`/`GetAiSummaryAsync`). The current v2 loop clamps
`NewsRefreshMinutes` to the existing validated
`Defaults.MinNewsRefreshMinutes`/`Defaults.MaxNewsRefreshMinutes` range of 30
through 240 minutes in `AppSettingsNormalizer` and `SettingsValidator`,
completes one RSS-plus-optional-AI refresh operation, then waits the clamped
interval before the next operation. The 240-minute upper bound is the existing
settings safety bound, not an additional network call or a shortened cadence;
CR-112 must retain it unless a source comparison proves the upstream settings
contract differs.
The two 750 ms/1.5 s AI attempts are internal attempts in that one operation;
they are not additional news refreshes.

Focused tests are in
`tests/DoNotPanicPortfolioVisualizer.Tests/AmbientSceneServicesTests.cs`:
`FinanceNewsService_RssFirstPathPublishesWithoutWaitingForAi`,
`FinanceNewsService_FallsBackToRssForAiHttpAndMalformedResponses`,
`FinanceNewsService_FallsBackToRssWhenAiTimesOut`,
`FinanceNewsService_PropagatesExplicitAiCancellation`, and the structured
response-shape tests. The hosted evidence implementation is
`.github/workflows/publish-six-rids.yml` and
`build/Test-HostedSoakClosure.ps1`; its v1 news evidence fields are
`rssUsable`, `rssPublished`, `aiRequired`, `aiRequestObserved`,
`aiSuccessObserved`, `failure`, and the two circular trace paths.

## Decision Rules

- A cadence violation is a second `NEWS_SOURCE` trace event, emitted by
  `ProductSceneViewModel.RefreshNewsAsync` after a completed refresh operation,
  less than 30 minutes after the preceding refresh operation's start. Internal AI retries within
  the same operation are not cadence violations; the documented bounded retry
  budget is two attempts with 0.75 s then 1.5 s delays.
- A provider-quota result is an AI response with HTTP 429 (or an equivalent
  provider rate-limit result) recorded inside a correctly timed refresh. It is
  retained as failed AI evidence and must not be rewritten as an AI success.
- An AI response, timeout, malformed payload, or credential failure never
  removes already-published usable RSS. The next refresh remains governed by
  the same 30-minute minimum.
- A lane is complete only when its manifest, screenshot where supported, both
  circular traces, news evidence, receipt, and reviewer output are retained and
  secret-scanned. The receipt validator rejects missing or empty review output;
  it does not erase quota-failure evidence.

The 21-lane count is not arbitrary: it is the current matrix emitted by
`.github/workflows/publish-six-rids.yml` and asserted by
`build/Test-WorkflowGateConfiguration.ps1` (`EXPECTED_LANE_COUNT=21`). The
workflow's root concurrency group serializes matrix runs; queued work must
finish before another matrix is launched. The authoritative current-run
artifacts are retained by GitHub at
`https://github.com/tuklusan/Do-Not-Panic-Portfolio-Visualizer-Multi-Paltform-2.0/actions/runs/34029467480`.

The upstream forward gate is executed as
`build/Test-MigrationBehaviorGate.ps1 -CrId CR-112 -Stage PreDevelopment` and
again with `-Stage Closure`. Its reverse scan reads the listed upstream and v2
files from disk and requires two successive zero-gap passes. The license,
PowerShell syntax, workflow, build, test, NVIDIA source/evidence review, and
cleanup gates are the repository pre-push gates defined in `AGENTS.md` and
`docs/FRESH-PROJECT-NVIDIA-REVIEW-GATE.md`.

## Current Evidence

Run `34029467480` showed `aiRequestObserved=true` on retained lanes and
`aiSuccessObserved=false` on several lanes after two HTTP 429 responses; its
lane review receipts also retain the exact provider findings. The same run
showed successful AI evidence on other lanes. This is evidence of a
parallel-provider quota condition, not evidence that the product scheduled a
second refresh faster than 30 minutes. The run also retained one screenshot
and two circular traces for each of 20 lanes; `macos-14` was cancelled before
evidence generation and remains an explicit acceptance gap.

Run `34051000159` provides the next retained evidence set. Its lane traces
show AI HTTP 429 outcomes on affected lanes, including lanes with no eventual
AI success, while the RSS/news evidence remains present. The aggregate failure
therefore routes to CR-112 as quota/evidence work; it does not establish a
cadence violation. The run remains non-closure evidence because the aggregate
validator did not pass for all 21 lanes.

Run `34063195136` again observed RSS publication and an AI request on all 21
lanes. Eight lanes had `aiSuccessObserved=false`; their traces show empty
successful responses or HTTP 429 outcomes without a later successful summary.
The product soaks all passed, so this remains an AI evidence/quota finding,
not evidence of a cadence violation. The aggregate was not all-green and CR
112 remains open.

Run `34058732179` adds a complete 21-lane inspection. RSS was published and an
AI request was observed on every lane; seven lanes recorded no successful AI
response after provider quota/availability responses, while the remaining lanes
retained successful AI evidence. All product soaks passed and both circular
traces were retained per lane. This confirms quota/evidence pressure, not a
cadence violation, but does not close the CR because the aggregate proof was
not all-green.

## Closure Gates

Run the focused tests named above and the full Release suite. Run
`Test-MigrationBehaviorGate.ps1` in both stages, `Test-LicenseHeaders.ps1`,
`Test-PowerShellSyntax.ps1`, `Test-WorkflowGateConfiguration.ps1`, the
mandatory NVIDIA source/evidence review, and cleanup. Then run one fresh
serialized 21-lane acceptance. Inspect every lane's screenshot where
supported, both circular traces, news evidence, review receipt, and closure
record; the lane manifest must show the fields above and no secret markers.
Close only when the cadence is proven and quota-limited lanes retain evidence
without being mistaken for a cadence defect.
