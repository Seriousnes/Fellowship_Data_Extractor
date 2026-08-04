using System.Text.Json;
using CUE4Parse.UE4.Assets;
using CUE4Parse.UE4.Assets.Exports;

namespace FellowshipExtractor;

/// <summary>
/// Loads a cooked package and serializes its exports. This is the first thing that needs
/// property mappings, so it doubles as the probe for whether a .usmap is required.
/// </summary>
public static class DumpCommand
{
    /// <summary>
    /// One asset by default, because a filter is usually a name and mounting the build again per
    /// asset is what makes a sweep expensive. Raise <paramref name="limit"/> to dump a whole folder
    /// in one mount.
    /// </summary>
    public static async Task RunAsync(ExtractorOptions options, string filter, int limit = 1)
    {
        var provider = await ProviderFactory.CreateAsync(options);

        var matches = provider.Files.Values
            .Where(f => f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            .Where(f => f.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();

        if (matches.Length == 0)
        {
            Console.Error.WriteLine($"No .uasset matching '{filter}'.");
            return;
        }

        foreach (var file in matches)
        {
            Console.WriteLine($"===== {file.Path}");
            var package = provider.LoadPackage(file);
            Console.WriteLine($"  {Describe(package)}");

            var target = Path.Combine(
                options.ResolvedOutputDir, "dump",
                Path.ChangeExtension(file.Path.Replace('/', Path.DirectorySeparatorChar), ".json"));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using var stream = File.Create(target);
            await using var writer = new Utf8JsonWriter(stream, UeJson.WriterOptions);
            writer.WriteStartArray();
            foreach (var export in package.GetExports())
            {
                UeJson.WriteObject(writer, export);
            }
            writer.WriteEndArray();
            await writer.FlushAsync();

            Console.WriteLine($"  wrote {target}");
        }

        if (UeJson.StringifiedTypes.Count > 0)
        {
            Console.WriteLine("  types written as plain strings (candidates for real handling):");
            foreach (var (type, count) in UeJson.StringifiedTypes.OrderByDescending(kv => kv.Value))
            {
                Console.WriteLine($"    {count,6}  {type}");
            }
        }
    }

    private static string Describe(IPackage package) => package switch
    {
        IoPackage io => $"iostore, flags={io.Summary.PackageFlags}",
        Package legacy => $"legacy, flags={legacy.Summary.PackageFlags}",
        _ => package.GetType().Name,
    };
}
