<!--
Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
This file is governed by the SANYALnet Labs Non-Commercial License in the
root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
for AI/ML model training are prohibited unless separately authorized.
Attribution is required: "Based on original work by Supratim Sanyal of
SANYALnet Labs." See LICENSE for full terms.
-->

# CR-123: Cinematic ticker coverage and compositor timing drift

Status: Open. This is a release-blocking visual-parity defect.

## Observed divergence

1. On wide or maximized displays, ticker lanes can terminate before the
   available scene width. A populated lane must repeat its measured sequence
   enough to cover the entire visible viewport, with the duplicated cycle
   joining without a visible gap.
2. The cinematic scene can freeze, jitter, and later catch up during initial
   hydration and ordinary playback. The motion scheduler must not queue
   competing UI updates behind refresh, hydration, network, or clock work.
3. The upstream Global Markets strip includes compact per-market trend graphs
   inside the moving cards. A text-only card is incomplete even when its
   clock, session, quote, and weather fields are present.

## Mitigation in this candidate

- Sparse lanes now expand their clipped content viewport to the available
  scene width and calculate repeated copies from that viewport.
- Ticker motion is advanced by the single ambient frame scheduler instead of a
  competing UI-dispatch loop.
- NTP refresh is detached from the frame critical path.
- Bounded `FRAME` timing records now correlate monotonic scheduler elapsed time
  with UI-dispatch duration and overdue frames.
- Global-market cards now retain quote samples and render a compact trend path;
  news playback is stepped by the same coalesced ambient frame as the other
  cinematic motion, eliminating a competing 40-ms UI-dispatch loop.

## Closure evidence required

- deterministic tests proving full-width coverage and cycle continuity at
  restored, maximized, and wide-screen dimensions;
- two successive real-product runs on Linux, Windows 10, Windows 11, and Intel
  macOS with screenshots and circular traces reviewed against the upstream
  cinematic contract;
- no unexplained `FRAME;...OVERDUE=true` bursts during hydration or settled
  playback; and
- NVIDIA review and protected release-gate evidence for the implementation and
  the retained runtime artifacts.
