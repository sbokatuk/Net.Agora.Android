#!/usr/bin/env bash
# The only parser of Directory.Build.props for shell callers. Source this, don't execute it.
#
#   . build/pins.sh
#   echo "$AGORA_VIDEO_VERSION"

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export AGORA_REPO_ROOT
AGORA_REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

export AGORA_VIDEO_VERSION
AGORA_VIDEO_VERSION="$(grep -oE '<AgoraVideoVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraVideoVersion>//')"
export AGORA_VIDEO_BINDING_REVISION
AGORA_VIDEO_BINDING_REVISION="$(grep -oE '<AgoraVideoBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraVideoBindingRevision>//')"
export AGORA_VIDEO_PACKAGE_VERSION="${AGORA_VIDEO_VERSION}.${AGORA_VIDEO_BINDING_REVISION}"
