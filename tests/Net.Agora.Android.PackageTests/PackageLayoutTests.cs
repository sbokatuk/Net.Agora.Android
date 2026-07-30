namespace Net.Agora.Android.PackageTests;

/// <summary>
/// Asserts the shape of the produced NuGet packages. Runs against the packed .nupkg rather than
/// the build output, so it catches packaging regressions the compiler cannot see — most
/// importantly the net8.0-android34.0 "empty shell" trap: AndroidMavenLibrary does not exist in
/// that SDK pack, is silently ignored, and produces a binding assembly with 0 warnings, 0 errors
/// and no real content. See src/Agora.Binding.props.
/// </summary>
public class PackageLayoutTests
{
    [Theory]
    [MemberData(nameof(Packages.PackageFrameworks), MemberType = typeof(Packages))]
    public void Package_carries_a_binding_assembly_for_every_target_framework(string packageId, string tfm)
    {
        using var package = Packages.OpenPackage(packageId);

        var entry = package.GetEntry($"lib/{tfm}/{packageId}.dll");
        Assert.True(entry is not null, $"{packageId} is missing the assembly for {tfm}.");

        // The empty-shell failure mode is a real .dll, just a tiny one with no bound types — a
        // present-but-missing check would not catch it. The floor is per package (see
        // Packages.All): the RTC bindings compile to several hundred KB, Signaling to less.
        Assert.True(
            entry!.Length > Packages.MinAssemblyBytesOf(packageId),
            $"{packageId}'s assembly for {tfm} is only {entry.Length} bytes — looks like an " +
            "empty binding shell rather than a real one.");
    }

    [Theory]
    [MemberData(nameof(Packages.PackageFrameworkAars), MemberType = typeof(Packages))]
    public void Package_carries_the_native_aars_for_every_target_framework(
        string packageId, string tfm, string aarPrefix, long minAarBytes)
    {
        using var package = Packages.OpenPackage(packageId);

        var entries = package.Entries
            .Where(e => e.FullName.StartsWith($"lib/{tfm}/", StringComparison.Ordinal)
                        && e.FullName.EndsWith(".aar", StringComparison.Ordinal))
            .ToList();

        Assert.True(entries.Count > 0, $"{packageId} carries no .aar for {tfm}.");

        var native = entries.SingleOrDefault(e => e.Name.StartsWith(aarPrefix, StringComparison.Ordinal));
        Assert.True(native is not null, $"{packageId} is missing {aarPrefix}*.aar for {tfm}.");

        // jniLibs for four ABIs put every real artifact well above its floor (see Packages.All);
        // anything below means a placeholder.
        Assert.True(native!.Length > minAarBytes, $"'{native.FullName}' is only {native.Length} bytes; looks empty.");
    }

    [Theory]
    [MemberData(nameof(Packages.NativePayloadPackageFrameworks), MemberType = typeof(Packages))]
    public void Package_does_not_ship_the_native_payload_twice(string packageId, string tfm)
    {
        using var package = Packages.OpenPackage(packageId);

        // Every non-application project produces its own <PackageId>.aar and the SDK packs it
        // beside the artifacts. For these packages the SDK fills it with the jni/*.so set lifted
        // out of a companion artifact in the same folder — up to 12 MB duplicated per target
        // framework, and a duplicate-library (XA4301) warning in the consuming app. Suppressed by
        // AgoraRemoveDuplicateProjectAar; asserted here because the waste is invisible in a build
        // log. The packages whose project .aar carries real content (the ProGuard rules) are not
        // on this axis — see Packages.NativePayloadPackages.
        Assert.Null(package.GetEntry($"lib/{tfm}/{packageId}.aar"));
    }

