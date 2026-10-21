#!/bin/sh
# Marks a .NET app host as linked against the installed macOS SDK, and
# signs it again ad hoc: see Mitsuamia.targets.
set -e
host="$1"
minimum="$2"
[ -f "$host" ] || exit 0
sdk=$(xcrun --show-sdk-version)
if vtool -show-build "$host" | grep -q "sdk $sdk\$"; then
  exit 0
fi
vtool -set-build-version macos "$minimum" "$sdk" -replace -output "$host.marked" "$host" 2>/dev/null
mv "$host.marked" "$host"
codesign --sign - --force "$host" 2>/dev/null
