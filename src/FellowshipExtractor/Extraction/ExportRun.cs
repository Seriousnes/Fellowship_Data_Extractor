using System.Reflection;
using System.Text.Json.Serialization;
using FellowshipExtractor.Output;
using FellowshipExtractor.Textures;

namespace FellowshipExtractor.Extraction;

public static class ExportRun
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

    private static bool HasContent(Entity entity) => Array.Exists(Content, p => p.GetValue(entity) is not null);

    private static readonly string[] NullMembers = ["Invalid", "None"];

    private static bool NullMember(Entity entity) =>
        entity.Name is { } name &&
        Array.Exists(NullMembers, leaf => name.EndsWith($".{leaf}", StringComparison.OrdinalIgnoreCase)) &&
        !HasContent(entity);

    public static string? NotWritten(Entity entity, IReadOnlySet<string> branches)
    {
        if (NullMember(entity)) return "a null enum member";

        if (entity.Type is null) return "no type";

        if (entity.Name is { } name && branches.Contains(name) && !HasContent(entity)) return "empty branch";

        if (entity.Type is EntityType.Creature && !HasContent(entity)) return "a creature carrying nothing";

        return null;
    }

    public static async Task RunAsync(ExtractorOptions options, IExportTarget target)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var resolver = new EntityResolver(provider);

        Console.WriteLine(
            $"presentation index: {resolver.Presentation.RowCount} rows across {resolver.Presentation.TableCount} tables, " +
            $"{resolver.Presentation.AdditionalAssetCount} additional abilities");
        Console.WriteLine($"tuning constants:   {resolver.ConstantCount} keys");
        Console.WriteLine(
            $"granted abilities:  {resolver.Mappings.RowCount} mappings naming " +
            $"{resolver.Mappings.GrantedCount} augmentations");
        Console.WriteLine($"hero names:         {resolver.Heroes.Count}");
        var profiles = resolver.Heroes.Profiles.ToArray();
        Console.WriteLine(
            $"hero prose:         {profiles.Count(hero => hero.Title is not null)} of {profiles.Length} titled, " +
            $"{profiles.Count(hero => hero.Biography is not null)} with a biography, " +
            $"{profiles.Count(hero => hero.Playstyle is not null)} with a playstyle, " +
            $"{profiles.Count(hero => hero.SpiritAbility is not null)} with a spirit ability");
        Console.WriteLine($"fixed hero items:   {resolver.HeroItems.Count} rows");
        Console.WriteLine($"talents:            {resolver.Talents.Count} across {resolver.Talents.HeroCount} trees");
        Console.WriteLine(
            $"gems:               {resolver.Gems.TypeCount} types, {resolver.Gems.SizeCount} sizes, " +
            $"{resolver.Gems.NodeCount} gem nodes, {resolver.Gems.ReplacementCount} superseded");
        Console.WriteLine(
            $"item traits:        {resolver.Traits.Count} rows, {resolver.Traits.BoundRuns} of " +
            $"{resolver.Traits.TotalRuns} rank runs bound to a curve");
        Console.WriteLine($"set bonuses:        {resolver.SetBonuses.Count} rows");
        Console.WriteLine(
            $"hero dynamic text:  {resolver.DynamicText.SentenceCount} sentences across " +
            $"{resolver.DynamicText.RowCount} row keys, {resolver.DynamicText.Substituted} tokens substituted, " +
            $"left literal: {Literal(resolver.DynamicText.Literal)}");

        foreach (var table in resolver.DynamicText.Unresolved)
        {
            Console.WriteLine(
                $"  {table} declares the dynamic text row structure but is not a " +
                "DT_DynamicText_Hero_<codename> table the roster can key");
        }
        Console.WriteLine(
            $"dungeons:           {resolver.Dungeons.Count} named, {resolver.Locations.Dungeons} with a map, " +
            $"{resolver.Locations.Maps} umaps read, {resolver.Locations.Placed} creatures placed, " +
            $"{resolver.Locations.PlacedByFolder} of them by the folder they live in, " +
            $"{resolver.Locations.Unplaced} placed in none");
        Console.WriteLine(
            $"modifiers:          {resolver.Modifiers.Count} rows across {resolver.Modifiers.TableCount} tables, " +
            $"{resolver.Modifiers.WithIcon} with an icon");
        Console.WriteLine(
            $"progression items:  {resolver.Progression.Count} rows across {resolver.Progression.TableCount} tables");
        Console.WriteLine(
            $"customizations:     {resolver.Customizations.Count} rows across " +
            $"{resolver.Customizations.TableCount} tables, {resolver.Customizations.Unnamed} naming nothing");

        foreach (var (kind, source) in resolver.Customizations.NameSourceByKind)
        {
            Console.WriteLine(
                $"  {kind}: {resolver.Customizations.RowsByKind[kind]} rows named from {source}");
        }
        Console.WriteLine(
            $"localized strings:  {resolver.Localized.Count} in {resolver.Localized.Culture}, " +
            $"{resolver.Localized.Contested.Count} keys two namespaces disagree on");
        Console.WriteLine($"calculation presets:{resolver.PresetCount} rows carrying a default spread");
        Console.WriteLine(
            $"settings:           {resolver.Settings.Heroes.Count} heroes, {resolver.Settings.Rarities.Count} " +
            $"rarities, {resolver.Settings.PrintFormats.Count} print formats, " +
            $"{resolver.Settings.Colors.Count} colours, {resolver.Settings.TextStyles.Count} text styles, " +
            $"{resolver.Settings.SlotIcons.Count} slot glyphs, " +
            $"{resolver.Settings.MaxTempers.Count} tempering ladders, " +
            $"{resolver.Settings.AbilityCategories.Count} ability categories");
        Console.WriteLine(
            $"item power:         {resolver.Settings.Items.PowerPotential.ScorePerGemType.Count} gem tiers " +
            $"and {resolver.Settings.Items.ItemGemPowers.Count} slots scored, item level to " +
            $"{resolver.Settings.Items.MaxItemLevel} in steps of " +
            $"{resolver.Settings.Items.ItemLevelsPerUpgrade}");

        var icons = new IconImages(
            provider, resolver.Collections.Renders, RarityImages.Ladder(resolver.Settings));

        int written = 0, withIcon = 0, withText = 0, scanned = 0;

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var dropped = new SortedDictionary<string, int>(StringComparer.Ordinal);

        var iconless = new SortedDictionary<string, int>(StringComparer.Ordinal);

        var branches = Branches(resolver.Tags);
        var resolved = new List<Entity>();

        foreach (var tag in resolver.Tags)
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
            if (scanned % 500 == 0) Console.WriteLine($"  {scanned} of {resolver.Tags.Count}...");
        }

        var shared = resolver.Gains.Shared(resolved);

        var types = shared
            .GroupBy(row => (Table: row.Table!, NetId: row.NetId!.Value))
            .ToDictionary(group => group.Key, group => group.First().Type);

        var names = new Dictionary<(EntityType Type, int Id), string>();

        foreach (var record in shared)
        {
            if (record.Type is not { } type) continue;
            if (DocumentProjection.Name(record) is not { } named) continue;

            names.TryAdd((type, record.NetId!.Value), named);

            if (DocumentProjection.Implied(record) is { } implied && implied.Name is { } borne)
            {
                names.TryAdd((EntityTypes.TypeOf(implied), implied.Id), borne);
            }
        }

        var folded = AdditionalAbilities.Fold(shared, resolver.Presentation);

        var references = new DocumentProjection.References(
            (table, netId) => types.GetValueOrDefault((table, netId)),
            (type, id) => names.GetValueOrDefault((type, id)),
            folded.Redirects);

        var roles = new EffectRoles(shared, resolver.Heroes.Codenames);

        var marked = SlottedNames.Mark(folded.Entities, resolver.Owners.Slotted);

        foreach (var record in marked)
        {
            await target.WriteDocumentAsync(DocumentProjection.From(record, references, roles), default);
            written++;

            if (DocumentProjection.Implied(record) is { } implied)
            {
                await target.WriteDocumentAsync(implied, default);
                written++;
            }
        }

        var chrome = await ChromeTextures.WriteAsync(provider, resolver.Settings, target, default);

        await target.WriteSettingsAsync(resolver.Settings.ToFile(icons.Writer), default);

        Console.WriteLine($"\nwrote {written} documents from {resolved.Count} entities across {scanned} tags");
        Console.WriteLine($"  {withText} entities with a display name, {withIcon} with an icon");
        Console.WriteLine(
            $"  {folded.Redirects.Count} abilities a presentation row lists under AdditionalGameplayAbilityAssets " +
            "folded into the ability the row presents");
        Console.WriteLine(
            $"  {marked.Count(record => record.NamedAfter is not null)} unslotted abilities named after the ability " +
            "a hero's ability set slots under the same name");
        Console.WriteLine($"  {resolver.Superseded} tag ids superseded by a later id for the same name");
        Console.WriteLine(
            $"  {roles.Parts} effects named as part of one of {roles.Abilities} tagged abilities, " +
            $"{roles.Roles} of them carrying a role, {roles.Withheld} withheld for naming a hero");
        if (dropped.Count > 0)
        {
            Console.WriteLine(
                $"  {dropped.Values.Sum()} entries dropped: " +
                string.Join(", ", dropped.Select(entry => $"{entry.Key} {entry.Value}")));
        }
        Console.WriteLine(
            $"  {icons.Count} entity icon images and {chrome} chrome images written, " +
            $"{icons.Composed} of the icons composed against a rarity colour");
        Console.WriteLine(
            $"  {resolver.Lore.Count} mounts whose lore the localization table keys by their own tag: " +
            string.Join(", ", resolver.Lore.KeyByTag.Select(entry => $"{entry.Key} from {entry.Value}")));
        Console.WriteLine(
            $"  {resolver.NamesInherited} names inherited from the entity that applies them, " +
            $"{resolver.NamesContested} withheld because two entities that apply them disagree");
        Console.WriteLine(
            $"  {resolver.EffectNames.Widened} effects reached for a name and {resolver.EffectIcons.Widened} " +
            $"for an icon only through the applier scan, {resolver.EffectNames.Unpackaged} named tags carrying " +
            "no package to scan");
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
        Console.WriteLine(
            $"  {resolver.NamespaceSupplied} tuning rows supplied by namespace that no asset names, " +
            $"{resolver.NamespaceCorroborated} named fields an applied effect binds to the value the row declares");
        Console.WriteLine(
            $"  {resolver.DeclaredByBlueprint} tokens bound by the ability's ResolveDynamicTextFloat, " +
            $"{resolver.NamedByToken} by the quantity their name states, " +
            $"{resolver.SynthesizedFromModifier} from the sole modifier their description leaves undeclared, " +
            $"{resolver.StatedByNativeLink} by a stated link to the row native code reads, " +
            $"{resolver.PrintedAsReduction} printed as the share their multiplier removes");
        Console.WriteLine(
            $"  {resolver.ModifiersRead} effect modifier magnitudes read");

        var generations = resolver.Gains.Totals;

        Console.WriteLine(
            $"  {resolver.Gains.Generations} resource generations found while walking the graphs, counting one " +
            "visit per walk of a body, written as " +
            $"{generations.Entries} entries over {generations.Documents} documents: " +
            $"{generations.Amounts} stating an amount, {generations.Shares} a share of the pool ceiling, " +
            $"{generations.Filled} both, {generations.Amountless} neither");

        foreach (var (reason, count) in resolver.Gains.Dropped)
        {
            Console.WriteLine($"    {count} dropped because {reason}");
        }

        foreach (var ceiling in resolver.Gains.RuntimeCeilings()) Console.WriteLine($"    {ceiling}");

        Console.WriteLine(
            $"  every hero carries a ceiling row for {string.Join(", ", resolver.Pools.Universal)}, " +
            "so one hero's noun for one of those names it for all");

        Console.WriteLine("  the names the shipped sentences print each resource channel under:");

        foreach (var named in resolver.ResourceNames.Lines()) Console.WriteLine($"    {named}");
        foreach (var contested in resolver.ResourceNames.Contested()) Console.WriteLine($"    {contested}");
        foreach (var unnamed in resolver.ResourceNames.Unnamed()) Console.WriteLine($"    {unnamed}");

        if (resolver.Gains.Frames.Count > 0)
        {
            Console.WriteLine(
                $"  the {resolver.Gains.Frames.Count} distinct bytecode frames a generation was dropped for " +
                "naming with no number:");

            foreach (var frame in resolver.Gains.Frames) Console.WriteLine($"    {frame}");
        }

        Console.WriteLine(
            $"  {resolver.DurationsFromSetByCaller} of {resolver.SetByCallerDurationsScanned} durations the " +
            "applier sets by caller recovered from its bytecode");

        foreach (var recovered in resolver.SetByCallerDurationsRecovered)
        {
            Console.WriteLine($"    {recovered}");
        }

        Console.WriteLine(
            $"  {resolver.Falloffs.Thresholds} of {resolver.Falloffs.Calls} multi-target damage falloff calls read " +
            $"a threshold, {resolver.Falloffs.ReachedThresholds} of them in a spawned actor or a granted ability, " +
            $"{resolver.Falloffs.EffectsVerified} tied to the one effect reading the scaler's tag, " +
            $"{resolver.Falloffs.EffectsAmbiguous} read by several; {resolver.Falloffs.Caps} target caps stated");

        foreach (var unread in resolver.Falloffs.Unread.Distinct(StringComparer.Ordinal))
        {
            Console.WriteLine($"    {unread}");
        }
        Console.WriteLine(
            $"  {resolver.SpreadOverrides} coefficient rows banded by the Spread row their effect overrides " +
            $"with, {resolver.SpreadPresetDefaults} by an effect's calculation preset");

        foreach (var row in resolver.SpreadRowsUnbindable)
        {
            Console.WriteLine($"  {row} is overridden onto an effect but no constants table declares it");
        }

        foreach (var row in resolver.SpreadContested)
        {
            Console.WriteLine($"  no band for {row}: two effects bind it under different presets");
        }
        Console.WriteLine("wrote the settings the documents are read against");
    }

    private static string Literal(IReadOnlyDictionary<string, int> counts) =>
        counts.Count == 0
            ? "none"
            : string.Join(
                ", ",
                counts.OrderByDescending(entry => entry.Value)
                    .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => $"{entry.Key} x{entry.Value}"));
}
