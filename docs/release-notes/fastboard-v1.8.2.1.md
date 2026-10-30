## What's changed

`Net.Agora.Fastboard.Android` advances to **1.8.2.1**, on `com.github.netless-io:fastboard-android`
**1.8.2** (netless released it on 2026-09-28). The binding revision resets to 1 with the new native line.

It needs `Net.Agora.Whiteboard.Android` 2.16.134.1. Fastboard 1.8.2 is built against
`whiteboard-android` 2.16.131 and calls API that doesn't exist below it.

### Breaking: room operations must run on the main thread

Fastboard 1.8.2 throws `Java.Lang.IllegalStateException` ("FastRoom must be used on the main thread")
from `Join`, `Disconnect`, `Destroy`, `RequiresRecreation`, `CanRetryJoin`, `RegisterApp` and the new
listener setters when they're called from any other thread. Calls from a page's event handlers are
already on the main thread. Anything else needs `MainThread.BeginInvokeOnMainThread`.

### What netless changed

From netless's own changelog:

- **New.** Presentation static courseware, app focus, structured document commands and unified
  page-state APIs. `InsertStaticDoc` still creates a DocsViewer.
- **Lifecycle fixes.** Join cancellation, late asynchronous results, replay lifecycle, callback
  reentrancy and retry after an SDK failure. `RequiresRecreation` and `CanRetryJoin` say what a failed
  room can still do.
- **Callbacks.** Mutable room configuration is preserved, and SDK and plugin callbacks are forwarded
  without replacing internal error handling.

`Fastboard.Version` still returns `1.8.1` on this release. That is netless's constant, which wasn't
updated in 1.8.2, and not a sign of the wrong payload.

### .NET API changes

43 public members are added, and nothing is removed.

### `Destroy` releases the view

This isn't new in 1.8.2, but it's easy to hit. `FastRoom.Destroy` also destroys the WebView inside the
`FastboardView`, so a later room can't use that view. To leave a room and join again on the same view,
call `Disconnect` alone. The sample used to call both, so its second Join never completed; it now
disconnects only.

### Supply chain

`build/checksums.txt` records the new SHA-256 for `fastboard-android` 1.8.2. The `.aar`'s other
dependencies and its `proguard.txt` are the same as 1.8.1's, so the `PackageReference`s and the keep rules
this package carries stay where they were.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Fastboard.Android` | 1.8.2.1 | `com.github.netless-io:fastboard-android` 1.8.2 |

Target frameworks: `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0`.

The `Xamarin.AndroidX.AppCompat` pin stays on the 1.7.0.x line: 1.7.1 pulls a Lifecycle newer than
.NET MAUI 9 pins and NU1107s any MAUI consumer.

## Tested

Before release, on Android emulators: device suites on net8 and net10 and with R8 shrinking. The sample
joined a live room outside China with its toolbar working, drew strokes that appeared on an iOS client,
switched to read-only and back, and left and rejoined.
