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

# Test Machine Access Details

Current working record: 2026-08-24

This repository is in migration-baseline mode.

Exact live credentials are intentionally not committed. Operators may keep a
local ignored endpoint inventory such as `build/vm/remote-test-machines.local.txt`,
and passwords remain in the operator password manager.

The four physical endpoints were refreshed and SSH-verified on 2026-08-24.
Update only the ignored local endpoint inventory when DHCP or network topology
changes; do not commit live addresses or credentials.

## Physical Machines

### `linux-x64-lxqt`

- Access: exact current SSH endpoint is kept only in the local ignored endpoint
  inventory
- Sudo: available; uses the same password on first prompt
- OS: Lubuntu Linux 26.04 with LXQt desktop
- Notes: DHCP details may change
- Desktop capture: `scrot` and ImageMagick `import` are available;
  `gnome-screenshot` was installed on 2026-08-30 as an additional X11 capture
  fallback. The physical product harness verifies non-empty PNG artifacts and
  uses bounded fallbacks in that order.
- Fullscreen acceptance: the harness sends the real product `F11` command,
  records the post-transition X11 geometry, and captures settled fullscreen
  and motion frames. LXQt may emit harmless X11 property warnings during that
  transition; geometry plus non-empty captures are the authoritative evidence.

### `windows-10-reference`

- Access: exact current SSH endpoint is kept only in the local ignored endpoint
  inventory
- OS: Windows 10
- Required project root: `D:\SW_DEV\DO-NOT-PANIC-2.0`
- Required temp root: `D:\TEMP`
- Required environment rule: machine `TEMP` and `TMP` must resolve to
  `D:\TEMP`
- Notes: treat missing or inaccessible `D:\SW_DEV\DO-NOT-PANIC-2.0` or
  `D:\TEMP` as a hard stop requiring human intervention
- Harness gate: when the remote publish path is below this root,
  `Invoke-ConfigWindowValidation.ps1` verifies both roots are writable, checks
  machine-level `TEMP` and `TMP`, and rechecks the contract while the product
  is running. A failed check is fatal; the harness does not continue or clean
  around the failure.

### `windows-11-laptop`

- Access: exact current SSH endpoint is kept only in the local ignored endpoint
  inventory
- OS: Windows 11
- Notes: no `D:\SW_DEV\DO-NOT-PANIC-2.0` requirement on this machine
- SSH: interactive login can take noticeably longer than the other two physical
  machines. Treat the endpoint as available when the SSH session completes;
  validation drivers must allow an extended connection budget before declaring
  a transport failure.
- SmartScreen: the test-machine owner disabled the SmartScreen filter on
  2026-08-25 so unsigned DNPPV-2.0 development bundles can be physically
  exercised here. This is a temporary, isolated local test-environment
  exception only, not a signed-release acceptance result or a practice for
  any other machine. Re-enable the filter after this testing session; restore
  a protected release posture and use a trusted code-signing certificate before
  any public release acceptance.

### `macos-x64-intel-big-sur`

- Access: exact current SSH endpoint is kept only in the local ignored endpoint
  inventory; current lab address is recorded there as `10.0.0.114`
- OS: macOS Big Sur on Intel x64 hardware
- Required project root: `~/SOFTWARE_DEV/DNPPV_20/`
- Confinement rule: every uploaded source, build, test, temporary, log, and
  acceptance artifact must remain below the required project root; missing or
  inaccessible root is a hard stop requiring human intervention
- Resource rule: total project-related disk usage must remain at or below
  `1 GB` at all times; validation must measure before and during each run and
  hard-stop before exceeding the ceiling
- SSH: user is maintained in the ignored endpoint inventory; credentials are
  never committed
- Harness gate: `build/vm/Test-MacStorageContract.sh` is the mandatory probe for
  the Mac lane. It hard-stops when the required root is missing, outside the
  user's home directory, inaccessible, or above `1 GiB` (1,048,576 KiB). The
  Mac runner must invoke it before deployment and between run steps; absence of
  a Mac driver that invokes it is a test-environment gap, not a waiver.

## GitHub-Hosted Build/Test Lanes

The publish workflow exercises every currently documented standard/public label
for the supported architectures. The `-latest` aliases are retained as
separate lanes so image migration is visible rather than silently replacing a
fixed-version result:

- Windows x64: `windows-latest`, `windows-2025`, `windows-2025-vs2026`, `windows-2022`; RID `win-x64`
- Windows ARM64: `windows-11-arm`, `windows-11-vs2026-arm`; RID `win-arm64`
- Linux x64: `ubuntu-latest`, `ubuntu-24.04`, `ubuntu-22.04`, `ubuntu-26.04`; RID `linux-x64`
- Linux ARM64: `ubuntu-24.04-arm`, `ubuntu-22.04-arm`, `ubuntu-26.04-arm`; RID `linux-arm64`
- macOS Intel x64: `macos-15-intel`, `macos-26-intel`; RID `osx-x64`
- macOS ARM64: `macos-15`, `macos-14`, `macos-26`; RID `osx-arm64`

Preview labels are intentionally included in the matrix and may fail when
GitHub availability changes; `fail-fast: false` preserves results from every
other lane. The local Windows 10, Windows 11, Lubuntu, and Intel Big Sur
machines remain physical acceptance lanes, not GitHub-hosted runners.

## Baseline Note

These are the retained environment details for the fresh DNPPV-2.0 migration
repository.

## Product-Scene Acceptance

### Diagnostic artifact contract

The product and its YFinance sidecar write diagnostics only to their
size-bounded circular traces under the resolved local-data root:

- `Trace/trace.circular.log` and `Trace/trace.circular.idx` for the product
- `Trace/yfinance.circular.log` and `Trace/yfinance.circular.idx` for YFinance

Physical harnesses copy the product trace pair into an artifact `trace/`
directory for review. `step.log` and `done.txt` are harness control evidence,
not product logs. Harnesses must discard redirected process output and must
not create `run.log`, `capture-errors.log`, `cinematic-playback.log`, or
`graph-impulse.log`.

Use `build/vm/Invoke-ProductSceneValidation.ps1` for physical cinematic-product
acceptance. It launches the normal product shell, captures its ordinary
background/ticker/card behavior and a cinematic playback trace, and guarantees
application cleanup. On Windows it captures an explicit `1024x768` viewport,
then a wider maximized viewport, followed by fullscreen motion. It deliberately
does not enable the graph-impulse fixture: fixtures are focused diagnostic
inputs, never the product demonstration used for acceptance.

Product-scene capture enforces at least a 30-second warmup so the live scene,
including all four ticker lanes, settles before any acceptance screenshot.

Windows product-scene capture also finds the ordinary File menu through Windows
UI Automation, clicks the center of its live accessibility rectangle, asserts
that its Exit child is visible, records `menu-open.png`, then dismisses it
before the wide/fullscreen checks. This is acceptance evidence for the actual
shell menu and submenu palette, not a visual fixture.

For the initial viewport the Windows driver launches the ordinary product with
`--windowed=1024x768`, a bounded startup option that places the Avalonia window
in its normal state before the physical geometry assertion. The default product
startup remains maximized; this option exists to make the required small-screen
acceptance state reproducible rather than to alter the cinematic default.

Windows native rectangles can be DPI virtualized. The driver records those
pixels diagnostically and instead polls for the product's fail-closed logical
viewport trace: `WINDOWED_STARTUP_APPLIED;STATE=Normal;WIDTH=1024;HEIGHT=768`.
