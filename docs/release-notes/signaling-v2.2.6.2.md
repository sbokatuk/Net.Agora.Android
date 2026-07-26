## What's changed

`Net.Agora.Signaling.Android` advances to **2.2.6.2**. The native SDK is unchanged at
`io.agora:agora-rtm` 2.2.6.

### Awaitable operations

Every RTM operation took a Java `IResultCallback`, which meant a nested callback per step. The
binding now ships `Task`-returning extension methods over `RtmClient`:

```csharp
await client.LoginAsync(token);
await client.SubscribeAsync(channel);
await client.PublishAsync(channel, "hello");
```

A failure throws `RtmOperationException`, which carries the SDK's `ErrorInfo` and puts its reason,
operation and code in the message. Faulting is right on this platform: Android's `onFailure` means
failure, unlike the Apple SDK where a non-nil error object can accompany a success with code 0.

The callback-taking methods are untouched — these are additions.

`samples/Net.Agora.Sample.Signaling.Android` dropped both its `IResultCallback` and its
`IRtmEventListener` classes: the operations are awaited and the three listened callbacks are already
generated as C# events on `RtmClient`.

### R8 keep rules travel with the package

`agora-rtm` ships no `proguard.txt`, so the package now carries keep rules under `proguard/` plus a
`buildTransitive` targets file that feeds them to a consuming app's R8 run. The .NET Android build
already auto-keeps the bound surface, so this covers what that cannot see: classes the binding does
not bind but the SDK reaches reflectively.

A device-test flavour and one CI leg now run **with R8 on**, so the shrunk configuration a store
build ships with is exercised at all — previously every leg pinned shrinking off. The suite verifies
publish-before-login still answers −10025 through the new async path.

### Supply chain: artifacts are pinned by digest

See the notes for v4.6.3.5 — `build/checksums.txt` covers this artifact too.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.Android` | 2.2.6.2 | `io.agora:agora-rtm` 2.2.6 |

Target frameworks: `net8.0-android34.0`, `net9.0-android35.0`, `net10.0-android36.0`.
