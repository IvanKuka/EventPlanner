#!/usr/bin/env bash
# Validates that exactly the six expected user packages exist, then writes SHA256SUMS and a
# release manifest. A release with a missing or extra package is rejected.
# Usage: build/validate-release.sh <packages-dir> <version> <commit-sha>
set -euo pipefail

dir="${1:?packages dir required}"
version="${2:?version required}"
commit="${3:?commit sha required}"

expected=(
  "EventPlanner-Setup-x64.exe"
  "EventPlanner-windows-x64.zip"
  "EventPlanner-macos-arm64.dmg"
  "EventPlanner-macos-arm64.zip"
  "EventPlanner-macos-x64.dmg"
  "EventPlanner-macos-x64.zip"
)

cd "$dir"
for file in "${expected[@]}"; do
  [[ -s "$file" ]] || { echo "Missing or empty package: $file" >&2; exit 1; }
done

actual_count=$(find . -maxdepth 1 -type f -name 'EventPlanner-*' | wc -l | tr -d ' ')
if [[ "$actual_count" -ne "${#expected[@]}" ]]; then
  echo "Expected ${#expected[@]} packages, found $actual_count" >&2
  ls -la >&2
  exit 1
fi

sha256sum "${expected[@]}" > SHA256SUMS

{
  echo "{"
  echo "  \"appId\": \"EventPlanner\","
  echo "  \"version\": \"$version\","
  echo "  \"commit\": \"$commit\","
  echo "  \"signed\": false,"
  echo "  \"files\": ["
  for i in "${!expected[@]}"; do
    file="${expected[$i]}"
    hash=$(sha256sum "$file" | cut -d' ' -f1)
    size=$(stat -c %s "$file" 2>/dev/null || stat -f %z "$file")
    sep=","; [[ $i -eq $((${#expected[@]} - 1)) ]] && sep=""
    echo "    { \"name\": \"$file\", \"size\": $size, \"sha256\": \"$hash\" }$sep"
  done
  echo "  ]"
  echo "}"
} > release-manifest.json

echo "Release $version validated: ${#expected[@]} packages"
