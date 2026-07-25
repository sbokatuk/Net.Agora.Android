## What's changed

**IoT debuts**: `Net.Agora.IoT.Android` **1.3.0.1** — `io.agora:iotsdk 1.3.0`, renamed to
`Agora.IoT`. Three things to know before adopting it, all of them Agora's packaging rather than
this binding's:

- **Android only.** There is no iOS IoT SDK, so there is no `Net.Agora.IoT.iOS` and no
  cross-platform client in `sbokatuk/Net.Agora`.
- **Exclusive with every other package here.** The 80 MB `.aar` bundles its own copies of
  `agora-rtc-sdk.jar` and `agora-rtm-sdk.jar` and their `.so` files, so a second Agora package
  fails the dex merge on duplicate `io.agora.rtc2.*` classes. Only `io.agora.iotlink` is bound —
  binding the embedded (older) RTC copy produced 253 generator errors and would have meant
  maintaining a second, worse copy of the Video binding's metadata.
- **Its device-messaging half needs the AWS Android SDK**, which `io.agora:iotsdk` does not
  declare as a dependency and which has no official .NET binding.

Maven Central's newest is 1.3.0 while Agora's documentation describes 1.8.0, which is published
only as a direct download; this package binds what is actually resolvable.

`samples/Net.Agora.Sample.IoT.Android` initialises the SDK, reports its state machine and releases
it — an app of its own, since it can share one with nothing else here.

## Packages

| Package | Version |
| --- | --- |
| `Net.Agora.IoT.Android` | 1.3.0.1 |
