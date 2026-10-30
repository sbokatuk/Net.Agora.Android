using System.Reflection;
using Microsoft.Maui.Controls;

namespace Net.Agora.Sample;

/// <summary>
/// Starts a sample's fields filled in from what its build was given. See
/// samples/Directory.Build.targets. An ordinary build carries nothing, so these change nothing and
/// the fields start as the XAML left them.
/// </summary>
internal static class SamplePrefill
{
    /// <summary>The value the build carried for <paramref name="key"/>, or an empty string.</summary>
    public static string Get(string key) =>
        typeof(SamplePrefill).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == $"AgoraSample.{key}")?.Value ?? string.Empty;

    /// <summary>
    /// Fills <paramref name="entry"/>. A secret is masked as well, so that neither a screenshot nor
    /// a UI dump of a live run shows it.
    /// </summary>
    public static void Fill(Entry entry, string key, bool secret = true)
    {
        var value = Get(key);
        if (value.Length == 0)
        {
            return;
        }

        entry.Text = value;
        entry.IsPassword = secret;
    }
}
