#!/usr/bin/env bash
# Builds EventPlanner.app, EventPlanner-macos-<arch>.dmg and .zip from artifacts/publish/<rid>.
# Must run on macOS (hdiutil, codesign). The bundle is ad-hoc signed only: this is NOT a
# Developer ID signature and Gatekeeper will warn on first launch (documented unsigned state).
# Usage: build/package-macos.sh osx-arm64 0.1.0
set -euo pipefail

rid="${1:?rid (osx-arm64|osx-x64) required}"
version="${2:?version required}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Invalid version: $version" >&2; exit 1; }

case "$rid" in
  osx-arm64) arch="arm64" ;;
  osx-x64) arch="x64" ;;
  *) echo "Unsupported rid: $rid" >&2; exit 1 ;;
esac

root="$(cd "$(dirname "$0")/.." && pwd)"
publish="$root/artifacts/publish/$rid"
packages="$root/artifacts/packages"
staging="$root/artifacts/staging/$rid"
app="$staging/EventPlanner.app"

[[ -x "$publish/EventPlanner" ]] || { echo "Run build/publish.ps1 -Rid $rid first." >&2; exit 1; }

rm -rf "$staging"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$packages"
cp -R "$publish/." "$app/Contents/MacOS/"
sed "s/__VERSION__/$version/g" "$root/installer/macos/Info.plist" > "$app/Contents/Info.plist"

# Apple Silicon refuses to run unsigned native code; an ad-hoc signature is required to launch at all.
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"

zip_path="$packages/EventPlanner-macos-$arch.zip"
dmg_path="$packages/EventPlanner-macos-$arch.dmg"
rm -f "$zip_path" "$dmg_path"

# ditto keeps bundle metadata and symlinks intact (plain zip can break the signature).
ditto -c -k --keepParent "$app" "$zip_path"

dmg_root="$staging/dmg"
mkdir -p "$dmg_root"
cp -R "$app" "$dmg_root/"
ln -s /Applications "$dmg_root/Applications"
hdiutil create -volname "EventPlanner" -srcfolder "$dmg_root" -ov -format UDZO "$dmg_path"

echo "macOS packages written to $packages"
