#!/usr/bin/env bash
# Prints the version a build carries as its informational version and in its file names.
# Usage: build/version.sh <commit-sha>
# A build without a version tag gets the development version: 0.0.0-dev plus the commit's short hash.
set -euo pipefail

commit="${1:-}"
if ! [[ "$commit" =~ ^[0-9a-f]{7,40}$ ]]; then
    echo "version.sh: '$commit' is not a commit hash." >&2
    exit 1
fi

echo "0.0.0-dev+${commit:0:7}"
