# Test Machine Access Details

Current working record: 2026-08-13

This repository is in clean-slate mode.

Exact live credentials are intentionally not committed. Operators may keep a
local ignored endpoint inventory such as `build/vm/remote-test-machines.local.txt`,
and passwords remain in the operator password manager.

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

## GitHub-Hosted Build/Test Lanes

- `github-macos-x64`: runner `macos-15-intel`, RID `osx-x64`
- `github-macos-arm64`: runner `macos-15`, RID `osx-arm64`
- `github-linux-arm64`: runner `ubuntu-24.04-arm`, RID `linux-arm64`
- `github-windows-arm64`: runner `windows-11-arm`, RID `win-arm64`
- `github-linux-x64`: runner `ubuntu-24.04`, RID `linux-x64`

## Reset Note

These are the only retained environment details from the prior project state.
All former workflow, validation, application, migration, and evidence artifacts
were intentionally removed during the clean-slate reset.
