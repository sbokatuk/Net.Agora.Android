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
}
