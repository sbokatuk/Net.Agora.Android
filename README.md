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

## Sample

`samples/Net.Agora.Sample.Android` is a plain .NET for Android app — no MAUI — that joins a
channel and publishes/renders video directly against `Agora.Rtc.RtcEngine`: an App ID/channel/token
entry, Join/Leave buttons, and a local + remote `SurfaceView`. It's the same flow as
[`Net.Agora`](https://github.com/sbokatuk/Net.Agora)'s cross-platform MAUI sample, but built
straight against this package with no façade in between — proof `Net.Agora.Video.Android` is
consumable end to end, and a reference for wiring `RtcEngine` up by hand.

It consumes the packed `Net.Agora.Video.Android` package from `./artifacts` (see `NuGet.config`),
so pack first:

```sh
./build/BuildNugets.sh
dotnet build samples/Net.Agora.Sample.Android -f net9.0-android35.0
```

## Licence

MIT — see [LICENSE](LICENSE). Agora's own SDK is distributed under Agora's SDK licence terms.

[agora]: https://www.agora.io/en/
