using System.IO.Compression;

namespace Net.Agora.Android.PackageTests;

/// <summary>Locates the packed .nupkg files and describes what each is expected to contain.</summary>
public static class Packages
{
    public const string Video = "Net.Agora.Video.Android";
    public const string Voice = "Net.Agora.Voice.Android";

    /// <summary>
    /// Every package build/packages.tsv lists, with the native .aar it is expected to carry.
    /// Pinned rather than parsed from the .tsv: a package silently dropped from the .tsv (and so
    /// from the pack) is a regression these tests should catch, not adapt to.
    /// </summary>
    public static readonly (string Id, string AarPrefix)[] All =
    [
        (Video, "full-rtc-basic-"),
        (Voice, "voice-rtc-basic-"),
    ];

    /// <summary>
    /// Target frameworks every package here must carry, one per SDK band pass. Pinned rather than
    /// discovered: a package that silently lost a target framework because a pack pass failed is
    /// exactly the regression these tests exist to catch.
    /// </summary>
    public static readonly string[] TargetFrameworks =
    [
        "net8.0-android34.0", "net9.0-android35.0", "net10.0-android36.0",
    ];

    public static IEnumerable<object[]> Frameworks =>
        TargetFrameworks.Select(tfm => new object[] { tfm });

    /// <summary>Every (package, target framework) pair — the axis most tests run over.</summary>
    public static IEnumerable<object[]> PackageFrameworks =>
        All.SelectMany(p => TargetFrameworks.Select(tfm => new object[] { p.Id, tfm }));

    /// <summary>Like <see cref="PackageFrameworks"/>, with the expected native .aar name prefix.</summary>
    public static IEnumerable<object[]> PackageFrameworkAars =>
        All.SelectMany(p => TargetFrameworks.Select(tfm => new object[] { p.Id, tfm, p.AarPrefix }));

    public static string ArtifactsDirectory { get; } = ResolveArtifactsDirectory();

    public static string FindPackage(string packageId, string extension = ".nupkg")
    {
        var matches = Directory.Exists(ArtifactsDirectory)
            ? Directory.GetFiles(ArtifactsDirectory, $"{packageId}.*{extension}")
                .Where(f => IsVersionOf(packageId, Path.GetFileName(f), extension))
                .ToArray()
            : [];

        Assert.True(
            matches.Length > 0,
            $"No {packageId}.<version>{extension} found in '{ArtifactsDirectory}'. " +
            "Run build/BuildNugets.sh first.");

        return matches.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static bool IsVersionOf(string packageId, string fileName, string extension)
    {
        var remainder = fileName[(packageId.Length + 1)..^extension.Length];
        return remainder.Length > 0 && char.IsDigit(remainder[0]);
    }

    public static ZipArchive OpenPackage(string packageId, string extension = ".nupkg") =>
        ZipFile.OpenRead(FindPackage(packageId, extension));

    /// <summary>Reads a package entry fully into memory so it can be seeked.</summary>
    public static MemoryStream ReadEntry(ZipArchive package, string entryName)
    {
        var entry = package.GetEntry(entryName);
        Assert.True(entry is not null, $"Package has no entry '{entryName}'.");

        var buffer = new MemoryStream();
        using (var stream = entry!.Open())
        {
            stream.CopyTo(buffer);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static string ResolveArtifactsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName ?? AppContext.BaseDirectory;

        return Environment.GetEnvironmentVariable("AGORA_ARTIFACTS") is { Length: > 0 } configured
            ? Path.GetFullPath(configured, root)
            : Path.Combine(root, "artifacts");
    }
}
