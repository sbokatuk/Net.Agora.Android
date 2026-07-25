## What's changed

**Fastboard debuts**: `Net.Agora.Fastboard.Android` **1.8.1.1** — netless's ready-made UI over the
Interactive Whiteboard, from the same JitPack source. It depends on `Net.Agora.Whiteboard.Android`,
whose types its own API takes and returns, so release it after the whiteboard track.

Its AppCompat is held to the 1.7.0 line, not the newest: 1.7.1 pulls a Lifecycle newer than .NET
MAUI 9 pins, which fails a MAUI app's restore outright with NU1107.

Fastboard's replay surface is not bound, on either platform. Removing it also resolves a generator
collision — `FastReplayListener` and `FastRoomListener` both declare `onFastError` — but the reason
it stays gone is that replay is a separate feature from drawing on a live board and no façade
reaches it.

## Packages

| Package | Version | Depends on |
| --- | --- | --- |
| `Net.Agora.Fastboard.Android` | 1.8.1.1 | `Net.Agora.Whiteboard.Android` 2.16.123.1 |
