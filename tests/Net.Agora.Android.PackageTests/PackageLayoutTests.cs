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
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Video_carries_a_binding_assembly_for_every_target_framework(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Video);

        var entry = package.GetEntry($"lib/{tfm}/{Packages.Video}.dll");
        Assert.True(entry is not null, $"{Packages.Video} is missing the assembly for {tfm}.");

        // The empty-shell failure mode is a real .dll, just a tiny one with no bound types — a
        // present-but-missing check would not catch it. A binding of io.agora.rtc:full-rtc-basic
        // compiles to several hundred KB at minimum.
        Assert.True(
            entry!.Length > 500_000,
            $"{Packages.Video}'s assembly for {tfm} is only {entry.Length} bytes — looks like an " +
            "empty binding shell rather than a real one.");
    }

    [Theory]
    [MemberData(nameof(Packages.Frameworks), MemberType = typeof(Packages))]
    public void Video_carries_the_native_aars_for_every_target_framework(string tfm)
    {
        using var package = Packages.OpenPackage(Packages.Video);

        var entries = package.Entries
            .Where(e => e.FullName.StartsWith($"lib/{tfm}/", StringComparison.Ordinal)
                        && e.FullName.EndsWith(".aar", StringComparison.Ordinal))
            .ToList();

        Assert.True(entries.Count > 0, $"{Packages.Video} carries no .aar for {tfm}.");

        var full = entries.SingleOrDefault(e => e.Name.StartsWith("full-rtc-basic-", StringComparison.Ordinal));
        Assert.True(full is not null, $"{Packages.Video} is missing full-rtc-basic-*.aar for {tfm}.");

        // Tens of MB with jniLibs for four ABIs. Anything small means a placeholder.
        Assert.True(full!.Length > 20_000_000, $"'{full.FullName}' is only {full.Length} bytes; looks empty.");
    }
}
