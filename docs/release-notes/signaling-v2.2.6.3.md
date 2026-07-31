## What's changed

`Net.Agora.Signaling.Android` advances to **2.2.6.3**. The native SDK is unchanged at
`io.agora:agora-rtm` 2.2.6.

### Signaling no longer breaks Video and Voice in the same app

An app that referenced this package **and** `Net.Agora.Video.Android` or
`Net.Agora.Voice.Android` could get an `RtcEngine.Create()` that returned `null`, with no
exception, no logged reason and a build that had gone through perfectly clean.

`io.agora:agora-rtm`'s own `.pom` declares no dependencies at all. Rather than depending on
`io.agora.infra:aosl` the way the RTC artifacts do, it vendors its own older copy of `libaosl.so`
inside its `.aar`'s `jni/` directory — and that copy is missing `aosl_ref_magic` and the rest of
the symbols the RTC engine's `libagora-rtc-sdk.so` resolves at `dlopen` time. Two `.aar`s carrying
the same `jni/<abi>/libaosl.so`, and the Android build silently keeps one of them. When it kept
agora-rtm's, the engine failed to load and `Create()` answered `null`.

Nothing in a build reported this. The two products are different Java packages
(`io.agora.rtm` vs `io.agora.rtc2`) and different native libraries, so they compiled, dexed,
packaged and installed together exactly as well broken as fixed — the only outward sign was a
quiet `XA4301` about a duplicate assembly entry.

This package now takes the `io.agora.infra:aosl` dependency agora-rtm should have declared, at the
same version the RTC artifacts resolve, and strips agora-rtm's vendored copy out of the `.aar`
before packing (`AgoraStripNativeLibraryEntries` in `src/Agora.Binding.props`). Exactly one aosl
ships, and it is the one both products work against.

**If you use Signaling together with Video or Voice, upgrade.** Signaling on its own was never
affected — agora-rtm's own copy is the one it was built against.

The Apple bindings carry the same conflict through their own mechanism (an `aosl.xcframework`
that lands at one path in the app bundle, so one copy wins) and are fixed in the same round:
[`Net.Agora.iOS`](https://github.com/sbokatuk/Net.Agora.iOS) `signaling-v2.2.6.3` and
[`Net.Agora.Mac`](https://github.com/sbokatuk/Net.Agora.Mac) `signaling-v2.2.8.3`.

### The conflict cannot come back quietly

Three guards, because the failure mode was silent in every direction:

- `AgoraAoslVersion` is pinned in `Directory.Build.props` separately from the RTC versions, since
  nothing ties them together — it merely happens to match today.
- Two package tests: that the packed `agora-rtm` `.aar` carries no `libaosl.so` of its own, and
  that the `aosl-*.aar` this package ships is byte-for-byte the one `Net.Agora.Video.Android`
  ships. A pin that drifts fails the build rather than shipping.
- A new emulator leg builds the device test app with **Video and Signaling both referenced** and
  runs the ordinary Video suite on it. That is the first check in this repository that the two
  coexist at *runtime* rather than at build time — which is the distinction this bug turned on.

The `sample` job's Video leg used to reference the Signaling package as a "coexistence proof".
It proved nothing, and it is gone; the emulator leg above replaces it.

### Samples

`samples/Net.Agora.Sample.Chat.Android`, `samples/Net.Agora.Sample.Fastboard.Android` and
`samples/Net.Agora.Sample.Whiteboard.Android` are new, so every product bound here now has a
sample in this repository. `samples/Net.Agora.Sample.Signaling.Android` gained a real RTM token
field, so it can be pointed at a project with App Certificate enabled rather than only at the
App-ID-as-token fallback.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.Android` | 2.2.6.3 | `io.agora:agora-rtm` 2.2.6, `io.agora.infra:aosl` 1.3.5 |

Target frameworks: `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0`.
