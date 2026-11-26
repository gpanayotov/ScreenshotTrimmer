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

[ "$failures" -eq 0 ] || { echo "$failures failed"; exit 1; }
