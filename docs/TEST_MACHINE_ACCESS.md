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

The three physical endpoints were refreshed and SSH-verified on 2026-08-24.
Update only the ignored local endpoint inventory when DHCP or network topology
changes; do not commit live addresses or credentials.

## Physical Machines

### `linux-x64-lxqt`

- Access: exact current SSH endpoint is kept only in the local ignored endpoint
  inventory
- Sudo: available; uses the same password on first prompt
- OS: Lubuntu Linux 26.04 with LXQt desktop
- Notes: DHCP details may change

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

## GitHub-Hosted Build/Test Lanes

- `github-macos-x64`: runner `macos-15-intel`, RID `osx-x64`
- `github-macos-arm64`: runner `macos-15`, RID `osx-arm64`
- `github-linux-arm64`: runner `ubuntu-24.04-arm`, RID `linux-arm64`
- `github-windows-arm64`: runner `windows-11-arm`, RID `win-arm64`
- `github-linux-x64`: runner `ubuntu-24.04`, RID `linux-x64`

## Baseline Note

These are the retained environment details for the fresh DNPPV-2.0 migration
repository.

## Product-Scene Acceptance

Use `build/vm/Invoke-ProductSceneValidation.ps1` for physical cinematic-product
acceptance. It launches the normal product shell, captures its ordinary
background/ticker/card behavior and a cinematic playback trace, and guarantees
application cleanup. It deliberately does not enable the graph-impulse fixture:
fixtures are focused diagnostic inputs, never the product demonstration used
for acceptance.
