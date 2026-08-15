using System.Text.Json;
using System.Text.Json.Serialization;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse_Conversion.Textures;
using FellowshipExtractor.Textures;

namespace FellowshipExtractor.Tooltip;
public static class TooltipCommand
{
    public static async Task RunAsync(ExtractorOptions options, string table, string id)
    {
        var provider = await ProviderFactory.CreateAsync(options);
        var resolver = new TooltipResolver(provider);

        Console.WriteLine(
            $"presentation index: {resolver.Presentation.RowCount} rows across {resolver.Presentation.TableCount} " +
            $"CRAbilityDataRow tables ({resolver.Presentation.FailedTables.Count} unreadable)");
        foreach (var (path, reason) in resolver.Presentation.FailedTables.Take(5))
        {
            Console.WriteLine($"  unreadable: {path} - {reason}");
        }

        Console.WriteLine($"tuning constants:   {resolver.ConstantCount} keys");
        Console.WriteLine($"granted abilities:  {resolver.Mappings.RowCount} mappings across {resolver.Mappings.TableCount} tables");
        Console.WriteLine($"item slot types:    {resolver.Slots.Count}");
        Console.WriteLine($"hero names:         {resolver.Heroes.Count} resolved from metadata assets");
        Console.WriteLine($"named augmentations:{resolver.Charges.NamedAugmentations} searched for the charges they supply");
        Console.WriteLine($"talents:            {resolver.Talents.Count} across {resolver.Talents.HeroCount} trees");
        Console.WriteLine(
            $"gems:               {resolver.Gems.TypeCount} types, {resolver.Gems.SizeCount} sizes, " +
            $"{resolver.Gems.NodeCount} ladder nodes, {resolver.Gems.ReplacementCount} superseded");
        Console.WriteLine(
            $"item traits:        {resolver.Traits.Count} rows, {resolver.Traits.BoundRuns} of " +
            $"{resolver.Traits.TotalRuns} rank runs bound to a curve");
        Console.WriteLine($"calculation presets:{resolver.PresetCount} rows carrying a default spread");
        Console.WriteLine(
            $"effect icon sources:{resolver.EffectIcons.Determined} effects can take an icon from the entity " +
            $"that applies them, {resolver.EffectIcons.Contested} contested");
        Console.WriteLine(
            $"settings:           {resolver.Settings.Heroes.Count} heroes, {resolver.Settings.Rarities.Count} " +
            $"rarities, {resolver.Settings.PrintFormats.Count} print formats");

        var (covered, total) = resolver.Coverage(table);
        Console.WriteLine($"{table} coverage: {covered} of {total} names have a presentation row");

        var tag = resolver.FindTag(table, id);
        if (tag is null)
        {
            Console.Error.WriteLine($"No entry in table '{table}' with id or name '{id}'.");
            Console.Error.WriteLine(
                $"Search output/{CsvExportCommands.TagsName} for the table and name you want.");
            return;
        }

        var iconDir = Path.Combine(options.ResolvedOutputDir, "textures", "tooltip");

        var records = resolver.Resolve(tag.Value, IconWriterInto(provider, iconDir, reportFailures: true, resolver.Collections.Renders));

        var target = Path.Combine(options.ResolvedOutputDir, $"tooltip-{tag.Value.Table}-{tag.Value.NetId}.json");
        await File.WriteAllTextAsync(target, JsonSerializer.Serialize(records, JsonOptions));

        Console.WriteLine($"\n{records.Count} entr{(records.Count == 1 ? "y" : "ies")} for {tag.Value.Name}");
        foreach (var record in records) Report(record, "  ");
        Console.WriteLine($"\ntuning rows supplied by namespace: {resolver.NamespaceSupplied}");
        Console.WriteLine(
            $"tokens bound by ResolveDynamicTextFloat: {resolver.DeclaredByBlueprint}, " +
            $"by the quantity their name states: {resolver.NamedByToken}");
        Console.WriteLine($"icons in {iconDir}");
        Console.WriteLine($"wrote {target}");
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IconWriter IconWriterInto(
        AbstractFileProvider provider, string directory, bool reportFailures, IReadOnlySet<string> renders)
    {
        var written = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(directory);

        return (iconPath, _) =>
        {
            if (written.TryGetValue(iconPath, out var cached)) return cached;

            var texture = TextureExporter.Load(provider, iconPath);
            if (texture is null)
            {
                if (reportFailures) Console.Error.WriteLine($"icon {iconPath} did not load as a texture");
                return written[iconPath] = null;
            }

            var export = TextureExporter.Export(
                texture, Path.Combine(directory, texture.Name), ETextureFormat.Png,
                discardAlpha: renders.Contains(iconPath));

            if (!export.Written)
            {
                if (reportFailures) Console.Error.WriteLine($"icon {iconPath} did not decode: {export.Error}");
                return written[iconPath] = null;
            }

            return written[iconPath] = Path.GetFileName(export.File);
        };
    }

    private static void Report(Entity entity, string indent)
    {
        Console.WriteLine();
        Console.WriteLine($"{indent}{entity.DisplayName?.ToUpperInvariant() ?? entity.Name}");

        var reading = new[] { entity.Slot, entity.ArmorType, entity.PrimaryStat }.OfType<string>();
        if (reading.Any()) Console.WriteLine($"{indent}{string.Join(" / ", reading)}");

        if (entity.Heroes is { } wearers) Console.WriteLine($"{indent}for: {string.Join(", ", wearers)}");
        if (entity.SetBonus is { } set)
        {
            Console.WriteLine($"{indent}set: {string.Join(", ", set.Select(bonus => bonus.Name))}");
        }
        if (entity.SetCount is { } pieces) Console.WriteLine($"{indent}pieces: {pieces}");
        if (entity.AllowedRoles is { } roles) Console.WriteLine($"{indent}roles: {string.Join(", ", roles)}");
        if (entity.AllowedAttributes is { } gates) Console.WriteLine($"{indent}attributes: {string.Join(", ", gates)}");
        if (entity.TraitCategory is { } category) Console.WriteLine($"{indent}category: {category} ({entity.TraitCategoryTag})");
        if (entity.MaxRank is { } ranks) Console.WriteLine($"{indent}max rank: {ranks}, rolled at weight {entity.RandomChance}");
        if (entity.GemType is { } gem) Console.WriteLine($"{indent}gem: {gem}");
        if (entity.GemTier is { } tier) Console.WriteLine($"{indent}tier {tier}, {entity.GemPower} power, {entity.Rarity}");
        if (entity.GemNode is { } node) Console.WriteLine($"{indent}node {node} at {entity.RequiredGemPower} power, replaces {entity.Replaces?.ToString() ?? "nothing"}");
        if (entity.GemAttribute is { } attribute) Console.WriteLine($"{indent}accumulates: {attribute}");
        if (entity.Augmentation is { } augmentation) Console.WriteLine($"{indent}augmentation: {augmentation}");
        if (entity.GemSlot is true) Console.WriteLine($"{indent}gem socket");
        if (entity.UniqueEquipped is true) Console.WriteLine($"{indent}unique equipped");
        if (entity.Tags is { } tags) Console.WriteLine($"{indent}{string.Join(", ", tags)}");
        if (entity.Stats is { } stats) Console.WriteLine($"{indent}stats: {string.Join(", ", stats)}");
        if (entity.Resource is { } resource) Console.WriteLine($"{indent}resource: {resource}");

        foreach (var (field, value) in Fields(entity))
        {
            Console.WriteLine($"{indent}{field,-12} {value}");
        }

        foreach (var line in (entity.Description ?? "").Split('\n'))
        {
            if (line.Trim().Length > 0) Console.WriteLine($"{indent}{line.TrimEnd('\r')}");
        }

        foreach (var line in entity.Detail ?? []) Console.WriteLine($"{indent}| {line.ReplaceLineEndings(" ")}");

        if (entity.ScalesOff is { } scaling) Console.WriteLine($"{indent}scales off: {string.Join(", ", scaling)}");

        foreach (var (token, placeholder) in entity.Placeholders ?? new Dictionary<string, Placeholder>())
        {
            var off = placeholder.ScalesOff is { } s ? $"  ({string.Join(", ", s)})" : "";
            var own = placeholder.FromOwnConstants is true ? "  from this entity's own constants" : "";
            var shown = placeholder.Text is { } word
                ? $" = {word}"
                : placeholder.Value is { } printed ? $" = {printed}" : "";
            var format = placeholder.Format is { } f ? $" as {f}" : "";
            var band = placeholder.Spread is { } spread
                ? $" x{placeholder.Coefficient} of {placeholder.Attribute ?? "nothing named"}, " +
                  $"spread {spread} ({1 - spread / 2:0.###} to {1 + spread / 2:0.###} of the mean)"
                : "";
            Console.WriteLine($"{indent}{{{token}}}{shown}{format}{band}{off}{own}");
            foreach (var (key, value) in placeholder.Constants ?? new Dictionary<string, object?>())
            {
                Console.WriteLine($"{indent}  {key,-58} {Show(value)}");
            }
        }

        foreach (var run in entity.RankRuns ?? [])
        {
            var bound = run.Key is null
                ? "bound to no curve"
                : $"{run.Key} [{run.Transform}{(run.Ticks is { } ticks ? $" x{ticks}" : "")}] " +
                  $"{string.Join(" / ", (run.Values ?? []).Select(v => v?.ToString() ?? "?"))}" +
                  (run.Disagrees is true ? "  DISAGREES with the printed text" : "");

            Console.WriteLine($"{indent}rank run {string.Join(" / ", run.Printed)}");
            Console.WriteLine($"{indent}  {bound}");
        }

        if (entity.GemOverflow is { } overflow)
        {
            Console.WriteLine($"{indent}overflow: {overflow.Description}");
            foreach (var (token, argument) in overflow.Placeholders ?? new Dictionary<string, Placeholder>())
            {
                var reads = argument.Text ?? $"{argument.Value} as {argument.Format}";
                Console.WriteLine($"{indent}  {{{token}}} {reads}");
            }
            if (overflow.Icon is { } overflowIcon) Console.WriteLine($"{indent}  icon: {overflowIcon}");
        }

        if (entity.Icon is { } icon) Console.WriteLine($"{indent}icon: {icon}");

        if (entity.Constants is { } constants)
        {
            Console.WriteLine($"{indent}{constants.Count} constant(s):");
            foreach (var (key, value) in constants) Console.WriteLine($"{indent}  {key,-58} {Show(value)}");
        }

        if (entity.GrantedTags is { } tags2) Console.WriteLine($"{indent}grants tags: {string.Join(", ", tags2)}");

        foreach (var (talent, fields) in entity.Talents ?? new Dictionary<string, IReadOnlyDictionary<string, object?>>())
        {
            var gate = entity.TalentGates?.GetValueOrDefault(talent) is { } tag
                ? $"  requires {tag}"
                : "  (no condition declared)";
            Console.WriteLine($"{indent}talent {talent}:{gate}");
            foreach (var (key, value) in fields) Console.WriteLine($"{indent}  {key,-58} {Show(value)}");
        }

        if (entity.Grants is { } granted)
        {
            Console.WriteLine($"\n{indent}grants:");
            Report(granted, indent + "  ");
        }

        foreach (var stat in entity.LegendaryStats ?? [])
        {
            Console.WriteLine($"\n{indent}legendary stat:");
            Report(stat, indent + "  ");
        }

    }

    private static IEnumerable<(string, double)> Fields(Entity entity)
    {
        if (entity.CastTime is { } cast) yield return ("castTime", cast);
        if (entity.ChannelTime is { } channel) yield return ("channelTime", channel);
        if (entity.ChannelTick is { } tick) yield return ("channelTick", tick);
        if (entity.Cooldown is { } cooldown) yield return ("cooldown", cooldown);
        if (entity.Charges is { } charges) yield return ("charges", charges);
        if (entity.Cost is { } cost) yield return ("cost", cost);
        if (entity.Range is { } range) yield return ("range", range);
        if (entity.Radius is { } radius) yield return ("radius", radius);
        if (entity.Duration is { } duration) yield return ("duration", duration);
        if (entity.Period is { } period) yield return ("period", period);
    }

    private static string Show(object? value) => value switch
    {
        null => "unresolved",
        double[] ranks => string.Join(" / ", ranks),
        _ => value.ToString() ?? "",
    };

    internal static string? TagValue(FAssetData asset, string key) =>
        asset.TagsAndValues.FirstOrDefault(kv => kv.Key.Text.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
}
