## What's changed

**Chat debuts**: `Net.Agora.Chat.Android` **1.4.0.1** — the full `class-parse` surface of
`io.agora.rtc:chat-sdk 1.4.0`, with `io.agora.chat` renamed to `Agora.Chat` the way the RTC and
Signaling bindings rename theirs. It coexists with any other package here except IoT: different
Java packages, different native libraries.

Its listener interfaces needed more metadata than any binding here so far. `GroupChangeListener`
and `ChatRoomChangeListener` declare twelve identically-named callbacks — a chat room and a group
support the same moderation operations — and four callbacks are overloaded within one interface,
so the generator's event sugar collides both across and within them. Suppressing the events does
not work (the Implementor still references the handler fields it no longer declares), so the
deprecated overloads are renamed instead and the chat-room `EventArgs` types are given their own
names. The full callback surface stays bound and implementable.

The package tests grew a Chat row, and the namespace-rename check now compares on a namespace
boundary — without the trailing dot it flagged Chat's `IO.Agora.ChatRoomChangeListener`, a type in
the bare `io.agora` package that the rename never covered.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Chat.Android` | 1.4.0.1 |
