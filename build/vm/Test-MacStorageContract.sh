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

root="${1:-$HOME/SOFTWARE_DEV/DNPPV_20}"
max_kib="${2:-1048576}"

case "$root" in
  "$HOME"/*) ;;
  *) echo "MAC_STORAGE_HARD_STOP=RootOutsideHome:$root" >&2; exit 2 ;;
esac

if [[ ! -d "$root" || ! -w "$root" ]]; then
  echo "MAC_STORAGE_HARD_STOP=MissingOrInaccessible:$root" >&2
  exit 2
fi

usage_kib="$(du -sk "$root" | awk '{print $1}')"
if [[ "$usage_kib" -gt "$max_kib" ]]; then
  echo "MAC_STORAGE_HARD_STOP=LimitExceeded:used_kib=$usage_kib;max_kib=$max_kib" >&2
  exit 3
fi

printf 'MAC_STORAGE_CONTRACT=Passed;ROOT=%s;USED_KIB=%s;MAX_KIB=%s\n' "$root" "$usage_kib" "$max_kib"
