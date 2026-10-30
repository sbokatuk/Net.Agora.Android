## What's changed

`Net.Agora.Whiteboard.Android` advances to **2.16.134.1**, on `com.github.netless-io:whiteboard-android`
**2.16.134** (netless released it on 2026-10-21). `DSBridge-Android` stays at `nl_dsb_v3.0.4`. The binding
revision resets to 1 with the new native line.

### What netless changed

From netless's own changelog, 2.16.124 to 2.16.134:

- **Session lifecycle (2.16.134).** Room and player session replacement is serialised, and stale
  lifecycle callbacks are rejected.
- **Bridge.** Updated to `white-web-sdk` 2.16.59, `window-manager` 1.0.25, `appliance-plugin` 1.1.45 and
  `app-slide` 0.2.108.
- **Dynamic configuration (2.16.132).** Parameters can come from a per-region configuration file, chosen
  by the room's region. A missing or failed download keeps the local parameters.
- **Windows and slides.** `FocusApp`, `FitOriginSizeAndCamera`, a typed `WindowAppParam` origin size, slide
  navigation button modes and unified page state.
- **Background images.** Recovery options, plus `HasBackgroundImage` and `ReloadBackgroundImage`.
- **Compatibility.** Appliance-plugin Worker rendering is fixed on Android System WebView 89 and older.

### .NET API changes

159 public members are added, and nothing is removed. Two behaviours differ:

- **`Room.DispatchDocsEvent` resolves to a `DispatchDocsEventResult`, not a boolean.** The C# signature
  doesn't change, because the binding's promises aren't generic, but the object your `IPromise.Then`
  receives does. Update any code that casts it to a `Java.Lang.Boolean`.
- **Joining through a destroyed view now fails straight away.** A `WhiteSdk` built on a
  `WhiteboardView` that has already been destroyed reports `HostInvalid`, where it used to hang.

The new `CommonCallback` methods have default implementations, so existing implementers still compile.

The `.pom`'s dependencies are the same as 2.16.123's, so the `PackageReference`s stay where they were.
The `.aar` still ships no `proguard.txt`, so the keep rules this package carries are still needed.

### Supply chain

`build/checksums.txt` records the new SHA-256 for `whiteboard-android` 2.16.134. It was checked against
the tag: the `.aar`'s hash-named assets match `sdk/src/main/assets/whiteboard/` at 2.16.134, and
`WhiteSdk` reports that version.

### Fastboard

`Net.Agora.Fastboard.Android` 1.8.1.2 keeps working on this version: its dependency on this package has a
minimum and no maximum, so it picks this release up. Joining a room with its toolbar was tested on this
pairing. Fastboard 1.8.2 needs this release first, because it requires `whiteboard-android` 2.16.131 or
newer.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Whiteboard.Android` | 2.16.134.1 | `com.github.netless-io:whiteboard-android` 2.16.134 + `DSBridge-Android` `nl_dsb_v3.0.4` |

Target frameworks: `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0`.

## Tested

Before release, on Android emulators: device suites on net8 and net10 and with R8 shrinking. The sample
joined a live room outside China, and strokes drawn on Android appeared on an iOS client in that room.
