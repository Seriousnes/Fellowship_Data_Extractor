using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;

namespace FellowshipExtractor.Textures;

/// <summary>
/// Bulk-exports every texture whose container path matches a filter, mirroring the container
/// layout under output/textures so an icon keeps the path the assets refer to it by.
/// </summary>
public static class TextureCommand
{
    public static async Task RunAsync(ExtractorOptions options, string filter, string? formatName)
    {
        var format = TextureExporter.ParseFormat(formatName);
        var provider = await ProviderFactory.CreateAsync(options);

        var matches = provider.Files.Values
            .Where(f => f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            .Where(f => f.Path.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Console.WriteLine($"{matches.Length} package(s) match '{filter}'; writing {format}");

        var root = Path.Combine(options.ResolvedOutputDir, "textures");
        int written = 0, skipped = 0;
        var byPixelFormat = new Dictionary<string, int>(StringComparer.Ordinal);
        var failures = new Dictionary<string, int>(StringComparer.Ordinal);
        var firstFailure = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in matches)
        {
            List<UTexture> textures;
            try
            {
                textures = [.. provider.LoadPackage(file).GetExports().OfType<UTexture>()];
            }
            catch (Exception ex)
            {
                Record(failures, firstFailure, $"{ex.GetType().Name}: {ex.Message}", file.Path);
                continue;
            }

            if (textures.Count == 0) { skipped++; continue; }

            foreach (var texture in textures)
            {
                // One package can hold several textures, so the object name disambiguates.
                var stem = Path.ChangeExtension(file.Path.Replace('/', Path.DirectorySeparatorChar), null)!;
                var target = textures.Count == 1
                    ? Path.Combine(root, stem)
                    : Path.Combine(root, stem + "." + texture.Name);

                var result = TextureExporter.Export(texture, target, format);
                if (result.Written)
                {
                    written++;
                    var key = result.PixelFormat ?? "unknown";
                    byPixelFormat[key] = byPixelFormat.GetValueOrDefault(key) + 1;
                }
                else
                {
                    Record(failures, firstFailure, $"{result.PixelFormat}: {result.Error}", file.Path);
                }
            }
        }

        Console.WriteLine($"wrote {written} image(s) to {root}");
        Console.WriteLine($"  packages holding no texture: {skipped}");

        foreach (var (pixelFormat, count) in byPixelFormat.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"  {count,6}  {pixelFormat}");
        }

        if (failures.Count == 0) return;

        Console.WriteLine($"  failed: {failures.Values.Sum()}");
        foreach (var (reason, count) in failures.OrderByDescending(kv => kv.Value).Take(10))
        {
            Console.WriteLine($"  {count,6}  {reason}   e.g. {firstFailure[reason]}");
        }
    }

    private static void Record(
        Dictionary<string, int> failures, Dictionary<string, string> first, string reason, string path)
    {
        failures[reason] = failures.GetValueOrDefault(reason) + 1;
        first.TryAdd(reason, path);
    }
}
