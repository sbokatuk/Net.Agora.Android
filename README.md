# Net.Agora.Android

[![NuGet](https://img.shields.io/nuget/v/Net.Agora.Video.Android?label=nuget)](https://www.nuget.org/packages/Net.Agora.Video.Android)
[![Targets: net8.0 | net9.0 | net10.0](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0%20%7C%20net10.0-512BD4)](#packages)
[![full-rtc-basic 4.6.3](https://img.shields.io/badge/full--rtc--basic-4.6.3-099DFD)](https://central.sonatype.com/artifact/io.agora.rtc/full-rtc-basic)
[![Licence: MIT](https://img.shields.io/badge/licence-MIT-green)](LICENSE)

.NET for Android and .NET MAUI bindings for Agora's native Android SDKs.

Only **Video** (RTC) is bound today. Join a channel and publish/subscribe audio and video from C#,
in a `net8.0-android`, `net9.0-android` or `net10.0-android` app.

```bash
dotnet add package Net.Agora.Video.Android
```

This is a raw platform binding — the full `class-parse`-generated surface, `Agora.Rtc.RtcEngine`
and friends, under the `Agora.Rtc` namespace (renamed from the Java package `io.agora.rtc2` to
match Agora's own C# / Unity SDK naming). Most apps want the cross-platform client instead:
[`Net.Agora.Video`](https://github.com/sbokatuk/Net.Agora), which wraps this package and its iOS
sibling behind one API. Reach for this package directly only when you need something the
cross-platform client does not expose.

```csharp
using Agora.Rtc;

var config = new RtcEngineConfig { MContext = context, MAppId = "<APP_ID>" };
var engine = RtcEngine.Create(config);

engine.EnableVideo();
engine.JoinChannel(token: null, channelId: "my-channel", optionalInfo: null, uid: 0);
```

---

## How this repository works

This repository binds nothing but the Video (RTC) SDK today, and it is the *only* thing that binds
it: [`Net.Agora`](https://github.com/sbokatuk/Net.Agora) (the cross-platform façade) and
[`Net.Agora.iOS`](https://github.com/sbokatuk/Net.Agora.iOS) (the iOS binding) are separate
repositories, each with their own release cadence. `Net.Agora.Video.Android`'s version is
`<io.agora.rtc:full-rtc-basic version>.<binding revision>` — see `Directory.Build.props` for why
the Android and iOS lines don't share a version number.

### What is bound, and why not `full-sdk`

`io.agora.rtc:full-sdk` is a POM-only aggregator over roughly twenty optional plugin `.aar`s — AI
noise suppression, face detection, virtual background, screen sharing, and so on. Binding all of
it pulls in features this package exposes no API for, and Android's Java dependency verification
(`XA4241`) refuses to build unless every one of those plugins is present too.
`io.agora.rtc:full-rtc-basic` is Agora's own base artifact: `RtcEngine` and the core surface this
package binds, with exactly one dependency (`io.agora.infra:aosl`).

`Transforms/Metadata.xml` removes a handful of internal implementation classes that `class-parse`
cannot bind cleanly on its own — the default camera/screen capturer, the raw
video-frame/EGL/texture pipeline, the spatial-audio impl class — none of which a consumer of
`RtcEngine` calls directly.

## Building locally

```sh
dotnet build src/Net.Agora.Video.Android/Net.Agora.Video.Android.csproj -f net9.0-android35.0
./build/BuildNugets.sh
dotnet test tests/Net.Agora.Android.PackageTests
```

Nothing is fetched or committed: `AndroidMavenLibrary` resolves the `.aar` straight from Maven
Central for `net9.0-android35.0`/`net10.0-android36.0`, cached under
`~/.cache/dotnet-android/MavenCacheDirectory`. `net8.0-android34.0` uses a different path — that
SDK pack has no `AndroidMavenLibrary` support at all, so `src/Agora.Binding.props` downloads the
same `.aar` directly with an MSBuild `DownloadFile` target instead. See the comments there for why
this matters: the unsupported item is silently ignored rather than erroring, so a build using it
on net8 "succeeds" with an empty few-KB binding assembly and no error anywhere in the log.

No single .NET SDK builds net8, net9 *and* net10 for Android, so `BuildNugets.sh` packs twice (the
installed SDK's band, then a `net10` pass from a scratch `global.json`) and merges the results —
see `build/merge-packages.py`.

## Tests

Everything runs against the packed `.nupkg` in `artifacts/`, not the build output, because the
failure modes worth catching here are packaging ones — most importantly the net8 "empty shell"
trap above, which builds with 0 errors and 0 warnings.

- **`tests/Net.Agora.Android.PackageTests`** (plain xUnit, runs anywhere) asserts the package
  layout — a real binding assembly and the native `.aar`s for every target framework — and, through
  the metadata reader, the API itself: the core `Agora.Rtc` types exist, the
  `IO.Agora.Rtc2 → Agora.Rtc` rename left nothing behind, and `RtcEngine` still exposes the
  channel lifecycle entry points. A binding that failed to generate still packs cleanly; these are
  what notice.
- **`tests/Net.Agora.Android.DeviceTests`** is a bare Android app (no MAUI, no test framework)
  that consumes the packed package and drives the raw binding on an emulator: resolve the Java
  entry points out of the packaged `.aar`, read the native SDK version, create the engine,
  enable/disable video and audio, run the local camera preview, destroy the engine. No Agora
  credentials are involved — the App ID is syntactically valid but unregistered, which is enough
  for everything short of joining a channel. It reports a single `AGORA_E2E_DONE PASS`/`FAIL`
  line to logcat, which `.github/scripts/run-emulator-tests.sh` turns into an exit code — the same
  marker the [`Net.Agora`](https://github.com/sbokatuk/Net.Agora) façade's own device tests use.

In CI (`.github/workflows/build.yml`) the `validate` job runs the package tests and the `e2e` job
runs the emulator suite on `net8.0-android34.0` and `net10.0-android36.0` — the two extremes:
net8's `.aar` arrives through the `DownloadFile` fallback, and net10's assets are grafted in by the
merge step, so those are the two that could each break alone.

Run the emulator suite locally, with an emulator already booted:

```sh
./build/BuildNugets.sh
AGORA_DEVICE_RID=android-arm64 ./.github/scripts/run-emulator-tests.sh 4.6.3.1 net9.0-android35.0
```

## Sample

`samples/Net.Agora.Sample.Android` is a MAUI app built straight against this package — no
cross-platform façade — that creates `Agora.Rtc.RtcEngine` and shows the local camera preview: an
App ID entry, camera/microphone permission handling, and a `SurfaceView` behind a small MAUI
handler (see its `AgoraVideoView.cs` for why a custom handler rather than a wrapped view). The
full join/publish/subscribe flow, wrapped behind one cross-platform API, is
[`Net.Agora`](https://github.com/sbokatuk/Net.Agora)'s sample.

It consumes the packed `Net.Agora.Video.Android` package from `./artifacts` (see `NuGet.config`),
so pack first. It targets `net10.0-android36.0` and needs the **.NET 10 SDK** with the
`maui-android` workload, which this repository's `global.json` does not select — hence the scratch
directory:

```sh
./build/BuildNugets.sh
cd /tmp && dotnet new globaljson --sdk-version 10.0.100 --force
dotnet build <repo>/samples/Net.Agora.Sample.Android/Net.Agora.Sample.Android.csproj
```

`Net.Agora.Android.sln` deliberately contains the binding project and the tests but **not** the
sample, so `dotnet build Net.Agora.Android.sln` does not require the MAUI workload.

## Licence

MIT — see [LICENSE](LICENSE). Agora's own SDK is distributed under Agora's SDK licence terms.

[agora]: https://www.agora.io/en/
