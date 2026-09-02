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
max_kib=1048576

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
export DNPPV_CONFIGURATION_VALIDATION_MODE=1
export DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT="$root/local-data-cr019"
rm -rf "$DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT"
mkdir -p "$DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT"

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

swift_source="$root/mac-window-capture.swift"
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

swift "$swift_source" "$artifact/mac-config-window.png" > "$artifact/mac-window-info.txt"
rm -f "$swift_source"
check_budget
test -s "$artifact/mac-config-window.png"
printf 'MAC_CONFIG_WINDOW_VALIDATION=Passed;ARTIFACT_ROOT=%s\n' "$artifact"
