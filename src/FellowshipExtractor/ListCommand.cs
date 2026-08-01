namespace FellowshipExtractor;

/// <summary>
/// Dumps the container file listing. The .utoc/.pak directory indexes are readable with no
/// mappings and no AES key, so this is the map every later question gets answered against.
/// </summary>
public static class ListCommand
{
    public static async Task RunAsync(ExtractorOptions options)
    {
        var provider = await ProviderFactory.CreateAsync(options);

        Console.WriteLine($"Mounted {provider.MountedVfs.Count} container(s), {provider.Files.Count} files.");
        foreach (var vfs in provider.MountedVfs)
        {
            Console.WriteLine($"  {vfs.Name}  files={vfs.FileCount}  encrypted={vfs.IsEncrypted}");
        }

        var listPath = Path.Combine(options.ResolvedOutputDir, "files.txt");
        var paths = provider.Files.Keys.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        await File.WriteAllLinesAsync(listPath, paths);
        Console.WriteLine($"Wrote {paths.Length} paths to {listPath}");

        var extPath = Path.Combine(options.ResolvedOutputDir, "extensions.txt");
        var extensions = paths
            .GroupBy(p => Path.GetExtension(p).ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Count(),8}  {(g.Key.Length == 0 ? "(none)" : g.Key)}");
        await File.WriteAllLinesAsync(extPath, extensions);
        Console.WriteLine($"Wrote extension histogram to {extPath}");

        ProveDecompression(provider);
    }

    /// <summary>
    /// Reads a file big enough to be Oodle-compressed and checks the bytes are the text we
    /// expect. A tiny file would be stored raw and would pass even with Oodle broken.
    /// </summary>
    private static void ProveDecompression(CUE4Parse.FileProvider.DefaultFileProvider provider)
    {
        const string probePath = "fellowship/Content/guid/ItemID.csv";
        if (!provider.Files.TryGetValue(probePath, out var probe))
        {
            Console.Error.WriteLine($"Decompression probe skipped: {probePath} not found.");
            return;
        }

        var text = System.Text.Encoding.UTF8.GetString(probe.Read());
        if (!text.Contains("ItemID.", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Decompression probe failed: {probePath} did not decode to the expected text.");
        }

        Console.WriteLine($"Decompression probe OK: {probePath} decoded to {text.Length} chars of expected text");
    }
}
