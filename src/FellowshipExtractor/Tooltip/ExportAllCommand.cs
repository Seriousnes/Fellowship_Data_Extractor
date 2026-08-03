using System.Reflection;
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

    /// <summary>
    /// The leaf a gameplay tag enumeration gives its null member. Five tags in the build end in one
    /// of these two words - ItemID.GemType.Invalid, and the None of CompanionPet, Avatar, Banner
    /// and Title - and no other tag in the 13,088 does.
    /// </summary>
    private static readonly string[] NullMembers = ["Invalid", "None"];

    /// <summary>
    /// Everything an entity can say beyond which tag it is, read off the record so a field added
    /// later counts towards <see cref="Bare"/> without anyone remembering to list it.
    /// </summary>
    private static readonly PropertyInfo[] Content =
    [
        .. typeof(Entity).GetProperties()
            .Where(p => p.Name is not (nameof(Entity.Table) or nameof(Entity.NetId) or nameof(Entity.Name))),
    ];

    /// <summary>
    /// Whether an entry is an enumeration's null member and nothing else. It is the absence of an
    /// item rather than an item, so a line for it is a row a reader has to learn to skip.
    ///
    /// Both halves are required. The name alone would drop a real thing that happens to be called
    /// None, and emptiness alone would drop the 7,454 tags that resolve to an id because their
    /// content lives somewhere this build does not read yet. Together they only catch a tag that
    /// says it is the null member and that the whole build gives nothing to, so a patch that starts
    /// shipping a name or an icon for one puts the line back rather than silently swallowing it.
    /// </summary>
    private static bool NullMember(Entity record) =>
        record.Name is { } name &&
        NullMembers.Any(leaf => name.EndsWith('.' + leaf, StringComparison.OrdinalIgnoreCase)) &&
        Array.TrueForAll(Content, property => property.GetValue(record) is null);

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
            $"{resolver.Vocabulary.Colors.Count} colours, {resolver.Vocabulary.TextStyles.Count} text styles");

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
        var icons = TooltipCommand.IconWriterInto(provider, folder, reportFailures: false);

        var target = Path.Combine(folder, FileName);
        await using var output = new StreamWriter(target, append: false);

        int written = 0, withIcon = 0, withText = 0, resolved = 0;

        // One line per entry, and an entry is identified by its id and by whichever of the
        // dimensions an item spreads over actually applies to it. A duplicate key would mean two
        // lines nothing downstream could tell apart, so it stops the export rather than shipping.
        //
        // The hero is the one that names the line, which is a line covering exactly one hero. Every
        // such line names them, so the shipped name discriminates as well as the codename it
        // replaced and is what a reader sees.
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var dropped = new List<string>();

        // What a later mapping pass has left to do, counted as it is written rather than worked out
        // again afterwards. An entry with a name and no art is one the game shows and this export
        // cannot illustrate, and which table it is in is what says where its art would come from.
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

        // The shared lookup tables, written only once the whole stream has, so the two files beside
        // each other always describe the same run.
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
