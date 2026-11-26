#!/usr/bin/env bash
# Builds ScreenshotTrimmer-<version>-x86_64.AppImage from the single-file linux-x64 executable.
# Usage: build/appimage.sh <published executable> <version> <output folder>
# Downloads appimagetool and the AppImage runtime at the versions pinned below, checks their SHA-256 before use,
# and runs appimagetool with --appimage-extract-and-run, so FUSE is not needed.
set -euo pipefail

# Pinned tools. To update, pick a release and copy the file's SHA-256 from its release page.
appimagetool_version=1.9.1
appimagetool_sha256=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0
appimagetool_url="https://github.com/AppImage/appimagetool/releases/download/$appimagetool_version/appimagetool-x86_64.AppImage"
# Given to appimagetool explicitly; otherwise it downloads the latest runtime, unpinned.
runtime_version=20251108
runtime_sha256=2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d
runtime_url="https://github.com/AppImage/type2-runtime/releases/download/$runtime_version/runtime-x86_64"

executable="${1:-}"
version="${2:-}"
output="${3:-}"
if [ -z "$executable" ] || [ -z "$version" ] || [ -z "$output" ]; then
    echo "Usage: appimage.sh <published executable> <version> <output folder>" >&2
    exit 1
fi

here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

download() { # download <url> <sha256> <file>
    curl --fail --silent --show-error --location --retry 3 --output "$3" "$1"
    if ! echo "$2  $3" | sha256sum --check --status; then
        echo "appimage.sh: $1 does not match its pinned SHA-256." >&2
        exit 1
    fi
}

download "$appimagetool_url" "$appimagetool_sha256" "$work/appimagetool"
download "$runtime_url" "$runtime_sha256" "$work/runtime"
chmod +x "$work/appimagetool"

"$here/appdir.sh" "$executable" "$work/ScreenshotTrimmer.AppDir"

mkdir -p "$output"
appimage="$output/ScreenshotTrimmer-$version-x86_64.AppImage"
ARCH=x86_64 "$work/appimagetool" --appimage-extract-and-run --no-appstream --runtime-file "$work/runtime" \
    "$work/ScreenshotTrimmer.AppDir" "$appimage"
echo "Built $appimage"
