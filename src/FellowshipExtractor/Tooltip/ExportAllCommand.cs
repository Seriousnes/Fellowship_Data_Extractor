using System.Text.Json;
using System.Text.Json.Serialization;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// Every named entity in the game as one JSONL file with every icon beside it, in a single flat
/// folder. One line per tag, holding the same record the tooltip command writes for one entity,
/// so anything that reads one reads all of them.
/// </summary>
public static class ExportAllCommand
{
    private const string FolderName = "export";
    private const string FileName = "entities.jsonl";

    private static readonly JsonSerializerOptions LineOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Exports every table, or only the named ones.</summary>
    public static async Task RunAsync(ExtractorOptions options, string[] tables)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var resolver = new TooltipResolver(provider);

        Console.WriteLine(
            $"presentation index: {resolver.Presentation.RowCount} rows across {resolver.Presentation.TableCount} tables");
        Console.WriteLine($"tuning constants:   {resolver.ConstantCount} keys");
        Console.WriteLine($"granted abilities:  {resolver.Mappings.RowCount} mappings");
        Console.WriteLine($"hero names:         {resolver.Heroes.Count}");

        var wanted = new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);
        var selected = resolver.Tags
            .Where(t => wanted.Count == 0 || wanted.Contains(t.Table))
            .ToArray();

        if (selected.Length == 0)
        {
            Console.Error.WriteLine($"No tags in table(s) {string.Join(", ", tables)}.");
            Console.Error.WriteLine($"Known tables: {string.Join(", ", resolver.Tags.Select(t => t.Table).Distinct().Order())}");
            return;
        }

        var folder = Path.Combine(options.ResolvedOutputDir, FolderName);
        Directory.CreateDirectory(folder);

        // Icons land beside the JSONL under their own texture name, so an icon shared by many
        // entities is decoded and written once and every entity referencing it agrees on the file.
        var icons = TooltipCommand.IconWriterInto(provider, folder, reportFailures: false, prefixWithHint: false);

        var target = Path.Combine(folder, FileName);
        await using var output = new StreamWriter(target, append: false);

        int written = 0, withIcon = 0, withText = 0;

        foreach (var tag in selected)
        {
            var record = resolver.Resolve(tag, icons);
            await output.WriteLineAsync(JsonSerializer.Serialize(record, LineOptions));

            written++;
            if (record.IconImage is not null) withIcon++;
            if (record.DisplayName is not null) withText++;

            if (written % 500 == 0) Console.WriteLine($"  {written} of {selected.Length}...");
        }

        await output.FlushAsync();

        var images = Directory.EnumerateFiles(folder, "*.png").Count();
        Console.WriteLine($"\nwrote {target}");
        Console.WriteLine($"  {written} entities, {withText} with a display name, {withIcon} with an icon");
        Console.WriteLine($"  {images} images in {folder}");
    }
}
