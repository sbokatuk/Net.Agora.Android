# Net.Agora.Android

[![NuGet](https://img.shields.io/nuget/v/Net.Agora.Video.Android?label=nuget)](https://www.nuget.org/packages/Net.Agora.Video.Android)
[![Targets: net8.0 | net9.0 | net10.0](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0%20%7C%20net10.0-512BD4)](#packages)
[![full-rtc-basic 4.6.3](https://img.shields.io/badge/full--rtc--basic-4.6.3-099DFD)](https://central.sonatype.com/artifact/io.agora.rtc/full-rtc-basic)
[![Licence: MIT](https://img.shields.io/badge/licence-MIT-green)](LICENSE)

.NET for Android and .NET MAUI bindings for Agora's native Android SDKs.

Six products are bound, from `net8.0-android` through `net10.0-android`:

| Package | Native artifact | Use it when |
| --- | --- | --- |
| `Net.Agora.Video.Android` | [`io.agora.rtc:full-rtc-basic`](https://central.sonatype.com/artifact/io.agora.rtc/full-rtc-basic) | The app shows or sends video (also carries the full audio surface). |
| `Net.Agora.Voice.Android` | [`io.agora.rtc:voice-rtc-basic`](https://central.sonatype.com/artifact/io.agora.rtc/voice-rtc-basic) | Audio only — the same engine built without the video pipeline, a ~20 MB smaller `.aar`. |
| `Net.Agora.Signaling.Android` | [`io.agora:agora-rtm`](https://central.sonatype.com/artifact/io.agora/agora-rtm) | Realtime messaging (Signaling / RTM 2.x, its own 2.2.x version line) — coexists with either RTC package. |
| `Net.Agora.Chat.Android` | [`io.agora.rtc:chat-sdk`](https://central.sonatype.com/artifact/io.agora.rtc/chat-sdk) | Persistent messaging (Chat / IM 1.x, its own version line) — coexists with everything else here. |
| `Net.Agora.IoT.Android` | [`io.agora:iotsdk`](https://central.sonatype.com/artifact/io.agora/iotsdk) | Agora IoT devices (1.3.x). **Android only** — there is no iOS SDK, so no cross-platform client — and **exclusive with every package above**, whose native artifacts it bundles its own copies of. |

Alongside them, twelve `Net.Agora.Extensions.<Name>.Android` packages carry the RTC SDK's optional
features — AI noise suppression, virtual background, spatial audio, the video enhancement filters,
the software encoders and the rest. They are native payload only: the switch that turns each one on
already exists on `Agora.Rtc.RtcEngine`, and what these packages add is the `.so` the engine loads
when it is flipped. Add one alongside either RTC package — they depend on neither, so they do not
force a flavour. See [src/Agora.Extension.md](src/Agora.Extension.md) for the full list and for
what is deliberately absent.

```bash
dotnet add package Net.Agora.Video.Android   # or Net.Agora.Voice.Android
```

Pick **one of the RTC pair**: both `.aar`s carry the same Java classes (`io.agora.rtc2.*`), so referencing both
fails the build at dex merge — mirroring Agora's own artifacts, where an app depends on the full
or the voice SDK, never both. For the same reason both bindings expose the same `Agora.Rtc`
namespace (renamed from `io.agora.rtc2` to match Agora's own C# / Unity SDK naming), so an app can
switch packages without touching code.

These are raw platform bindings — the full `class-parse`-generated surface, `Agora.Rtc.RtcEngine`
and friends. Most apps want the cross-platform clients instead:
[`Net.Agora.Video` / `Net.Agora.Voice`](https://github.com/sbokatuk/Net.Agora), which wrap these
packages and their iOS siblings behind one API. Reach for a binding directly only when you need
something the cross-platform client does not expose.

```csharp
using Agora.Rtc;

var config = new RtcEngineConfig { MContext = context, MAppId = "<APP_ID>" };
var engine = RtcEngine.Create(config);

engine.EnableVideo();
engine.JoinChannel(token: null, channelId: "my-channel", optionalInfo: null, uid: 0);
```

---

## How this repository works

This repository is the *only* thing that binds Agora's Android SDKs:
[`Net.Agora`](https://github.com/sbokatuk/Net.Agora) (the cross-platform façade) and
[`Net.Agora.iOS`](https://github.com/sbokatuk/Net.Agora.iOS) (the iOS binding) are separate
repositories, each with their own release cadence. Each package's version is
`<native artifact version>.<binding revision>` — see `Directory.Build.props` for why the Android
and iOS lines don't share a version number. The package set lives in `build/packages.tsv`; adding
a package means adding a row there and a project under `src/`.

### What is bound, and why not `full-sdk` / `voice-sdk`

`io.agora.rtc:full-sdk` (and its voice counterpart `voice-sdk`) is a POM-only aggregator over
optional plugin `.aar`s — AI noise suppression, face detection, virtual background, screen
sharing, and so on. Binding all of it pulls in features these packages expose no API for, and
Android's Java dependency verification (`XA4241`) refuses to build unless every one of those
plugins is present too. `io.agora.rtc:full-rtc-basic` / `voice-rtc-basic` are Agora's own base
artifacts: `RtcEngine` and the core surface these packages bind, with exactly one dependency
(`io.agora.infra:aosl`).

Each project's `Transforms/Metadata.xml` removes the few internal implementation classes that
`class-parse` cannot bind cleanly on its own — for Video the default camera/screen capturer, for
both the raw video-frame/EGL/texture pipeline and the spatial-audio impl class — none of which a
consumer of `RtcEngine` calls directly. (The voice `.aar` ships the same shared Java API layer,
video types included; only the native pipeline differs.)

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
runs the emulator suite per package (Video and Voice — one leg each, since a single app can hold
only one of them) on `net8.0-android34.0` and `net10.0-android36.0` — the two extremes: net8's
`.aar` arrives through the `DownloadFile` fallback, and net10's assets are grafted in by the merge
step, so those are the two that could each break alone. The Voice legs compile the video checks
out (see the `AGORA_VOICE` constant in the device tests).

Run the emulator suite locally, with an emulator already booted:

```sh
./build/BuildNugets.sh
AGORA_DEVICE_RID=android-arm64 ./.github/scripts/run-emulator-tests.sh 4.6.3.1 net9.0-android35.0
AGORA_DEVICE_RID=android-arm64 ./.github/scripts/run-emulator-tests.sh 4.6.3.1 net9.0-android35.0 Voice
```

## Sample

`samples/Net.Agora.Sample.Android` is a MAUI app built straight against the packages — no
cross-platform façade — that creates `Agora.Rtc.RtcEngine` and shows the local camera preview
with a front/back flip: an App ID entry, camera/microphone permission handling, and a
`SurfaceView` behind a small MAUI handler (see its `AgoraVideoView.cs` for why a custom handler
rather than a wrapped view). Its **Try Signaling** button drives `Agora.Rtm.RtmClient` from the
same app — the coexistence of the two products, proven at dex-merge time just by building.

`samples/Net.Agora.Sample.Voice.Android` is its audio-only sibling against
`Net.Agora.Voice.Android`: capture, mute, speakerphone routing and the who-is-speaking volume
reports, with no camera permission anywhere.

The full join/publish/subscribe flows, wrapped behind one cross-platform API, are
[`Net.Agora`](https://github.com/sbokatuk/Net.Agora)'s samples.

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
