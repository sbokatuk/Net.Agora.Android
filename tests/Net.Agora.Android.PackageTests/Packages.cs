using System.IO.Compression;

namespace Net.Agora.Android.PackageTests;

/// <summary>Locates the packed .nupkg files and describes what each is expected to contain.</summary>
public static class Packages
{
    public const string Video = "Net.Agora.Video.Android";
    public const string Voice = "Net.Agora.Voice.Android";
    public const string Signaling = "Net.Agora.Signaling.Android";

    /// <summary>
    /// The types a consumer of the RTC bindings starts with, all under the renamed namespace.
    /// One list for both RTC packages: Agora ships the same Java API layer in voice-rtc-basic —
    /// including the video types — and strips only the native video pipeline.
    /// </summary>
    private static readonly string[] RtcCoreTypes =
    [
        "Agora.Rtc.RtcEngine",
        "Agora.Rtc.RtcEngineConfig",
        "Agora.Rtc.IRtcEngineEventHandler",
        "Agora.Rtc.ChannelMediaOptions",
        "Agora.Rtc.Video.VideoCanvas",
        "Agora.Rtc.Video.VideoEncoderConfiguration",
    ];

    /// <summary>The types a consumer of the Signaling binding starts with.</summary>
    private static readonly string[] SignalingCoreTypes =
    [
        "Agora.Rtm.RtmClient",
        "Agora.Rtm.RtmConfig",
        "Agora.Rtm.IRtmEventListener",
        "Agora.Rtm.MessageEvent",
        "Agora.Rtm.IResultCallback",
    ];

    /// <summary>
    /// Every package build/packages.tsv lists, with what its packed form is expected to contain:
    /// the native .aar (by name prefix and a size floor that rules out placeholders), the types a
    /// consumer starts with, the Java-derived namespace prefix the rename must have emptied, and
    /// a public-type floor that rules out an empty binding shell. Pinned rather than parsed from
    /// the .tsv: a package silently dropped from the .tsv (and so from the pack) is a regression
    /// these tests should catch, not adapt to.
    /// </summary>
    public static readonly (string Id, string AarPrefix, long MinAarBytes, long MinAssemblyBytes, string[] CoreTypes, string LegacyPrefix, int MinPublicTypes)[] All =
    [
        // The RTC .aars are tens of MB with jniLibs for four ABIs and bind ~300 public types
        // into a >500 KB assembly.
        (Video, "full-rtc-basic-", 20_000_000, 500_000, RtcCoreTypes, "IO.Agora.Rtc2", 200),
        (Voice, "voice-rtc-basic-", 20_000_000, 500_000, RtcCoreTypes, "IO.Agora.Rtc2", 200),
        // agora-rtm is a smaller product: a ~16 MB .aar and ~60 public types. The floors still
        // sit far above the net8 empty-shell failure mode (a few KB, zero bound types).
        (Signaling, "agora-rtm-", 10_000_000, 100_000, SignalingCoreTypes, "IO.Agora.Rtm", 40),
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

    /// <summary>Like <see cref="PackageFrameworks"/>, with the expected native .aar name and floor.</summary>
    public static IEnumerable<object[]> PackageFrameworkAars =>
        All.SelectMany(p => TargetFrameworks.Select(tfm => new object[] { p.Id, tfm, p.AarPrefix, p.MinAarBytes }));

    public static long MinAssemblyBytesOf(string packageId) => Row(packageId).MinAssemblyBytes;

    /// <summary>The RTC packages only — the axis of the RtcEngine-specific member checks.</summary>
    public static IEnumerable<object[]> RtcPackageFrameworks =>
        All.Where(p => p.LegacyPrefix == "IO.Agora.Rtc2")
            .SelectMany(p => TargetFrameworks.Select(tfm => new object[] { p.Id, tfm }));

    public static (string Id, string AarPrefix, long MinAarBytes, long MinAssemblyBytes, string[] CoreTypes, string LegacyPrefix, int MinPublicTypes) Row(string packageId) =>
        All.Single(p => p.Id == packageId);

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
