## What's changed

`Net.Agora.Fastboard.Android` advances to **1.8.1.2**. The native SDK is unchanged at
`com.github.netless-io:fastboard-android` 1.8.1.

### R8 keep rules travel with the package

`fastboard-android`'s own `proguard.txt` keeps only `io.agora.board.fast.{,extension,model,ui}` —
enough for a Java consumer, but the binding reaches other types over JNI where R8 sees no reference.
The package now carries a keep rule for the whole namespace under `proguard/` plus a
`buildTransitive` targets file that feeds it to a consuming app's R8 run. The `com.herewhite` and
`wendu.dsbridge` rules travel with `Net.Agora.Whiteboard.Android`, which this package depends on.

### Supply chain: artifacts are pinned by digest

JitPack builds this artifact on demand from a git tag and netless has re-tagged before, so the bytes
are now pinned in `build/checksums.txt` and checked by `build/verify-artifacts.sh`.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Fastboard.Android` | 1.8.1.2 | `com.github.netless-io:fastboard-android` 1.8.1 |

Target frameworks: `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0`.

The `Xamarin.AndroidX.AppCompat` pin stays on the 1.7.0.x line: 1.7.1 pulls a Lifecycle newer than
.NET MAUI 9 pins and NU1107s any MAUI consumer.
