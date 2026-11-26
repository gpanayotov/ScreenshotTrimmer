#!/usr/bin/env bash
# Tests for launch-check.sh: runs it on stand-in executables that keep running or exit early, under a stand-in
# xvfb-run that starts its command with no display.
# Run: build/launch-check.test.sh
set -u
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
failures=0

check() { # check <description> <command...>
    local description="$1"; shift
    if "$@" >/dev/null 2>&1; then
        echo "ok   $description"
    else
        echo "FAIL $description"
        failures=$((failures + 1))
    fi
}

fails() { ! "$@"; }

stand_in() { # stand_in <name> <script body>
    printf '#!/bin/sh\n%s\n' "$2" > "$work/$1"
    chmod +x "$work/$1"
}

mkdir "$work/bin"
printf '#!/bin/sh\n[ "$1" = "--auto-servernum" ] && shift\nexec "$@"\n' > "$work/bin/xvfb-run"
chmod +x "$work/bin/xvfb-run"
export PATH="$work/bin:$PATH"

stand_in running 'sleep 30'
stand_in crashing 'exit 3'
stand_in quitting 'exit 0'
stand_in needs-argument '[ "$1" = "--appimage-extract-and-run" ] && sleep 30'

check "an app still running when the time runs out passes" "$here/launch-check.sh" 1 "$work/running"
check "an app that crashes fails" fails "$here/launch-check.sh" 1 "$work/crashing"
check "an app that quits on its own fails" fails "$here/launch-check.sh" 1 "$work/quitting"
check "arguments after the executable are passed on" \
    "$here/launch-check.sh" 1 "$work/needs-argument" --appimage-extract-and-run
check "no arguments fails" fails "$here/launch-check.sh"

if [ "$failures" -ne 0 ]; then
    echo "$failures failed"
    exit 1
fi
echo "All passed"
