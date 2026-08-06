using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FellowshipExtractor.Tooltip;

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

    private static readonly string[] NullMembers = ["Invalid", "None"];

    private static readonly PropertyInfo[] Content =
    [
        .. typeof(Entity).GetProperties()
            .Where(p => p.Name is not (nameof(Entity.Table) or nameof(Entity.NetId) or nameof(Entity.Name))),
    ];

    private static bool NullMember(Entity record) =>
        record.Name is { } name &&
        NullMembers.Any(leaf => name.EndsWith('.' + leaf, StringComparison.OrdinalIgnoreCase)) &&
        Array.TrueForAll(Content, property => property.GetValue(record) is null);

    public static async Task RunAsync(ExtractorOptions options, string[] tables)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var resolver = new TooltipResolver(provider);

        Console.WriteLine(
            $"presentation index: {resolver.Presentation.RowCount} rows across {resolver.Presentation.TableCount} tables");
        Console.WriteLine($"tuning constants:   {resolver.ConstantCount} keys");
        Console.WriteLine($"granted abilities:  {resolver.Mappings.RowCount} mappings");
        Console.WriteLine($"hero names:         {resolver.Heroes.Count}");
        Console.WriteLine($"fixed hero items:   {resolver.HeroItems.Count} rows");
        Console.WriteLine($"talents:            {resolver.Talents.Count} across {resolver.Talents.HeroCount} trees");
        Console.WriteLine(
            $"gems:               {resolver.Gems.TypeCount} types, {resolver.Gems.SizeCount} sizes, " +
            $"{resolver.Gems.NodeCount} ladder nodes, {resolver.Gems.ReplacementCount} superseded");
        Console.WriteLine(
            $"item traits:        {resolver.Traits.Count} rows, {resolver.Traits.BoundRuns} of " +
            $"{resolver.Traits.TotalRuns} rank runs bound to a curve");
        Console.WriteLine($"set bonuses:        {resolver.SetBonuses.Count} rows");
        Console.WriteLine($"calculation presets:{resolver.PresetCount} rows carrying a default spread");
        Console.WriteLine(
            $"vocabulary:         {resolver.Vocabulary.Heroes.Count} heroes, {resolver.Vocabulary.Rarities.Count} " +
            $"rarities, {resolver.Vocabulary.PrintFormats.Count} print formats, " +
            $"{resolver.Vocabulary.Colors.Count} colours, {resolver.Vocabulary.TextStyles.Count} text styles, " +
            $"{resolver.Vocabulary.SlotIcons.Count} slot glyphs, " +
            $"{resolver.Vocabulary.MaxTempers.Count} tempering ladders");

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

        var icons = TooltipCommand.IconWriterInto(provider, folder, reportFailures: false);

        var target = Path.Combine(folder, FileName);
        await using var output = new StreamWriter(target, append: false);

        int written = 0, withIcon = 0, withText = 0, resolved = 0;

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var dropped = new List<string>();

        var iconless = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (var tag in selected)
        {
            foreach (var record in resolver.Resolve(tag, icons))
            {
                if (NullMember(record))
                {
                    dropped.Add(record.Name!);
                    continue;
                }

                var hero = record.Heroes is { Count: 1 } only ? only[0] : null;
                var key = $"{record.Table}/{record.NetId}/{record.ArmorType}/{record.PrimaryStat}/{hero}";
                if (!keys.Add(key))
                {
                    throw new InvalidOperationException(
                        $"Two entries share the key {key}. Expansion has produced entries that differ in " +
                        "something other than armour type, primary stat and hero.");
                }

                await output.WriteLineAsync(JsonSerializer.Serialize(record, LineOptions));

                written++;
                if (record.Icon is not null) withIcon++;
                if (record.DisplayName is not null) withText++;

                if (record is { DisplayName: not null, Icon: null, Table: { } table })
                {
                    iconless[table] = iconless.GetValueOrDefault(table) + 1;
                }
            }

            resolved++;
            if (resolved % 500 == 0) Console.WriteLine($"  {resolved} of {selected.Length}...");
        }

        await output.FlushAsync();

        var sidecar = await resolver.Vocabulary.WriteAsync(folder);

        var images = Directory.EnumerateFiles(folder, "*.png").Count();
        Console.WriteLine($"\nwrote {target}");
        Console.WriteLine($"  {written} entries from {resolved} tags, {withText} with a display name, {withIcon} with an icon");
        if (dropped.Count > 0)
        {
            Console.WriteLine($"  {dropped.Count} enum null members dropped: {string.Join(", ", dropped)}");
        }
        Console.WriteLine($"  {images} images in {folder}");
        Console.WriteLine(
            $"  {resolver.NamesInherited} names inherited from the entity that applies them, " +
            $"{resolver.NamesContested} withheld because two entities that apply them disagree");
        Console.WriteLine(
            $"  {resolver.IconsInherited} icons inherited from the entity that applies them, " +
            $"{resolver.IconsContested} withheld because two entities that apply them disagree");
        Console.WriteLine(
            $"  {iconless.Values.Sum()} named entries still with no icon: " +
            string.Join(", ", iconless.Select(entry => $"{entry.Key} {entry.Value}")));
        Console.WriteLine($"  {resolver.NamespaceSupplied} tuning rows supplied by namespace that no asset names");
        Console.WriteLine(
            $"  {resolver.SpreadOverrides} coefficient rows banded by their own Spread row, " +
            $"{resolver.SpreadPresetDefaults} by their effect's calculation preset");

        foreach (var row in resolver.SpreadContested)
        {
            Console.WriteLine($"  no band for {row}: two effects bind it under different presets");
        }
        Console.WriteLine($"wrote {Path.Combine(folder, Vocabulary.FileName)} ({sidecar} bytes)");
    }
}