    [Theory]
    [MemberData(nameof(Packages.SignalingPackageFrameworks), MemberType = typeof(Packages))]
    public void Signaling_package_does_not_bundle_agora_rtm_s_own_stale_aosl(string packageId, string tfm)
    {
        using var package = Packages.OpenPackage(packageId);

        var rtmAar = package.Entries.SingleOrDefault(e =>
            e.FullName.StartsWith($"lib/{tfm}/", StringComparison.Ordinal)
            && e.Name.StartsWith("agora-rtm-", StringComparison.Ordinal));
        Assert.True(rtmAar is not null, $"{packageId} carries no agora-rtm-*.aar for {tfm}.");

        using var rtmAarStream = Packages.ReadEntry(package, rtmAar!.FullName);
        using var rtmAarArchive = new ZipArchive(rtmAarStream, ZipArchiveMode.Read);

        // agora-rtm's own .aar vendors an older libaosl.so, missing symbols (aosl_ref_magic and
        // friends) the RTC engine needs — see AgoraStripNativeLibraryEntries in
        // src/Agora.Binding.props. Left in place, an app referencing both this package and Video
        // or Voice can end up with either aosl at lib/<abi>/libaosl.so depending on native-library
        // merge order, and RtcEngine.Create() returns null silently when the older one wins.
        var staleAosl = rtmAarArchive.Entries
            .Where(e => e.FullName.StartsWith("jni/", StringComparison.Ordinal) && e.Name == "libaosl.so")
            .ToList();
        Assert.True(
            staleAosl.Count == 0,
            $"{packageId}'s agora-rtm .aar for {tfm} still bundles its own libaosl.so " +
            $"({string.Join(", ", staleAosl.Select(e => e.FullName))}) — the native-library " +
            "version conflict AgoraStripNativeLibraryEntries strips this to prevent would reintroduce itself.");
    }

    [Theory]
    [MemberData(nameof(Packages.SignalingPackageFrameworks), MemberType = typeof(Packages))]
    public void Signaling_package_ships_the_same_aosl_version_as_the_rtc_bindings(string packageId, string tfm)
    {
        using var signaling = Packages.OpenPackage(packageId);
        using var video = Packages.OpenPackage(Packages.Video);

        var signalingAosl = signaling.Entries.SingleOrDefault(e =>
            e.FullName.StartsWith($"lib/{tfm}/", StringComparison.Ordinal)
            && e.Name.StartsWith("aosl-", StringComparison.Ordinal));
        var videoAosl = video.Entries.SingleOrDefault(e =>
            e.FullName.StartsWith($"lib/{tfm}/", StringComparison.Ordinal)
            && e.Name.StartsWith("aosl-", StringComparison.Ordinal));

        Assert.True(signalingAosl is not null, $"{packageId} carries no aosl-*.aar for {tfm}.");
        Assert.True(videoAosl is not null, $"{Packages.Video} carries no aosl-*.aar for {tfm}.");

        // Directory.Build.props pins AgoraAoslVersion separately from AgoraVideoVersion/
        // AgoraVoiceVersion because nothing ties them together — Video and Voice get aosl
        // transitively through full-rtc-basic/voice-rtc-basic's own Maven dependency, Signaling
        // through an explicit pin added purely to fix the stale-copy bug above. A version bump to
        // either line that is not re-checked against the other would silently reintroduce the
        // exact conflict AgoraStripNativeLibraryEntries exists to prevent — this is the check
        // Directory.Build.props's AgoraAoslVersion comment promises.
        Assert.Equal(videoAosl!.Name, signalingAosl!.Name);
    }

    [Theory]
    [MemberData(nameof(Packages.ProguardPackageIds), MemberType = typeof(Packages))]
    public void Package_ships_r8_keep_rules_when_its_aar_has_none(string packageId)
    {
        using var package = Packages.OpenPackage(packageId);

        // Both halves must travel together: the rules file, and the buildTransitive targets
        // (named exactly <PackageId>.targets or NuGet never imports it) that feeds the rules to
        // the consuming app's R8 run. See src/Agora.Proguard.targets for why the bindings cannot
        // rely on R8's own reachability analysis.
        using var rules = new StreamReader(Packages.ReadEntry(package, "proguard/proguard.cfg"));
        Assert.Contains("-keep class", rules.ReadToEnd());

        using var targets = new StreamReader(Packages.ReadEntry(package, $"buildTransitive/{packageId}.targets"));
        Assert.Contains("ProguardConfiguration", targets.ReadToEnd());
    }
}
