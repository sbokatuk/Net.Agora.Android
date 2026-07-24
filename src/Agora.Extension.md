# The extension packages

`Net.Agora.Extensions.*.Android` are not bindings. They carry native payload and nothing else.

## Why they exist at all

Agora's RTC SDK ships its optional features — AI noise suppression, virtual background, spatial
audio, the software encoders and so on — as separate Maven artifacts rather than inside
`full-rtc-basic`. The switch that turns each one on is already on `RtcEngine`, in the core SDK and
therefore already in `Net.Agora.Video.Android` / `Net.Agora.Voice.Android`. What is missing without
these packages is the `.so` the engine tries to `dlopen` when the switch is flipped, and the
failure mode is a runtime error code from a call that compiled and linked perfectly.

So each extension is one package, and an app pays only for the ones it turns on. That is the whole
point of Agora shipping them separately: `spatial-audio` alone is 12 MB across four ABIs.

## What the packages look like

Every one of them is a `.aar` whose `classes.jar` is a 22-byte empty archive and whose content is
`jni/<abi>/*.so`. There is no Java API to generate C# from, so the projects set
`AgoraBindArtifact=false` and the packed assembly is a ~5 KB stub next to the `.aar` — which is why
`tests/Net.Agora.Android.PackageTests` holds them to a different set of expectations than the four
real bindings, rather than the assembly-size and public-type floors that exist to catch an
[empty binding shell](../src/Agora.Binding.props).

They deliberately depend on **neither** RTC package. The Video and Voice bindings are mutually
exclusive in one app, so depending on either would force a flavour on the consumer; and the audio
extensions work with both. The README tells consumers to add one alongside whichever RTC package
they already have.

## What is not here

**Screen capture** has no extension package. `io.agora.rtc:full-screen-sharing` exists, but unlike
every artifact above it is Java (a 107 KB `classes.jar`, no `.so`) implementing the *separate
process* screen-sharing flow, and its classes extend `io.agora.rtc2` types — so binding it would
require one of the two RTC packages to be present, which is exactly the coupling these packages
avoid. It is also not needed for ordinary screen sharing: `RtcEngine.StartScreenCapture` is in the
core SDK and works with `Net.Agora.Video.Android` alone.

**The low-latency variants** (`ains-ll`, `aiaec-ll`) and `full-super-resolution`, `pvc` and the
decoder packs have no 4.6.3 release on Maven — the newest is 4.5.2. Adding them would put a second
native version line inside one product; revisit at the next RTC bump.

**LipSync** ships an iOS xcframework but has no Android artifact under any spelling, so there is no
pair to publish.
