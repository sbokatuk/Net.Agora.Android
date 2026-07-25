## What's changed

**The Interactive Whiteboard debuts**: `Net.Agora.Whiteboard.Android` **2.16.123.1**. It is
netless's rather than Agora's own, and comes from **JitPack** rather than Maven Central — JitPack
builds a GitHub tag on demand, so there is no coordinate to point at. `@(AgoraMavenArtifact)`'s
`Repository` metadatum now takes `AndroidMavenLibrary`'s three forms (`Central`, `Google`, or a
URL), and the net8 fallback target understands the same three, with an error rather than a silent
fall-through for anything else.

It is also the first product here with real third-party dependencies rather than one Agora infra
artifact. Microsoft publishes bindings for all but one, so those come in as `PackageReference`s:
embedding the raw `.aar`s would put a second copy of okio, kotlin-stdlib and androidx.core into any
app that already has them, and duplicate classes fail the dex merge. The pins are the newest of
each line that still carries a **net8** asset.

`DSBridge-Android` is the exception, and it is *bound* rather than carried: `WhiteboardView` — the
board itself, the one type an app cannot avoid — extends `wendu.dsbridge.special.DWebView`, so
leaving it unbound makes class-parse drop the view and every method that takes one **without
failing the build**. The package still packs, and `WhiteSdk` arrives with no constructor.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.Whiteboard.Android` | 2.16.123.1 |
