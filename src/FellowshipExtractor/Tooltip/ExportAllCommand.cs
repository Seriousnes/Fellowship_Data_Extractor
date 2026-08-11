using System.Reflection;
using System.Text.Json.Serialization;
using FellowshipExtractor.Output;
using FellowshipExtractor.Textures;

namespace FellowshipExtractor.Tooltip;

public static class ExportAllCommand
{
    public static IReadOnlySet<string> Branches(IEnumerable<TagRecord> tags)
    {
        var branches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            var segments = tag.Name.Split('.');
            for (var at = 1; at < segments.Length; at++)
            {
                branches.Add(string.Join('.', segments[..at]));
            }
        }

        return branches;
    }

    private static readonly PropertyInfo[] Content =
    [
        .. typeof(Entity).GetProperties()
            .Where(p => p.Name is not (nameof(Entity.Table) or nameof(Entity.NetId) or nameof(Entity.Name)
                or nameof(Entity.Type)))
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null),
    ];

    private static bool Says(Entity entity) => Array.Exists(Content, p => p.GetValue(entity) is not null);

    private static readonly string[] NullMembers = ["Invalid", "None"];

    private static bool NullMember(Entity entity) =>
        entity.Name is { } name &&
        Array.Exists(NullMembers, leaf => name.EndsWith($".{leaf}", StringComparison.OrdinalIgnoreCase)) &&
        !Says(entity);

    public static string? NotWritten(Entity entity, IReadOnlySet<string> branches)
    {
        if (NullMember(entity)) return "a null enum member";

        if (entity.Type is null) return "no type";

        if (entity.Name is { } name && branches.Contains(name) && !Says(entity)) return "empty branch";

        if (entity.Type is EntityType.Creature && !Says(entity)) return "a creature carrying nothing";

        return null;
    }

    public static async Task RunAsync(ExtractorOptions options, string[] tables, IExportTarget target)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var resolver = new TooltipResolver(provider);

        Console.WriteLine(
            $"presentation index: {resolver.Presentation.RowCount} rows across {resolver.Presentation.TableCount} tables");
        Console.WriteLine($"tuning constants:   {resolver.ConstantCount} keys");
        Console.WriteLine(
            $"granted abilities:  {resolver.Mappings.RowCount} mappings naming " +
            $"{resolver.Mappings.GrantedCount} augmentations");
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
        Console.WriteLine(
            $"dungeons:           {resolver.Dungeons.Count} named, {resolver.Locations.Dungeons} with a map, " +
            $"{resolver.Locations.Maps} umaps read, {resolver.Locations.Placed} creatures placed, " +
            $"{resolver.Locations.Unplaced} placed in none");
        Console.WriteLine($"calculation presets:{resolver.PresetCount} rows carrying a default spread");
        Console.WriteLine(
            $"settings:           {resolver.Settings.Heroes.Count} heroes, {resolver.Settings.Rarities.Count} " +
            $"rarities, {resolver.Settings.PrintFormats.Count} print formats, " +
            $"{resolver.Settings.Colors.Count} colours, {resolver.Settings.TextStyles.Count} text styles, " +
            $"{resolver.Settings.SlotIcons.Count} slot glyphs, " +
            $"{resolver.Settings.MaxTempers.Count} tempering ladders");

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

        var icons = new IconImages(
            provider, resolver.Collections.Renders, RarityImages.Ladder(resolver.Settings));

        int written = 0, withIcon = 0, withText = 0, scanned = 0;

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var dropped = new SortedDictionary<string, int>(StringComparer.Ordinal);

        var iconless = new SortedDictionary<string, int>(StringComparer.Ordinal);

        var branches = Branches(resolver.Tags);
        var resolved = new List<Entity>();

        foreach (var tag in selected)
        {
            foreach (var record in resolver.Resolve(tag, icons.Writer))
            {
                if (NotWritten(record, branches) is { } reason)
                {
                    dropped[reason] = dropped.GetValueOrDefault(reason) + 1;
                    continue;
                }

                if (record is not { Table: not null, NetId: not null })
                {
                    throw new InvalidOperationException(
                        $"{record.Name} reached the export without a table or a net id. Every row " +
                        "resolved from a tag carries both.");
                }

                var hero = record.Heroes is { Count: 1 } only ? only[0] : null;
                var key = $"{record.Type}/{record.NetId}/{record.ArmorType}/{record.PrimaryStat}/{hero}";
                if (!keys.Add(key))
                {
                    throw new InvalidOperationException(
                        $"Two entries share the key {key}. Expansion has produced entries that differ in " +
                        "something other than armour type, primary stat and hero.");
                }

                resolved.Add(record);

                if (record.Icon is not null) withIcon++;
                if (record.DisplayName is not null) withText++;

                if (record is { DisplayName: not null, Icon: null, Table: { } table })
                {
                    iconless[table] = iconless.GetValueOrDefault(table) + 1;
                }
            }

            await icons.FlushAsync(target, default);

            scanned++;
            if (scanned % 500 == 0) Console.WriteLine($"  {scanned} of {selected.Length}...");
        }

        var types = resolved
            .GroupBy(row => (Table: row.Table!, NetId: row.NetId!.Value))
            .ToDictionary(group => group.Key, group => group.First().Type);

        var names = new Dictionary<(EntityType Type, int Id), string>();

        foreach (var record in resolved)
        {
            if (record.Type is not { } type) continue;
            if (DocumentProjection.Name(record) is not { } named) continue;

            names.TryAdd((type, record.NetId!.Value), named);

            if (DocumentProjection.Implied(record) is { } implied && implied.Name is { } borne)
            {
                names.TryAdd((EntityTypes.TypeOf(implied), implied.Id), borne);
            }
        }

        var references = new DocumentProjection.References(
            (table, netId) => types.GetValueOrDefault((table, netId)),
            (type, id) => names.GetValueOrDefault((type, id)));

        foreach (var record in resolved)
        {
            await target.WriteDocumentAsync(DocumentProjection.From(record, references), default);
            written++;

            if (DocumentProjection.Implied(record) is { } implied)
            {
                await target.WriteDocumentAsync(implied, default);
                written++;
            }
        }

        var chrome = await ChromeTextures.WriteAsync(provider, resolver.Settings, target, default);

        await target.WriteSettingsAsync(resolver.Settings.ToFile(), default);

        Console.WriteLine($"\nwrote {written} documents from {resolved.Count} entities across {scanned} tags");
        Console.WriteLine($"  {withText} entities with a display name, {withIcon} with an icon");
        if (dropped.Count > 0)
        {
            Console.WriteLine(
                $"  {dropped.Values.Sum()} entries dropped: " +
                string.Join(", ", dropped.Select(entry => $"{entry.Key} {entry.Value}")));
        }
        Console.WriteLine(
            $"  {icons.Written} entity icon images and {chrome} chrome images written, " +
            $"{icons.Composed} of the icons composed against a rarity colour");
        Console.WriteLine(
            $"  {resolver.NamesInherited} names inherited from the entity that applies them, " +
            $"{resolver.NamesContested} withheld because two entities that apply them disagree");
        Console.WriteLine(
            $"  {resolver.IconsInherited} icons inherited from the entity that applies them, " +
            $"{resolver.IconsContested} withheld because two entities that apply them disagree");
        Console.WriteLine(
            $"  {iconless.Values.Sum()} named entries still with no icon: " +
            string.Join(", ", iconless.Select(entry => $"{entry.Key} {entry.Value}")));
        Console.WriteLine(
            $"  {resolver.CreatureStats.Namespaces} creature tuning namespaces, " +
            $"{resolver.CreatureStats.Healthless} creatures resolving neither health row, " +
            $"{resolver.CreatureStats.ScaledApart} scaling apart from Default");

        Console.WriteLine(
            $"  {resolver.Leagues.Count} leagues covering difficulty 1 to {resolver.Leagues.Ceiling}, " +
            $"{resolver.StatedCoefficients} coefficients stated outside a description, " +
            $"{resolver.NamespaceFromBindings} namespaces taken from the rows an entity binds");
        Console.WriteLine($"  {resolver.NamespaceSupplied} tuning rows supplied by namespace that no asset names");
        Console.WriteLine(
            $"  {resolver.DeclaredByBlueprint} tokens bound by the ability's ResolveDynamicTextFloat, " +
            $"{resolver.NamedByToken} by the quantity their name states");
        Console.WriteLine(
            $"  {resolver.SpreadOverrides} coefficient rows banded by their own Spread row, " +
            $"{resolver.SpreadPresetDefaults} by their effect's calculation preset");

        foreach (var row in resolver.SpreadContested)
        {
            Console.WriteLine($"  no band for {row}: two effects bind it under different presets");
        }
        Console.WriteLine("wrote the settings the documents are read against");
    }
}
