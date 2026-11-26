#!/usr/bin/env bash
# Tests for appdir.sh: builds an AppDir around a stand-in executable and checks what the AppImage will hold.
# Run: build/appdir.test.sh
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

# A stand-in for the published executable that reports the arguments it was given.
printf '#!/bin/sh\necho "args: $*"\n' > "$work/ScreenshotTrimmer"
chmod +x "$work/ScreenshotTrimmer"

appdir="$work/ScreenshotTrimmer.AppDir"
if ! "$here/appdir.sh" "$work/ScreenshotTrimmer" "$appdir"; then
    echo "FAIL appdir.sh exited with an error"
    exit 1
fi
entry="$appdir/screenshot-trimmer.desktop"

check "the executable is in usr/bin and can be run" test -x "$appdir/usr/bin/ScreenshotTrimmer"
check "the desktop entry is at the AppDir's root" test -f "$entry"
check "the desktop entry names the app" grep -qx 'Name=Screenshot Trimmer' "$entry"
check "the desktop entry runs the executable with the opened files" grep -qx 'Exec=ScreenshotTrimmer %F' "$entry"
check "the desktop entry uses the icon" grep -qx 'Icon=screenshot-trimmer' "$entry"
check "the desktop entry offers the app for PNG, JPEG, WebP and BMP" \
    grep -qx 'MimeType=image/png;image/jpeg;image/webp;image/bmp;' "$entry"
check "the icon is the 256 px PNG at the AppDir's root" \
    cmp "$appdir/screenshot-trimmer.png" "$here/../src/ScreenshotTrimmer.App/Assets/Icon/screenshot-trimmer-256.png"
check "the icon is also in the hicolor theme" test -f "$appdir/usr/share/icons/hicolor/256x256/apps/screenshot-trimmer.png"
check "the scalable icon is in the hicolor theme" test -f "$appdir/usr/share/icons/hicolor/scalable/apps/screenshot-trimmer.svg"
check "AppRun can be run" test -x "$appdir/AppRun"
check "AppRun passes every argument to the executable" \
    test "$("$appdir/AppRun" 'a file.png' second.png)" = "args: a file.png second.png"
if command -v desktop-file-validate >/dev/null; then
    check "the desktop entry is valid" desktop-file-validate "$entry"
fi
check "building into an existing AppDir fails rather than mixing old files" \
    bash -c "! '$here/appdir.sh' '$work/ScreenshotTrimmer' '$appdir'"

[ "$failures" -eq 0 ] || { echo "$failures failed"; exit 1; }
