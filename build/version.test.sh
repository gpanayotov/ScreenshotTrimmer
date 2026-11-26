#!/usr/bin/env bash
# Tests for version.sh over sample inputs. Run: build/version.test.sh
set -u
here="$(cd "$(dirname "$0")" && pwd)"
failures=0

expect() { # expect <description> <expected output> <args...>
    local description="$1" expected="$2"; shift 2
    local actual
    if actual="$("$here/version.sh" "$@" 2>/dev/null)" && [ "$actual" = "$expected" ]; then
        echo "ok   $description"
    else
        echo "FAIL $description: expected '$expected', got '${actual:-<failure>}'"
        failures=$((failures + 1))
    fi
}

expect_failure() { # expect_failure <description> <args...>
    local description="$1"; shift
    if "$here/version.sh" "$@" >/dev/null 2>&1; then
        echo "FAIL $description: expected a failure"
        failures=$((failures + 1))
    else
        echo "ok   $description"
    fi
}

expect "an untagged build gets the development version with the short commit hash" \
    "0.0.0-dev+1a2b3c4" "1a2b3c4d5e6f708192a3b4c5d6e7f80912a3b4c5"
expect "a short commit hash is used as it is" "0.0.0-dev+1a2b3c4" "1a2b3c4"
expect_failure "no commit fails" ""
expect_failure "a commit that is not a hash fails" "main"

expect "a version tag gives its version" "1.2.3" --tag v1.2.3
expect "a pre-release tag keeps its suffix" "1.2.3-rc.1" --tag v1.2.3-rc.1
expect_failure "a tag without the v fails" --tag 1.2.3
expect_failure "a tag with two numbers fails" --tag v1.2
expect_failure "a tag with leading zeros fails" --tag v01.2.3
expect_failure "a tag with an empty suffix fails" --tag v1.2.3-
expect_failure "a tag with a word fails" --tag v1.2.3.beta
expect_failure "a tag that is not a version fails" --tag release
expect_failure "no tag fails" --tag ""

[ "$failures" -eq 0 ] || { echo "$failures failed"; exit 1; }
