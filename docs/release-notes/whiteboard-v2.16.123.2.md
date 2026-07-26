## What's changed

`Net.Agora.Whiteboard.Android` advances to **2.16.123.2**. The native SDKs are unchanged at
`com.github.netless-io:whiteboard-android` 2.16.123 and `DSBridge-Android` `nl_dsb_v3.0.4`.

### R8 keep rules travel with the package

`whiteboard-android` ships no `proguard.txt`, so the package now carries keep rules under
`proguard/` plus a `buildTransitive` targets file that feeds them to a consuming app's R8 run. They
cover `com.herewhite.**` and `wendu.dsbridge.**`, and keep `@JavascriptInterface` members by name —
the whiteboard is a WebView SDK whose bridge is invoked reflectively from JavaScript, which R8
cannot see. The failure mode this prevents is a blank board with no error in a shrunk release build.

### Supply chain: artifacts are pinned by digest

This package needed it most. Both artifacts come from JitPack, which builds them on demand from a
git tag, and netless has re-tagged before — `DSBridge-Android`'s pinned "version" is literally a tag
name, and it is bound (`Bind=true`), so a moved tag would change the generated public API.
`build/checksums.txt` now records a SHA-256 for both and `build/verify-artifacts.sh` checks them.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Whiteboard.Android` | 2.16.123.2 | `com.github.netless-io:whiteboard-android` 2.16.123 + `DSBridge-Android` `nl_dsb_v3.0.4` |

Target frameworks: `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0`.
