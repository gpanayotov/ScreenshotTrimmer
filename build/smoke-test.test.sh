#!/usr/bin/env bash
# Tests for smoke-test.sh: runs it on stand-in executables that pass, fail, or print the wrong version.
# Run: build/smoke-test.test.sh
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

stand_in passing 'echo "ScreenshotTrimmer 1.2.3 ($*)"; echo "Smoke test passed."'
stand_in failing 'echo "ScreenshotTrimmer 1.2.3"; echo "Smoke test failed."; exit 1'
stand_in other-version 'echo "ScreenshotTrimmer 9.9.9"; echo "Smoke test passed."'
stand_in needs-switch '[ "$1" = "--appimage-extract-and-run" ] && [ "$2" = "--smoke-test" ] && echo "ScreenshotTrimmer 1.2.3"'

check "a passing executable that prints the version passes" "$here/smoke-test.sh" 1.2.3 "$work/passing"
check "the executable is given --smoke-test" \
    sh -c '"$1" 1.2.3 "$2" | grep -F -- "(--smoke-test)"' _ "$here/smoke-test.sh" "$work/passing"
check "the executable's output is shown" \
    sh -c '"$1" 1.2.3 "$2" | grep -F "Smoke test passed."' _ "$here/smoke-test.sh" "$work/passing"
check "a failing executable fails" fails "$here/smoke-test.sh" 1.2.3 "$work/failing"
check "an executable printing another version fails" fails "$here/smoke-test.sh" 1.2.3 "$work/other-version"
check "arguments after the executable come before --smoke-test" \
    "$here/smoke-test.sh" 1.2.3 "$work/needs-switch" --appimage-extract-and-run
check "a missing executable fails" fails "$here/smoke-test.sh" 1.2.3 "$work/missing"
check "no arguments fails" fails "$here/smoke-test.sh"

if [ "$failures" -ne 0 ]; then
    echo "$failures failed"
    exit 1
fi
echo "All passed"
