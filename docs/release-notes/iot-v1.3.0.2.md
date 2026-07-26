## What's changed

`Net.Agora.IoT.Android` advances to **1.3.0.2**. The native SDK is unchanged at `io.agora:iotsdk`
1.3.0; this is a packaging and supply-chain release.

### R8 keep rules travel with the package

`iotsdk`'s own `proguard.txt` is empty, and this package is the strongest case for keep rules in the
repository: the binding deliberately unbinds the private RTC and RTM copies the .aar bundles
(`Transforms/Metadata.xml` removes 23 packages), so the auto-generated bound-surface keeps do not
cover them — yet `io.agora.iotlink` drives them at runtime, partly reflectively. The package now
carries keep rules under `proguard/` plus a `buildTransitive` targets file that feeds them to a
consuming app's R8 run.

### Supply chain: artifacts are pinned by digest

`build/checksums.txt` records a SHA-256 for every native coordinate, `iotsdk` included, and
`build/verify-artifacts.sh` checks them against what Maven Central serves.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.IoT.Android` | 1.3.0.2 | `io.agora:iotsdk` 1.3.0 |

Target frameworks: `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0`.

This package is exclusive with every other Agora package: the .aar bundles whole private copies of
the RTC and RTM SDKs, so referencing it alongside another fails at dex merge.
