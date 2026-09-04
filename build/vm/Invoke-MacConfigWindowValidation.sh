# ============================================================================
# Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
# Proprietary rights reserved except as expressly licensed herein.
#
# DO NOT PANIC PORTFOLIO VISUALIZER
# This file is governed by the SANYALnet Labs Non-Commercial License in the
# root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
# for AI/ML model training are prohibited unless separately authorized.
#
# Attribution is required: "Based on original work by Supratim Sanyal of
# SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
# patent, trademark, and governing-law provisions.
# ============================================================================
#!/usr/bin/env bash
set -euo pipefail

root="${1:?required project root}"
publish="$root/publish-cr019"
artifact="${2:?required artifact root}"
soak_minutes="${3:-0}"
max_kib=1048576

if [[ ! "$soak_minutes" =~ ^[0-9]+$ || "$soak_minutes" -gt 240 ]]; then
  echo "MAC_ACCEPTANCE_HARD_STOP=InvalidSoakDuration:$soak_minutes" >&2
  exit 2
fi

case "$root" in
  "$HOME"/*) ;;
  *) echo "MAC_STORAGE_HARD_STOP=RootOutsideHome:$root" >&2; exit 2 ;;
esac
if [[ ! -d "$root" || ! -w "$root" ]]; then
  echo "MAC_STORAGE_HARD_STOP=MissingOrInaccessible:$root" >&2
  exit 2
fi
if [[ -f "$publish/DoNotPanicPortfolioVisualizer.App" ]]; then
  # ZIP extraction on macOS may drop the executable bit from self-contained hosts.
  chmod +x "$publish/DoNotPanicPortfolioVisualizer.App"
fi
if [[ ! -x "$publish/DoNotPanicPortfolioVisualizer.App" ]]; then
  echo "MAC_ACCEPTANCE_HARD_STOP=MissingExecutable:$publish" >&2
  exit 2
fi

check_budget() {
  local used
  used="$(du -sk "$root" | awk '{print $1}')"
  if [[ "$used" -gt "$max_kib" ]]; then
    echo "MAC_STORAGE_HARD_STOP=LimitExceeded:used_kib=$used;max_kib=$max_kib" >&2
    return 3
  fi
}

mkdir -p "$artifact"
check_budget
rm -f "$artifact/mac-config-window.png" "$artifact/mac-window-info.txt"
export DOTNET_ROOT="$root/dotnet"
export DOTNET_ROOT_X64="$root/dotnet"
export DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT="$root/local-data-cr019"
rm -rf "$DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT"
mkdir -p "$DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT"

if [[ "$soak_minutes" -gt 0 ]]; then
  mkdir -p "$artifact/screenshots"
  export DNPPV_PRODUCT_CAPTURE_PATH="$artifact/screenshots"
  export DNPPV_PRODUCT_CAPTURE_INTERVAL_MINUTES=30
else
  export DNPPV_CONFIGURATION_VALIDATION_MODE=1
  export DNPPV_CONFIG_CAPTURE_PATH="$artifact/mac-config-window.png"
fi

cd "$publish"
./DoNotPanicPortfolioVisualizer.App >/dev/null 2>&1 &
app_pid=$!
cleanup() {
  kill "$app_pid" 2>/dev/null || true
  pkill -P "$app_pid" 2>/dev/null || true
}
trap cleanup EXIT

# Big Sur can take more than a minute to materialize the Avalonia window.
for _ in $(seq 1 18); do
  sleep 5
  check_budget
done

if [[ "$soak_minutes" -gt 0 ]]; then
  printf 'MAC_SOAK_STARTED;MINUTES=%s\n' "$soak_minutes" > "$artifact/mac-soak.log"
  for ((elapsed=0; elapsed<soak_minutes; elapsed++)); do
    sleep 60
    if (( (elapsed + 1) % 30 == 0 )); then
      check_budget
    fi
  done
  printf 'MAC_SOAK_COMPLETED\n' >> "$artifact/mac-soak.log"
  check_budget
  trace="$DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT/Trace/trace.circular.log"
  mkdir -p "$artifact/trace"
  if [[ -s "$trace" ]]; then
    cp "$trace" "$artifact/trace/trace.circular.log"
    if [[ -f "${trace%.log}.idx" ]]; then cp "${trace%.log}.idx" "$artifact/trace/trace.circular.idx"; fi
  fi
  rss_usable=false
  ai_required=false
  ai_requested=false
  ai_succeeded=false
  [[ "${DNPPV_SOAK_REQUIRE_AI_NEWS:-}" == 1 ]] && ai_required=true
  if [[ -s "$trace" ]]; then
    grep -aEq 'event=RssPlaybackReady / state=(Fresh|Partial) / headline_count=[1-9][0-9]*' "$trace" && rss_usable=true
    grep -aEq 'event=AiSummaryRequestStarted([[:space:]]|/|$)' "$trace" && ai_requested=true
    grep -aEq 'event=AiSummarySucceeded([[:space:]]|/|$)' "$trace" && ai_succeeded=true
  fi
  cat > "$artifact/news-evidence.json" <<EOF
{
  "schema": "dnppv2-soak-news-evidence/v1",
  "rssUsable": $rss_usable,
  "aiRequired": $ai_required,
  "aiRequestObserved": $ai_requested,
  "aiSuccessObserved": $ai_succeeded,
  "traceFile": "trace/trace.circular.log"
}
EOF
  if [[ "$rss_usable" != true || "${DNPPV_SOAK_REQUIRE_AI_NEWS:-}" == 1 && ( "$ai_requested" != true || "$ai_succeeded" != true ) ]]; then
    echo "MAC_NEWS_EVIDENCE_HARD_STOP=RSS_OR_AI_TRACE_MISSING" >&2
    exit 4
  fi
  printf 'MAC_PRODUCT_SOAK=Passed;MINUTES=%s;ARTIFACT_ROOT=%s\n' "$soak_minutes" "$artifact"
  exit 0
fi

swift_source="$root/mac-window-capture.swift"
if [[ ! -s "$artifact/mac-config-window.png" ]]; then
cat > "$swift_source" <<'SWIFT'
import CoreGraphics
import Foundation
import ImageIO

let output = CommandLine.arguments[1]
let raw = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID)! as NSArray
var selected: UInt32?
for item in raw {
    guard let window = item as? NSDictionary,
          let owner = window["kCGWindowOwnerName"] as? String,
          owner.contains("DoNotPanicPortfolioVisualizer"),
          let number = window["kCGWindowNumber"] as? NSNumber else { continue }
    selected = number.uint32Value
    print("OWNER=\(owner) WINDOW_ID=\(number)")
    break
}
guard let windowId = selected else { fatalError("Mac product window was not found in CoreGraphics.") }
guard let image = CGWindowListCreateImage(.null, .optionIncludingWindow, windowId, [.bestResolution, .boundsIgnoreFraming]) else {
    fatalError("Mac product window could not be captured; grant Screen Recording permission to the SSH/terminal host.")
}
guard let destination = CGImageDestinationCreateWithURL(URL(fileURLWithPath: output) as CFURL, "public.png" as CFString, 1, nil) else {
    fatalError("Could not create the Mac PNG destination.")
}
CGImageDestinationAddImage(destination, image, nil)
guard CGImageDestinationFinalize(destination) else { fatalError("Could not finalize the Mac PNG artifact.") }
SWIFT

if ! swift "$swift_source" "$artifact/mac-config-window.png" > "$artifact/mac-window-info.txt" 2>&1; then
  # SSH-launched processes may lack Screen Recording entitlement while the
  # logged-in Terminal application already has it. Retry through that desktop
  # host, then wait for the real window artifact it produces.
  rm -f "$artifact/mac-config-window.png"
  capture_command="/usr/bin/swift '$swift_source' '$artifact/mac-config-window.png' > '$artifact/mac-window-info.txt' 2>&1"
  osascript <<APPLESCRIPT
tell application "Terminal"
  do script "$capture_command"
end tell
APPLESCRIPT
  for _ in $(seq 1 30); do
    [[ -s "$artifact/mac-config-window.png" ]] && break
    sleep 2
  done
fi
fi
rm -f "$swift_source"
check_budget
test -s "$artifact/mac-config-window.png"
printf 'MAC_CONFIG_WINDOW_VALIDATION=Passed;ARTIFACT_ROOT=%s\n' "$artifact"
