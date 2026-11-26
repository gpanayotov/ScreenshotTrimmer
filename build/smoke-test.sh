#!/usr/bin/env bash
# Runs a packaged build's hidden --smoke-test switch and checks it passed and printed the expected version.
# Usage: build/smoke-test.sh <version> <executable> [arguments before --smoke-test...]
# For example: build/smoke-test.sh 1.2.3 ./ScreenshotTrimmer-1.2.3-x86_64.AppImage --appimage-extract-and-run
set -euo pipefail

version="${1:-}"
executable="${2:-}"
if [ -z "$version" ] || [ -z "$executable" ]; then
    echo "Usage: smoke-test.sh <version> <executable> [arguments...]" >&2
    exit 1
fi
shift 2

if [ ! -x "$executable" ]; then
    echo "smoke-test.sh: $executable is not an executable file." >&2
    exit 1
fi

output="$(mktemp)"
trap 'rm -f "$output"' EXIT

"$executable" "$@" --smoke-test | tee "$output"
if ! grep -qF "ScreenshotTrimmer $version" "$output"; then
    echo "smoke-test.sh: the smoke test did not print version $version." >&2
    exit 1
fi
