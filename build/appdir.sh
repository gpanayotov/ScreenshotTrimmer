#!/usr/bin/env bash
# Builds the AppDir the AppImage is made from: the single-file executable, AppRun, the desktop entry and the icon.
# Usage: build/appdir.sh <published executable> <AppDir to create>
set -euo pipefail

executable="${1:-}"
appdir="${2:-}"
if [ -z "$executable" ] || [ -z "$appdir" ]; then
    echo "Usage: appdir.sh <published executable> <AppDir to create>" >&2
    exit 1
fi
if [ ! -x "$executable" ]; then
    echo "appdir.sh: '$executable' is not an executable file." >&2
    exit 1
fi
if [ -e "$appdir" ]; then
    echo "appdir.sh: '$appdir' already exists." >&2
    exit 1
fi

icons="$(cd "$(dirname "$0")/../src/ScreenshotTrimmer.App/Assets/Icon" && pwd)"

install -D -m 755 "$executable" "$appdir/usr/bin/ScreenshotTrimmer"
install -D -m 644 "$icons/screenshot-trimmer-256.png" "$appdir/usr/share/icons/hicolor/256x256/apps/screenshot-trimmer.png"
install -D -m 644 "$icons/screenshot-trimmer.svg" "$appdir/usr/share/icons/hicolor/scalable/apps/screenshot-trimmer.svg"
# appimagetool takes the icon named by the desktop entry from the AppDir's root.
install -m 644 "$icons/screenshot-trimmer-256.png" "$appdir/screenshot-trimmer.png"

# %F: "Open with" passes every chosen file, and the app opens them as it does from the command line.
cat > "$appdir/screenshot-trimmer.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=Screenshot Trimmer
Comment=Trim screenshots down to their subject
Exec=ScreenshotTrimmer %F
Icon=screenshot-trimmer
Terminal=false
Categories=Graphics;
MimeType=image/png;image/jpeg;image/webp;image/bmp;
EOF

# The AppImage runtime sets APPDIR; falling back to AppRun's own folder lets the AppDir run unpacked too.
cat > "$appdir/AppRun" <<'EOF'
#!/bin/sh
here="${APPDIR:-$(dirname "$(readlink -f "$0")")}"
exec "$here/usr/bin/ScreenshotTrimmer" "$@"
EOF
chmod 755 "$appdir/AppRun"
