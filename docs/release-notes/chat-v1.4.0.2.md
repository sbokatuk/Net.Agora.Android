## What's changed

`Net.Agora.Chat.Android` advances to **1.4.0.2**. The native SDK is unchanged at
`io.agora.rtc:chat-sdk` 1.4.0; this is a packaging and supply-chain release.

### R8 keep rules travel with the package

`chat-sdk` ships no `proguard.txt` of its own (its `res/raw/keep.xml` is an aapt resource keep, not
a code rule), so the package now carries keep rules under `proguard/` plus a `buildTransitive`
targets file that feeds them to a consuming app's R8 run. The .NET Android build already auto-keeps
the bound surface, so this covers what that cannot see: classes the binding does not bind but the
SDK reaches reflectively.

Previously nothing in this repository exercised a shrunk build at all — the device suite pinned
shrinking off in every configuration. One CI leg now runs with R8 on.

### Supply chain: artifacts are pinned by digest

`build/checksums.txt` records a SHA-256 for every native coordinate, `chat-sdk` included, and
`build/verify-artifacts.sh` checks them against what Maven Central serves. A coordinate is a
mutable pointer and `AndroidMavenLibrary` performs no content check of its own.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Chat.Android` | 1.4.0.2 | `io.agora.rtc:chat-sdk` 1.4.0 |

Target frameworks: `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0`.
