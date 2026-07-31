#!/usr/bin/env bash
set -euo pipefail

# Builds the device test app against a packed Net.Agora.<PACKAGE>.Android package, installs it on
# a running Android emulator and runs its checks. The app reports its verdict to logcat under a
# single tag; this script turns that into an exit code.
#
# Assumes an emulator is already booted and visible to adb - in CI that is
# reactivecircus/android-emulator-runner, locally it is whatever you started yourself.
#
# Usage: run-emulator-tests.sh VERSION [TARGET_FRAMEWORK] [PACKAGE]
#
# PACKAGE is the packages.tsv id — Video (default), Voice, Signaling, Chat, Whiteboard or
# Fastboard. One run exercises one package: the two RTC packages carry the same Java classes, so a
# single app cannot hold both, and every product swaps in its own suite behind its own define (see
# tests/Net.Agora.Android.DeviceTests). IoT is not a flavor — it bundles private copies of the RTC
# and RTM SDKs and so shares an app with nothing.
#
# Environment:
#   AGORA_SHRINK=1          build with Java shrinking (R8) on
#   AGORA_WITH_SIGNALING=1  also reference Signaling (Video/Voice flavors only)
#
# VERSION is that package's own version: the products sit on independent native version lines, so
# there is no single number that spans them. See build/pins.sh.

VERSION="${1:?a package version is required}"
TARGET_FRAMEWORK="${2:-net10.0-android36.0}"
PACKAGE="${3:-Video}"

PACKAGE_NAME="com.sbokatuk.agora.android.devicetests"
LOG_FILE="emulator-tests.log"
LOG_TAG="AgoraE2E"
# CI emulators are x86_64. Override for a local arm64 emulator on Apple silicon.
DEVICE_RID="${AGORA_DEVICE_RID:-android-x64}"
POLL_ATTEMPTS=90
POLL_INTERVAL=5

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT="${REPO_ROOT}/tests/Net.Agora.Android.DeviceTests/Net.Agora.Android.DeviceTests.csproj"

# The SDK band is chosen by the *Android API level* in the target framework, not by the .NET
# version alone, because that is what decides which workload owns the runtime packs:
#
#   net8.0-android34.0  -> android 34.0.x, in the .NET 8 band
#   net9.0-android35.0  -> android 35.0.x, in the .NET 9 band
#   net10.0-android36.0 -> android 36.0.x, in the .NET 10 band
#
# The .NET 9 band compiles a net8 app happily - it has the API 34 *reference* packs - and then
# fails at packaging time, because it has no API 34 *runtime* packs and they cannot be restored
# from NuGet (NETSDK1112). The runtime packs come from the workload, not from a restore. The SDK
# is resolved from the working directory, and this repository's global.json pins .NET 9, hence
# the scratch directory below.
case "${TARGET_FRAMEWORK}" in
    net10.0-*) sdk_major=10 ;;
    net8.0-*)  sdk_major=8 ;;
    *)         sdk_major=9 ;;
esac

sdk_version="$(dotnet --list-sdks | grep "^${sdk_major}\." | tail -1 | cut -d' ' -f1)"
if [ -z "${sdk_version}" ]; then
    echo "::error::no .NET ${sdk_major} SDK installed, cannot build ${TARGET_FRAMEWORK}"
    exit 1
fi

SDK_DIR="$(mktemp -d)"
trap 'rm -rf "${SDK_DIR}"' EXIT
printf '{ "sdk": { "version": "%s", "rollForward": "latestFeature" } }\n' "${sdk_version}" \
    > "${SDK_DIR}/global.json"

# Signaling's own version, for the AGORA_WITH_SIGNALING run below — resolved here because the
# cache purge needs it too. The products sit on independent version lines, so VERSION (the chosen
# product's) says nothing about Signaling's; only the prerelease suffix carries across, and it has
# to, since a pull request packs every package as <version>-beta.<pr>.<run> and an exact-version
# PackageReference would otherwise ask for one that is not in ./artifacts.
. "${REPO_ROOT}/build/pins.sh"
case "${VERSION}" in
    *-*) SIGNALING_VERSION="${AGORA_SIGNALING_PACKAGE_VERSION}-${VERSION#*-}" ;;
    *)   SIGNALING_VERSION="${AGORA_SIGNALING_PACKAGE_VERSION}" ;;
esac

# NuGet caches by package id + version, so rebuilding a version that was already restored once
# silently reuses the stale copy. CI versions are unique, but locally you will re-pack the same
# version repeatedly and test yesterday's bits without this. Driven by packages.tsv so a package
# added there is purged here without a second edit.
while IFS=$'\t' read -r name _rest; do
    case "${name}" in ''|\#*) continue ;; esac
    lower="$(printf '%s' "${name}" | tr '[:upper:]' '[:lower:]')"
    rm -rf "${HOME}/.nuget/packages/net.agora.${lower}.android/${VERSION}"
