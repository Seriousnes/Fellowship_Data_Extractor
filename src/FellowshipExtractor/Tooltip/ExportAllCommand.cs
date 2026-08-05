using System.Reflection;
using System.Text.Json.Serialization;
using FellowshipExtractor.Output;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// Every named entity in the game as its type's document, handed to the format the run was given,
/// with every icon beside it in a single flat folder. The rows are resolved first and projected
/// after, because a document addresses another row by type and no lookup from id to type is whole
/// until every row has been resolved.
/// </summary>
public static class ExportAllCommand
{
    private const string FolderName = "export";

    /// <summary>
    /// Every tag that another tag hangs beneath. A branch is not an entity, so the 807 that also say
    /// nothing are not written; a branch that does say something is, which is what keeps the six gem
    /// types, each carrying four tiers beneath it.
    ///
    /// 807 is every branch the export leaves out, whichever reason it leaves it out under. 84 of
    /// them are dropped under the empty branch reason the run prints and the other 723 under no type
    /// at all, because <see cref="NotWritten"/> asks what type a row is before it asks whether the
    /// row is a branch.
    /// </summary>
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

    /// <summary>
    /// Everything an entity can say beyond which tag it is, read off the record so a field added
    /// later counts without anyone remembering to list it. A property the export never carries is
    /// not something the row says either, so anything marked <see cref="JsonIgnoreAttribute"/> is
    /// excluded the same way, read off the property rather than named: <see cref="Entity.Variants"/>
    /// is why this matters, since it is set on the way past resolution and cleared only on some
    /// paths, and without this exclusion a row carrying nothing else would still say something.
    /// </summary>
    private static readonly PropertyInfo[] Content =
    [
        .. typeof(Entity).GetProperties()
            .Where(p => p.Name is not (nameof(Entity.Table) or nameof(Entity.NetId) or nameof(Entity.Name)
                or nameof(Entity.Type)))
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null),
    ];

    private static bool Says(Entity entity) => Array.Exists(Content, p => p.GetValue(entity) is not null);

    /// <summary>
    /// The leaf a gameplay tag enumeration gives its null member. Five tags in the build end in one
    /// of these two words - ItemID.GemType.Invalid, and the None of CompanionPet, Avatar, Banner
    /// and Title - and no other tag in the 13,088 does.
    /// </summary>
    private static readonly string[] NullMembers = ["Invalid", "None"];

    /// <summary>
    /// Whether an entry is an enumeration's null member and nothing else. It is the absence of an
    /// item rather than an item, so a line for it is a row a reader has to learn to skip.
    ///
    /// Both halves are required. The name alone would drop a real thing that happens to be called
    /// None, and emptiness alone would drop every tag that resolves to an id because its content
    /// lives somewhere this build does not read yet.
    /// </summary>
    private static bool NullMember(Entity entity) =>
        entity.Name is { } name &&
        Array.Exists(NullMembers, leaf => name.EndsWith($".{leaf}", StringComparison.OrdinalIgnoreCase)) &&
        !Says(entity);

    /// <summary>Why a row does not reach the store, or null where it does.</summary>
    public static string? NotWritten(Entity entity, IReadOnlySet<string> branches)
    {
        // Spec 3.3: a tag naming an enumeration's null member is not an entity. 5 tags in the build.
        if (NullMember(entity)) return "a null enum member";

        if (entity.Type is null) return "no type";

        if (entity.Name is { } name && branches.Contains(name) && !Says(entity)) return "empty branch";

        // Spec 3.3: a creature carrying nothing is a type decided against. 106 of the 338 rows.
        if (entity.Type is EntityType.Creature && !Says(entity)) return "a creature carrying nothing";

        return null;
    }

    /// <summary>Exports every table, or only the named ones.</summary>
    public static async Task RunAsync(ExtractorOptions options, string[] tables, IOutputFormat output)
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

        // Icons land in the export folder under their own texture name, so an icon shared by many
        // entities is decoded and written once and every entity referencing it agrees on the file.
        var icons = TooltipCommand.IconWriterInto(provider, folder, reportFailures: false);

        int written = 0, withIcon = 0, withText = 0, scanned = 0;

        // One document per entry, and an entry is identified by its id and by whichever of the
        // dimensions an item spreads over actually applies to it. A duplicate key would mean two
        // rows nothing downstream could tell apart, so it stops the export rather than shipping.
        //
        // The hero is the one that names the row, which is a row covering exactly one hero. Every
        // such row names them, so the shipped name discriminates as well as the codename it
        // replaced and is what a reader sees.
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var dropped = new SortedDictionary<string, int>(StringComparer.Ordinal);

        // What a later mapping pass has left to do, counted as it is written rather than worked out
        // again afterwards. An entry with a name and no art is one the game shows and this export
        // cannot illustrate, and which table it is in is what says where its art would come from.
        var iconless = new SortedDictionary<string, int>(StringComparer.Ordinal);

        var branches = Branches(resolver.Tags);
        var resolved = new List<Entity>();

        foreach (var tag in selected)
        {
            foreach (var record in resolver.Resolve(tag, icons))
            {
                if (NotWritten(record, branches) is { } reason)
                {
                    dropped[reason] = dropped.GetValueOrDefault(reason) + 1;
                    continue;
                }

                // Every top-level row is addressed by its table and its id, both taken from the tag
                // it was resolved from and neither of them nullable there. Stated once here, because
                // the lookup keys on both and every arm of the projection dereferences the id, and a
                // row that broke the invariant would otherwise fail as a null dereference deep in a
                // switch with nothing saying which row it was.
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

            scanned++;
            if (scanned % 500 == 0) Console.WriteLine($"  {scanned} of {selected.Length}...");
        }

        // What type a table and an id name together, over the rows the export keeps. A document
        // addresses another row by type and id, so a pointer at a row nothing wrote resolves to
        // nothing rather than to a type no line carries.
        //
        // Keyed on the table as well, because a netId is an index within its own table rather than a
        // corpus-wide id: 3,261 of them are live in more than one type-yielding table and 2,307 are
        // live in both GameplayAbility and GameplayEffect. Both ends of the join take the table from
        // the same TagRecord, so the strings match exactly.
        var types = resolved
            .GroupBy(row => (Table: row.Table!, NetId: row.NetId!.Value))
            .ToDictionary(group => group.Key, group => group.First().Type);

        DocumentProjection.TypeLookup lookup = (table, netId) => types.GetValueOrDefault((table, netId));

        foreach (var record in resolved)
        {
            await output.WriteAsync(DocumentProjection.From(record, lookup), default);
            written++;

            // One talent slot is a slot and a talent both, which is why 12,436 rows write 12,437.
            if (DocumentProjection.Implied(record) is { } implied)
            {
                await output.WriteAsync(implied, default);
                written++;
            }
        }

        // The shared lookup tables, written only once the whole stream has, so the two always
        // describe the same run.
        await output.WriteVocabularyAsync(resolver.Vocabulary, default);

        var images = Directory.EnumerateFiles(folder, "*.png").Count();
        Console.WriteLine($"\nwrote {written} documents from {resolved.Count} entities across {scanned} tags");
        Console.WriteLine($"  {withText} entities with a display name, {withIcon} with an icon");
        if (dropped.Count > 0)
        {
            Console.WriteLine(
                $"  {dropped.Values.Sum()} entries dropped: " +
                string.Join(", ", dropped.Select(entry => $"{entry.Key} {entry.Value}")));
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
        Console.WriteLine("wrote the vocabulary the documents are read against");
    }
}
