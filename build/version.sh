#!/usr/bin/env bash
# Prints the version a build carries as its informational version and in its file names.
# Usage: build/version.sh <commit-sha>
#        build/version.sh --tag <tag>
# A version tag is v plus a semantic version (semver.org) and gives that version: v1.2.3 gives 1.2.3,
# v1.2.3-rc.1 gives 1.2.3-rc.1. Any other tag fails.
# A build without a version tag gets the development version: 0.0.0-dev plus the commit's short hash.
set -euo pipefail

if [ "${1:-}" = "--tag" ]; then
    tag="${2:-}"
    number='(0|[1-9][0-9]*)'
    identifier='(0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
    semver="$number\.$number\.$number(-$identifier(\.$identifier)*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?"
    if ! [[ "$tag" =~ ^v$semver$ ]]; then
        echo "version.sh: '$tag' is not a version tag: v plus a semantic version, such as v1.2.3 or v1.2.3-rc.1." >&2
        exit 1
    fi
    echo "${tag#v}"
    exit 0
fi

commit="${1:-}"
if ! [[ "$commit" =~ ^[0-9a-f]{7,40}$ ]]; then
    echo "version.sh: '$commit' is not a commit hash." >&2
    exit 1
fi

echo "0.0.0-dev+${commit:0:7}"
