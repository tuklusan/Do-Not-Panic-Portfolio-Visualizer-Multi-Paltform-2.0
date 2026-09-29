<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# Physical acceptance receipt: `1b792187`

Candidate commit: `1b792187b50bed0f2717bf08e2c20a458d81828f`

The exact-commit self-contained bundles were exercised on all four required
machines. The product was never launched on the developer workstation.

| Machine | RID | Cycle result | Evidence |
| --- | --- | --- | --- |
| Lubuntu LXQt | `linux-x64` | Passed, 10-minute soak | `D:\TEMP\dnppv2-local-cycle-1b792187-linux` |
| Windows 10 reference | `win-x64` | Passed, 10-minute soak | `D:\TEMP\dnppv2-local-cycle-1b792187-win10` |
| Windows 11 laptop | `win-x64` | Passed, 10-minute soak | `D:\TEMP\dnppv2-local-cycle-1b792187-win11` |
| Intel Mac Big Sur | `osx-x64` | Passed, 10-minute native-PTY soak | `D:\TEMP\dnppv2-local-cycle-1b792187-mac` |

All four lanes produced settled scene evidence, circular product/YFinance
traces, RSS evidence, and cleanup. Linux additionally produced fullscreen
motion captures after the X11 screenshot fallback correction. Windows 10 and
Windows 11 produced small, wide, fullscreen, motion, and menu captures. The
Mac lane used native interactive SSH/PTY, CoreGraphics capture, and removed
its exact temporary remote cycle root after artifact retrieval.

The local provider receipts recorded `RssPlaybackReady` and
`AiSummaryRequestStarted` where the provider path was exercised. No local
receipt is claimed as `AiSummarySucceeded`: the configured external AI
provider was unavailable or credential-limited during this cycle, so AI
degraded behavior remains an open release gate rather than being falsely
marked complete.

Representative retained artifact hashes (SHA-256):

- Linux result: `31d007065a6301af40d80b5a56dcb94ba821681bd704afac64bed239d707e1f`
- Windows 10 result: `3ba8ff77b1e0be96780432dabddf2f4d345cf8166e34e402645df74c3005580`
- Windows 11 result: `c5247a31c948a50851acdf578d6f8ab00c154cafce5ce2b271a86b97a8cb4741`
- Mac soak receipt: `81c5f021d7ae539fa4f18941b377faff57d93a9635474a0af4070a63096dca4b`
- Mac product screenshot: `5e3b260e198ae0efd1eff15626fcf9498919c2404ac98b3ba89645256323ffbf`

This receipt closes the fresh four-machine launch/scene/cleanup evidence gap
for this candidate, but does not close AI-provider acceptance, complete
upstream cinematic-contract parity, trusted signing, or public release.
