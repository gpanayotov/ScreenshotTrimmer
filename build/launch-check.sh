#!/usr/bin/env bash
# Starts a packaged build for real under a virtual display (Xvfb) and checks it is still running after a few seconds,
# so interface startup is covered. Linux only; the Windows leg of the package workflow does the same in PowerShell.
# Usage: build/launch-check.sh <seconds> <executable> [arguments...]
set -uo pipefail

seconds="${1:-}"
executable="${2:-}"
if [ -z "$seconds" ] || [ -z "$executable" ]; then
    echo "Usage: launch-check.sh <seconds> <executable> [arguments...]" >&2
    exit 1
fi
shift 2

# timeout exits with 124 only when the app was still running when the time ran out.
code=0
timeout "$seconds" xvfb-run --auto-servernum "$executable" "$@" || code=$?
if [ "$code" -ne 124 ]; then
    echo "launch-check.sh: $executable exited with $code within $seconds seconds." >&2
    exit 1
fi
echo "$executable was still running after $seconds seconds."
