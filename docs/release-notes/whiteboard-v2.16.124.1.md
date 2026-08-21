## What's changed

`Net.Agora.Whiteboard.Android` advances to **2.16.124.1**, on `com.github.netless-io:whiteboard-android`
**2.16.124** (netless released it on 2026-08-14). `DSBridge-Android` stays at `nl_dsb_v3.0.4`; the
binding revision resets to 1 with the new native line, as the version scheme says it should.

### What netless changed

A bug-fix release, entirely inside the WebView side of the SDK. From netless's own changelog:

- `Whiteboard-bridge` is updated to `aa1dfd7`, which builds the appliance-plugin Worker render
  mode and a Canvas Context compatibility blacklist into the bridge itself.
- Fixes appliance-plugin Worker rendering on **Android System WebView 89 and older**. An app that
  had configured the blacklist by hand to work around it no longer needs to.

The one change to the Java half is internal: `WhiteSdk` no longer reads the system volume through
`AudioManager` from the main thread while it initialises (a synchronous Binder call that a stalled
audio service could block on), and guards against a `null` `AudioManager` and a zero maximum
volume. The helper that decides this is package-private, so the generated C# surface is
**unchanged** — nothing for `Metadata.xml` to rename, and no method appears or disappears.

The `.aar`'s transitive dependencies are byte-for-byte the same `.pom` as 2.16.123's apart from the
version line, so the `PackageReference`s for OkHttp, Gson, WebKit and Annotation stay where they
were, and the bundled JavaScript under `assets/whiteboard/` is simply the newer build (no
`proguard.txt` appears either, so the keep rules this package carries are still needed).

### Supply chain

`build/checksums.txt` records the new SHA-256 for `whiteboard-android` 2.16.124, taken from the
artifact JitPack serves for that tag. It has been checked against the sources of the tag: the
`classes.jar` carries the `WhiteSdk` change above and nothing else, and the asset bundle's
hash-named files match the tag's `sdk/src/main/assets/whiteboard/` listing.

### Fastboard

`Net.Agora.Fastboard.Android` is not released in this round. Its unbracketed dependency on this
package resolves to whichever whiteboard binding is newest at restore time, and
`fastboard-android` 1.8.1's own `.pom` asks for `whiteboard-android` 2.16.115 or newer, so 2.16.124
is within what it was built against.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Whiteboard.Android` | 2.16.124.1 | `com.github.netless-io:whiteboard-android` 2.16.124 + `DSBridge-Android` `nl_dsb_v3.0.4` |

Target frameworks: `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0`.
