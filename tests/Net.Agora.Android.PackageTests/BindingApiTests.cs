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
    private static AssemblyApi OpenBinding(string packageId, string tfm)
    {
        using var package = Packages.OpenPackage(packageId);
        var assembly = Packages.ReadEntry(package, $"lib/{tfm}/{packageId}.dll");
        return new AssemblyApi(assembly);
    }

    [Theory]
    [MemberData(nameof(Packages.PackageFrameworks), MemberType = typeof(Packages))]
    public void Binding_exposes_the_core_types(string packageId, string tfm)
    {
        // Per package — see Packages.All: the RTC pair share one list (the voice .aar ships the
        // same Java API layer), Signaling has its own under Agora.Rtm.
        using var api = OpenBinding(packageId, tfm);

        var missing = Packages.Row(packageId).CoreTypes.Except(api.PublicTypes).ToList();

        Assert.True(
            missing.Count == 0,
            $"{packageId} ({tfm}) is missing bound types: {string.Join(", ", missing)}. " +
            $"The assembly exposes {api.PublicTypes.Count} public types in total.");
    }

    [Theory]
    [MemberData(nameof(Packages.PackageFrameworks), MemberType = typeof(Packages))]
    public void Binding_is_not_an_empty_shell(string packageId, string tfm)
    {
        using var api = OpenBinding(packageId, tfm);

        // Guards the real failure mode described in src/Agora.Binding.props: @(AndroidMavenLibrary)
        // silently ignored produces a valid but essentially empty assembly, which still packs and
        // installs fine. The floor is per package — see Packages.All — with the margin below each
        // artifact's real count as headroom for Agora trimming their surface, not for a broken
        // build.
        Assert.True(
            api.PublicTypes.Count >= Packages.Row(packageId).MinPublicTypes,
            $"{packageId} ({tfm}) exposes only {api.PublicTypes.Count} public types; " +
            "the binding generator likely did not run.");
    }

    [Theory]
    [MemberData(nameof(Packages.PackageFrameworks), MemberType = typeof(Packages))]
    public void Namespace_rename_left_nothing_behind(string packageId, string tfm)
    {
        using var api = OpenBinding(packageId, tfm);

        // @(AndroidNamespaceReplacement) maps each package's Java-derived prefix (see
        // Packages.All) to Agora's own C#/Unity naming. If the replacement stopped applying — a
        // regenerated binding, a renamed item — the types would still exist and every consumer
        // using the documented namespace would break. Other IO.Agora.* prefixes (io.agora.base
        // and friends) are expected: the rename deliberately covers only the package each
        // repository documents.
        // The trailing dot matters: without it this matches on a type *name* that merely starts
        // with the last segment — Chat's IO.Agora.ChatRoomChangeListener, which lives in the bare
        // io.agora package and is not covered by the io.agora.chat rename at all.
        var legacy = Packages.Row(packageId).LegacyPrefix + ".";
        var leftovers = api.PublicTypes
            .Where(t => t.StartsWith(legacy, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            leftovers.Count == 0,
            $"{packageId} ({tfm}) still has types under {legacy}: " +
            $"{string.Join(", ", leftovers.Take(5))}…");
    }

    [Theory]
    [MemberData(nameof(Packages.RtcPackageFrameworks), MemberType = typeof(Packages))]
    public void RtcEngine_exposes_the_channel_lifecycle_entry_points(string packageId, string tfm)
    {
        using var api = OpenBinding(packageId, tfm);

        var methods = api.MethodsOf("Agora.Rtc.RtcEngine");

        Assert.Contains("Create", methods);
        Assert.Contains("Destroy", methods);
        Assert.Contains("EnableVideo", methods);
        Assert.Contains("JoinChannel", methods);
        Assert.Contains("LeaveChannel", methods);
    }

    [Theory]
    [MemberData(nameof(Packages.RtcPackageFrameworks), MemberType = typeof(Packages))]
    public void RtcEngineConfig_exposes_the_settable_field_properties(string packageId, string tfm)
    {
        using var api = OpenBinding(packageId, tfm);

        var properties = api.PropertiesOf("Agora.Rtc.RtcEngineConfig");

        // The config binds each Java field twice: the M-prefixed field form (settable, the one
        // every consumer needs) and a same-named read-only getter. Losing the field form would
        // make the engine unconstructable from C# while everything still compiled here.
        Assert.Contains("MAppId", properties);
        Assert.Contains("MContext", properties);
        Assert.Contains("MEventHandler", properties);
    }
}
