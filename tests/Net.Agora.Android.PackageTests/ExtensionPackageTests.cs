using System.Xml.Linq;

namespace Net.Agora.Android.PackageTests;

/// <summary>
/// Asserts the shape of the optional RTC feature extension packages. They are not bindings — see
/// src/Agora.Extension.md — so what is worth checking about them is almost the inverse of
/// <see cref="PackageLayoutTests"/>: the native payload has to be there for every target
/// framework, the managed assembly is expected to be an empty stub, and the package must pull in
/// neither RTC binding.
/// </summary>
public class ExtensionPackageTests
{
    [Theory]
    [MemberData(nameof(Packages.ExtensionFrameworkAars), MemberType = typeof(Packages))]
    public void Extension_carries_its_native_aar_for_every_target_framework(
        string packageId, string tfm, string aarPrefix, long minAarBytes)
    {
        using var package = Packages.OpenPackage(packageId);

        // This is the whole product. The switch that turns the extension on already exists in the
        // RTC binding; the .so in this .aar is the thing whose absence turns that switch into a
        // runtime error code.
        var native = package.Entries.SingleOrDefault(e =>
            e.FullName.StartsWith($"lib/{tfm}/", StringComparison.Ordinal) &&
            e.Name.StartsWith(aarPrefix, StringComparison.Ordinal) &&
            e.Name.EndsWith(".aar", StringComparison.Ordinal));

        Assert.True(native is not null, $"{packageId} is missing {aarPrefix}*.aar for {tfm}.");
        Assert.True(
            native!.Length > minAarBytes,
            $"'{native.FullName}' is only {native.Length} bytes; looks like a placeholder.");
    }

    [Theory]
    [MemberData(nameof(Packages.ExtensionIds), MemberType = typeof(Packages))]
    public void Extension_depends_on_neither_RTC_binding(string packageId)
    {
        using var package = Packages.OpenPackage(packageId);

        var nuspec = package.Entries.Single(e => e.Name.EndsWith(".nuspec", StringComparison.Ordinal));
        using var stream = nuspec.Open();
        var document = XDocument.Load(stream);

        // The <dependency> elements specifically, not the nuspec text: each package's description
        // names both RTC packages on purpose, to tell the reader what to add this alongside.
        var dependencies = document.Descendants()
            .Where(e => e.Name.LocalName == "dependency")
            .Select(e => (string?)e.Attribute("id") ?? "")
            .ToList();

        // The Video and Voice bindings are mutually exclusive in one app, so a dependency on
        // either would force a flavour on the consumer — and the audio extensions work with both.
        Assert.DoesNotContain("Net.Agora.Video.Android", dependencies);
        Assert.DoesNotContain("Net.Agora.Voice.Android", dependencies);
    }

    [Theory]
    [MemberData(nameof(Packages.ExtensionIds), MemberType = typeof(Packages))]
    public void Extension_ships_a_stub_assembly_rather_than_a_binding(string packageId)
    {
        using var package = Packages.OpenPackage(packageId);

        foreach (var tfm in Packages.TargetFrameworks)
        {
            var entry = package.GetEntry($"lib/{tfm}/{packageId}.dll");
            Assert.True(entry is not null, $"{packageId} is missing the assembly for {tfm}.");

            // Documenting the intent rather than guarding a regression: if one of these artifacts
            // ever grows a real Java API, this is where it will show up, and the package will
            // need AgoraBindArtifact and a place in Packages.All instead.
            Assert.True(
                entry!.Length < 100_000,
                $"{packageId}'s assembly for {tfm} is {entry.Length} bytes — that is a real " +
                "binding, not the expected stub. Did the .aar grow a classes.jar?");
        }
    }
}
