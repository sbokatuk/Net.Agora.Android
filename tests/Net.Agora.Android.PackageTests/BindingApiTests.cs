namespace Net.Agora.Android.PackageTests;

/// <summary>
/// Asserts that the binding assembly inside the package actually exposes the Agora RTC API.
/// A binding that fails to generate still compiles and packs cleanly — it just produces an
/// almost-empty assembly — so the package layout (and its file-size heuristics in
/// <see cref="PackageLayoutTests"/>) is not enough to prove the build worked.
/// </summary>
/// <remarks>
/// The assembly is read with the metadata reader (<see cref="AssemblyApi"/>) rather than loaded:
/// it targets *-android and references Mono.Android, so it cannot be loaded into this desktop
/// test process.
/// </remarks>
public class BindingApiTests
{
    /// <summary>
    /// The types a consumer starts with, all under the renamed namespace. Video lives in
    /// Agora.Rtc.Video (from the Java package io.agora.rtc2.video); the rename maps the whole
    /// IO.Agora.Rtc2 prefix, sub-namespaces included.
    /// </summary>
    private static readonly string[] CoreTypes =
    [
        "Agora.Rtc.RtcEngine",
        "Agora.Rtc.RtcEngineConfig",
        "Agora.Rtc.IRtcEngineEventHandler",
        "Agora.Rtc.ChannelMediaOptions",
        "Agora.Rtc.Video.VideoCanvas",
        "Agora.Rtc.Video.VideoEncoderConfiguration",
    ];

    private static AssemblyApi OpenBinding(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Video);
        var assembly = Packages.ReadEntry(package, $"lib/{tfm}/{Packages.Video}.dll");
        return new AssemblyApi(assembly);
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Binding_exposes_the_core_rtc_types(string tfm)
    {
        using var api = OpenBinding(tfm);

        var missing = CoreTypes.Except(api.PublicTypes).ToList();

        Assert.True(
            missing.Count == 0,
            $"{Packages.Video} ({tfm}) is missing bound types: {string.Join(", ", missing)}. " +
            $"The assembly exposes {api.PublicTypes.Count} public types in total.");
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Binding_is_not_an_empty_shell(string tfm)
    {
        using var api = OpenBinding(tfm);

        // Guards the real failure mode described in src/Agora.Binding.props: @(AndroidMavenLibrary)
        // silently ignored produces a valid but essentially empty assembly, which still packs and
        // installs fine. A real binding of full-rtc-basic exposes ~300 public top-level types; the
        // margin below that is headroom for Agora trimming their surface, not for a broken build.
        Assert.True(
            api.PublicTypes.Count >= 200,
            $"{Packages.Video} ({tfm}) exposes only {api.PublicTypes.Count} public types; " +
            "the binding generator likely did not run.");
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Namespace_rename_left_nothing_behind(string tfm)
    {
        using var api = OpenBinding(tfm);

        // @(AndroidNamespaceReplacement) maps IO.Agora.Rtc2 -> Agora.Rtc. If the replacement
        // stopped applying — a regenerated binding, a renamed item — the types would still exist
        // and every consumer using the documented namespace would break. Other IO.Agora.* prefixes
        // (io.agora.base and friends) are expected: the rename deliberately covers only the rtc2
        // package, the one this repository documents.
        var leftovers = api.PublicTypes
            .Where(t => t.StartsWith("IO.Agora.Rtc2", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            leftovers.Count == 0,
            $"{Packages.Video} ({tfm}) still has types under IO.Agora.Rtc2: " +
            $"{string.Join(", ", leftovers.Take(5))}…");
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void RtcEngine_exposes_the_channel_lifecycle_entry_points(string tfm)
    {
        using var api = OpenBinding(tfm);

        var methods = api.MethodsOf("Agora.Rtc.RtcEngine");

        Assert.Contains("Create", methods);
        Assert.Contains("Destroy", methods);
        Assert.Contains("EnableVideo", methods);
        Assert.Contains("JoinChannel", methods);
        Assert.Contains("LeaveChannel", methods);
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void RtcEngineConfig_exposes_the_settable_field_properties(string tfm)
    {
        using var api = OpenBinding(tfm);

        var properties = api.PropertiesOf("Agora.Rtc.RtcEngineConfig");

        // The config binds each Java field twice: the M-prefixed field form (settable, the one
        // every consumer needs) and a same-named read-only getter. Losing the field form would
        // make the engine unconstructable from C# while everything still compiled here.
        Assert.Contains("MAppId", properties);
        Assert.Contains("MContext", properties);
        Assert.Contains("MEventHandler", properties);
    }
}
