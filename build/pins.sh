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

export AGORA_VOICE_VERSION
AGORA_VOICE_VERSION="$(grep -oE '<AgoraVoiceVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraVoiceVersion>//')"
export AGORA_VOICE_BINDING_REVISION
AGORA_VOICE_BINDING_REVISION="$(grep -oE '<AgoraVoiceBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraVoiceBindingRevision>//')"
export AGORA_VOICE_PACKAGE_VERSION="${AGORA_VOICE_VERSION}.${AGORA_VOICE_BINDING_REVISION}"

export AGORA_SIGNALING_VERSION
AGORA_SIGNALING_VERSION="$(grep -oE '<AgoraSignalingVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraSignalingVersion>//')"
export AGORA_SIGNALING_BINDING_REVISION
AGORA_SIGNALING_BINDING_REVISION="$(grep -oE '<AgoraSignalingBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraSignalingBindingRevision>//')"
export AGORA_SIGNALING_PACKAGE_VERSION="${AGORA_SIGNALING_VERSION}.${AGORA_SIGNALING_BINDING_REVISION}"

export AGORA_CHAT_VERSION
AGORA_CHAT_VERSION="$(grep -oE '<AgoraChatVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraChatVersion>//')"
export AGORA_CHAT_BINDING_REVISION
AGORA_CHAT_BINDING_REVISION="$(grep -oE '<AgoraChatBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraChatBindingRevision>//')"
export AGORA_CHAT_PACKAGE_VERSION="${AGORA_CHAT_VERSION}.${AGORA_CHAT_BINDING_REVISION}"

export AGORA_IOT_VERSION
AGORA_IOT_VERSION="$(grep -oE '<AgoraIoTVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraIoTVersion>//')"
export AGORA_IOT_BINDING_REVISION
AGORA_IOT_BINDING_REVISION="$(grep -oE '<AgoraIoTBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraIoTBindingRevision>//')"
export AGORA_IOT_PACKAGE_VERSION="${AGORA_IOT_VERSION}.${AGORA_IOT_BINDING_REVISION}"

export AGORA_WHITEBOARD_VERSION
AGORA_WHITEBOARD_VERSION="$(grep -oE '<AgoraWhiteboardVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraWhiteboardVersion>//')"
export AGORA_WHITEBOARD_BINDING_REVISION
AGORA_WHITEBOARD_BINDING_REVISION="$(grep -oE '<AgoraWhiteboardBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraWhiteboardBindingRevision>//')"
export AGORA_WHITEBOARD_PACKAGE_VERSION="${AGORA_WHITEBOARD_VERSION}.${AGORA_WHITEBOARD_BINDING_REVISION}"

export AGORA_FASTBOARD_VERSION
AGORA_FASTBOARD_VERSION="$(grep -oE '<AgoraFastboardVersion>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraFastboardVersion>//')"
export AGORA_FASTBOARD_BINDING_REVISION
AGORA_FASTBOARD_BINDING_REVISION="$(grep -oE '<AgoraFastboardBindingRevision>[^<]+' "${AGORA_REPO_ROOT}/Directory.Build.props" | head -1 | sed 's/<AgoraFastboardBindingRevision>//')"
export AGORA_FASTBOARD_PACKAGE_VERSION="${AGORA_FASTBOARD_VERSION}.${AGORA_FASTBOARD_BINDING_REVISION}"
