using System.Text.Json;
using System.Text.Json.Serialization;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse_Conversion.Textures;
using FellowshipExtractor.Textures;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// Reconstructs what the game shows for one named entity and writes it out with the icon decoded
/// to a real image. The resolution itself lives in <see cref="TooltipResolver"/> so the bulk
/// export runs the same code over every tag and produces the same record.
/// </summary>
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

        var (covered, total) = resolver.Coverage(table);
        Console.WriteLine($"{table} coverage: {covered} of {total} names have a presentation row");

        var tag = resolver.FindTag(table, id);
        if (tag is null)
        {
            Console.Error.WriteLine($"No entry in table '{table}' with id or name '{id}'.");
            Console.Error.WriteLine("Run 'tags' and search tags.jsonl for the table and name you want.");
            return;
        }

        var iconDir = Path.Combine(options.ResolvedOutputDir, "textures", "tooltip");
        var record = resolver.Resolve(tag.Value, IconWriterInto(provider, iconDir, reportFailures: true));

        var target = Path.Combine(options.ResolvedOutputDir, $"tooltip-{tag.Value.Table}-{tag.Value.NetId}.json");
        await File.WriteAllTextAsync(target, JsonSerializer.Serialize(record, JsonOptions));

        Report(record, "  ");
        Console.WriteLine($"\ntuning rows supplied by namespace: {resolver.NamespaceSupplied}");
        Console.WriteLine($"icons in {iconDir}");
        Console.WriteLine($"wrote {target}");
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Writes icons under one directory, decoding each texture once, and answers with the file
    /// name. A cooked texture is block-compressed GPU data, so the path a presentation row carries
    /// is not usable as an image until it has been through the decoder. Files are named after the
    /// texture, so an icon several entities share is written once and they all agree on it.
    /// </summary>
    public static IconWriter IconWriterInto(AbstractFileProvider provider, string directory, bool reportFailures)
    {
        var written = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        Directory.CreateDirectory(directory);

        return iconPath =>
        {
            if (written.TryGetValue(iconPath, out var cached)) return cached;

            var texture = TextureExporter.Load(provider, iconPath);
            if (texture is null)
            {
                if (reportFailures) Console.Error.WriteLine($"icon {iconPath} did not load as a texture");
                return written[iconPath] = null;
            }

            var export = TextureExporter.Export(
                texture, Path.Combine(directory, texture.Name), ETextureFormat.Png);

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
        if (entity.Type is not null) Console.WriteLine($"{indent}{entity.Type}");
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
            Console.WriteLine($"{indent}{{{token}}}{off}{own}");
            foreach (var (key, value) in placeholder.Constants ?? new Dictionary<string, object?>())
            {
                Console.WriteLine($"{indent}  {key,-58} {Show(value)}");
            }
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

        foreach (var variant in entity.Variants ?? [])
        {
            Console.WriteLine($"\n{indent}{variant.HeroName ?? variant.Hero} ({variant.Hero}): {variant.DisplayName}");
            if (variant.Grants is { } ability) Report(ability, indent + "  ");
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