done < "${REPO_ROOT}/build/packages.tsv"

# And Signaling at its own version, which the loop above cannot reach: it keys every package id on
# VERSION, on the old assumption that an app holds one product. Only matters locally — CI versions
# are unique — but locally it is the difference between testing this pack and yesterday's.
rm -rf "${HOME}/.nuget/packages/net.agora.signaling.android/${SIGNALING_VERSION}"

rm -rf "${REPO_ROOT}/tests/Net.Agora.Android.DeviceTests/obj" \
       "${REPO_ROOT}/tests/Net.Agora.Android.DeviceTests/bin"

# AGORA_SHRINK=1 turns Java shrinking (R8) on for this run, which validates the keep rules the
# packages ship in buildTransitive/ — see the .csproj comment. Off by default: the plain legs
# should keep meaning "the .aar is not in the app / the binding does not drive the SDK".
SHRINK_ARGS=()
if [ "${AGORA_SHRINK:-0}" = "1" ]; then
    SHRINK_ARGS=(-p:AgoraShrinkTest=true)
fi

# AGORA_WITH_SIGNALING=1 additionally references Net.Agora.Signaling.Android, at its own pin from
# build/pins.sh, without changing which suite compiles. Valid on the Video and Voice flavors only.
# This is the regression test for the aosl conflict signaling-v2.2.6.3 fixes: the app builds and
# installs identically either way, and only the engine-creation check on a real device tells the
# two apart. See the .csproj comment.
COEXIST_ARGS=()
if [ "${AGORA_WITH_SIGNALING:-0}" = "1" ]; then
    COEXIST_ARGS=(-p:AgoraDeviceWithSignaling=true
                  -p:AgoraSignalingPackageVersion="${SIGNALING_VERSION}")
fi

echo "==> building device tests (package=Net.Agora.${PACKAGE}.Android, version=${VERSION}, tfm=${TARGET_FRAMEWORK}, sdk=${sdk_version}, shrink=${AGORA_SHRINK:-0}, signaling=${AGORA_WITH_SIGNALING:-0})"
# Debug, not Release. Release AOT-compiles every assembly, and an AOT image built against an
# unlinked assembly set disagrees with what the runtime loads - the app aborts on startup before a
# single check runs. Debug also skips the R8 shrinking this app avoids by default - see the
# comments in the .csproj.
( cd "${SDK_DIR}" && dotnet build "${PROJECT}" \
    --configuration Debug \
    -p:AgoraDevicePackage="${PACKAGE}" \
    -p:AgoraDevicePackageVersion="${VERSION}" \
    -p:AgoraDeviceTargetFramework="${TARGET_FRAMEWORK}" \
    -p:RuntimeIdentifier="${DEVICE_RID}" \
    ${SHRINK_ARGS[@]+"${SHRINK_ARGS[@]}"} \
    ${COEXIST_ARGS[@]+"${COEXIST_ARGS[@]}"} \
    -t:Install )

echo "==> granting camera/microphone permissions"
# Dangerous (runtime) permissions on Android 6+: declaring them in AndroidManifest.xml is not
# enough to have them granted. Without this, EnableVideo/StartPreview report a different failure
# than the one SmokeTests.cs isolates.
adb shell pm grant "${PACKAGE_NAME}" android.permission.CAMERA
adb shell pm grant "${PACKAGE_NAME}" android.permission.RECORD_AUDIO

echo "==> launching"
adb logcat -c
# The activity name is pinned in the app rather than left to the generated crc64* name, so this
# target stays stable across builds.
adb shell am start -n "${PACKAGE_NAME}/.MainActivity"

echo "==> waiting for the verdict"
for _ in $(seq "${POLL_ATTEMPTS}"); do
    if adb logcat -d -s "${LOG_TAG}:*" | grep -q "AGORA_E2E_DONE"; then
        break
    fi
    sleep "${POLL_INTERVAL}"
done

adb logcat -d -s "${LOG_TAG}:*" | tee "${LOG_FILE}"

if ! grep -q "AGORA_E2E_DONE PASS" "${LOG_FILE}"; then
    # No verdict usually means the app died before reporting, so keep the crash trace. A missing
    # Java dependency shows up here as a NoClassDefFoundError naming the class.
    echo "==> no passing verdict; capturing crash output"
    adb logcat -d -s AndroidRuntime:E DEBUG:F "${PACKAGE_NAME}:*" 2>/dev/null \
        | tail -100 | tee -a "${LOG_FILE}" || true
    echo "::error::Agora emulator smoke tests failed or timed out"
    exit 1
fi

echo "==> emulator smoke tests passed"
