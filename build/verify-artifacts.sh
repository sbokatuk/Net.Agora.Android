#!/usr/bin/env bash
set -euo pipefail

# Verifies every native artifact this repository binds against the SHA-256 recorded in
# build/checksums.txt, by downloading each coordinate straight from the repository the binding
# resolves it from.
#
# Why this exists: nothing else here pins the *bytes*. The versions in Directory.Build.props are
# exact, but a Maven coordinate is a mutable pointer — a re-published artifact keeps its version —
# and AndroidMavenLibrary performs no content check of its own (what the .NET Android SDK calls
# Java dependency verification is a class-graph completeness check, XA4241/XA4242, not a digest).
# The netless artifacts are worse: JitPack builds them on demand from a git tag, and a moved tag
# changes the bytes under an unchanged coordinate.
#
# Deliberately independent of the SDK's own download path and cache layout, so it keeps working
# across SDK versions: it asks the repository for the same coordinate the build will ask for and
# checks what comes back. That detects the threat this guards against (an artifact changing under
# a pinned version) without depending on MSBuild internals.
#
# Usage:
#   ./build/verify-artifacts.sh              # verify every coordinate against build/checksums.txt
#   ./build/verify-artifacts.sh --print      # print the coordinates and their hashes, for updating
#                                            # build/checksums.txt after a version bump
#
# Coordinates come from the csproj files and Directory.Build.props via build/pins.sh, so a package
# added to the repository is covered here without a second edit.

cd "$(dirname "$0")/.."

ROOT="$(pwd)"
CHECKSUMS="$ROOT/build/checksums.txt"
MODE="verify"

if [ "${1:-}" = "--print" ]; then
    MODE="print"
elif [ $# -gt 0 ]; then
    echo "usage: $0 [--print]" >&2
    exit 1
fi

sha256_of() {
    if command -v shasum >/dev/null 2>&1; then
        shasum -a 256 "$1" | cut -d' ' -f1
    else
        sha256sum "$1" | cut -d' ' -f1
    fi
}

# group:artifact:version:repository, harvested from every binding project. AgoraMavenArtifact rows
# a project adds below its Import (aosl, DSBridge) are picked up alongside the project's own
# artifact.
coordinates() {
    python3 - "$ROOT" <<'PYEOF'
import re, sys
from pathlib import Path

root = Path(sys.argv[1])
props = (root / "Directory.Build.props").read_text()


def prop(name):
    match = re.search(rf"<{name}>([^<]+)</{name}>", props)
    return match.group(1) if match else None


def resolve(version):
    """A version as MSBuild would see it: a bare literal, or one $(...) into Directory.Build.props.

    Both the project's own AgoraNativeVersion and its AgoraMavenArtifact rows go through here.
    They did not always: the extra rows took the attribute literally, which was invisible while
    every one of them happened to be a literal, and produced a coordinate of
    'io.agora.infra:aosl:$(AgoraAoslVersion)' — and a 404 — the moment one used a property.
    """
    reference = re.fullmatch(r"\$\((\w+)\)", version)
    return (prop(reference.group(1)) or "") if reference else version


for csproj in sorted(root.glob("src/*/*.csproj")):
    text = csproj.read_text()

    def read(name):
        match = re.search(rf"<{name}>([^<]+)</{name}>", text)
        return match.group(1) if match else None

    group, artifact = read("AgoraGroupId"), read("AgoraArtifact")
    if not group or not artifact:
        continue

    version = resolve(read("AgoraNativeVersion") or "")
    repository = read("AgoraMavenRepository") or "Central"
    print(f"{group}:{artifact}:{version}:{repository}")

    # Extra rows, e.g. <AgoraMavenArtifact Include="io.agora.infra:aosl" Version="$(AgoraAoslVersion)" />
    for extra in re.finditer(
            r'<AgoraMavenArtifact\s+Include="([^:"]+):([^"]+)"\s+Version="([^"]+)"'
            r'(?:[^>]*?Repository="([^"]+)")?', text):
        print(f"{extra.group(1)}:{extra.group(2)}:{resolve(extra.group(3))}:"
              f"{extra.group(4) or repository}")
PYEOF
}

url_for() {
    group="$1"; artifact="$2"; version="$3"; repository="$4"
    path="$(printf '%s' "$group" | tr '.' '/')"

    case "$repository" in
        Central) base="https://repo1.maven.org/maven2" ;;
        Google)  base="https://dl.google.com/dl/android/maven2" ;;
        http*)   base="${repository%/}" ;;
        *)
            echo "error: unknown repository '$repository' for $group:$artifact" >&2
            return 1
            ;;
    esac

    printf '%s/%s/%s/%s/%s-%s.aar\n' "$base" "$path" "$artifact" "$version" "$artifact" "$version"
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Written to a file rather than piped into the loop: a `... | while` loop runs in a subshell, so
# the failure counter would not survive it and the script could never exit non-zero.
coordinates | sort -u > "$WORK/coordinates"

if [ ! -s "$WORK/coordinates" ]; then
    echo "error: no coordinates found — has the repository layout changed?" >&2
    exit 1
fi

failures=0
checked=0

while IFS=: read -r group artifact version repository; do
    if [ -z "$version" ]; then
        echo "error: could not resolve a version for $group:$artifact" >&2
        exit 1
    fi

    key="$group:$artifact:$version"
    url="$(url_for "$group" "$artifact" "$version" "$repository")"

    if ! curl -fsSL -o "$WORK/artifact.aar" "$url"; then
        echo "FAIL  $key — could not download from $url" >&2
        failures=$((failures + 1))
        continue
    fi

    actual="$(sha256_of "$WORK/artifact.aar")"

    if [ "$MODE" = "print" ]; then
        printf '%s  %s\n' "$key" "$actual"
        continue
    fi

    expected="$(sed -n "s|^${key}[[:space:]]\{1,\}\([0-9a-f]\{64\}\).*|\1|p" "$CHECKSUMS" | head -1)"

    if [ -z "$expected" ]; then
        echo "FAIL  $key — no SHA-256 recorded in build/checksums.txt" >&2
        echo "      run '$0 --print' and add the line" >&2
        failures=$((failures + 1))
    elif [ "$expected" != "$actual" ]; then
        echo "FAIL  $key — the artifact does not match build/checksums.txt" >&2
        echo "      expected $expected" >&2
        echo "      actual   $actual" >&2
        failures=$((failures + 1))
    else
        echo "ok    $key"
    fi

    checked=$((checked + 1))
done < "$WORK/coordinates"

if [ "$MODE" = "print" ]; then
    exit 0
fi

if [ "$failures" -gt 0 ]; then
    echo "::error::$failures of $checked native artifacts do not match build/checksums.txt" >&2
    exit 1
fi

echo "all $checked native artifacts match build/checksums.txt"
