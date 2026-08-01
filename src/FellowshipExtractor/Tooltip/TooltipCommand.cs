using System.Text.Json;
using System.Text.Json.Serialization;
using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse_Conversion.Textures;
using FellowshipExtractor.Textures;

namespace FellowshipExtractor.Tooltip;

/// <summary>
/// Reconstructs everything the game shows for one named entity and writes it out with the icon
/// decoded to a real image. The resolution itself lives in <see cref="TooltipResolver"/> so the
/// bulk export runs the same code over every tag.
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

        Report(record);
        Console.WriteLine($"\nwrote {target}");
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Writes icons under one directory, decoding each texture once. A cooked texture is
    /// block-compressed GPU data, so the path a presentation row carries is not usable as an image
    /// until it has been through the decoder.
    /// </summary>
    public static IconWriter IconWriterInto(
        AbstractFileProvider provider, string directory, bool reportFailures, bool prefixWithHint = true)
    {
        var written = new Dictionary<string, TextureExport?>(StringComparer.OrdinalIgnoreCase);

        return (iconPath, nameHint) =>
        {
            if (written.TryGetValue(iconPath, out var cached)) return cached;

            var texture = TextureExporter.Load(provider, iconPath);
            if (texture is null)
            {
                if (reportFailures) Console.Error.WriteLine($"icon {iconPath} did not load as a texture");
                return written[iconPath] = null;
            }

            var target = Path.Combine(directory, prefixWithHint ? $"{nameHint}-{texture.Name}" : texture.Name);
            var export = TextureExporter.Export(texture, target, ETextureFormat.Png);

            if (!export.Written)
            {
                if (reportFailures) Console.Error.WriteLine($"icon {iconPath} did not decode: {export.Error}");
                return written[iconPath] = null;
            }

            return written[iconPath] = export;
        };
    }

    private static void Report(TooltipRecord record)
    {
        Console.WriteLine();
        Console.WriteLine($"  {record.DisplayName?.ToUpperInvariant() ?? record.Name}");
        if (record.ItemType is not null) Console.WriteLine($"  {record.ItemType}");

        if (record.Slots is { } slots)
        {
            Console.WriteLine($"\n  {slots.Count} stat slot(s):");
            foreach (var slot in slots)
            {
                var stats = slot.StatPool is null ? "hero relative" : string.Join(", ", slot.StatPool);
                Console.WriteLine($"    {slot.Slot,-28} {stats}");
            }
        }

        foreach (var ability in record.Abilities ?? [])
        {
            Console.WriteLine($"\n  grants: {ability.DisplayName ?? ability.Asset}");
        }

        if (record.GrantedAbility is { } granted) ReportAugmentation(granted, "  ");

        if (record.Variants is { } variants)
        {
            Console.WriteLine($"\n  {variants.Count} hero variant(s):");
            foreach (var variant in variants)
            {
                Console.WriteLine($"    {variant.HeroName ?? variant.Hero} ({variant.Hero})");
                foreach (var (field, value) in variant.Fields) Console.WriteLine($"      {field}: {value}");
                if (variant.GrantedAbility is { } ability) ReportAugmentation(ability, "      ");
            }
        }

        if (record.LegendaryStats is { } stats2)
        {
            Console.WriteLine($"\n  {stats2.Count} legendary stat(s):");
            foreach (var stat in stats2)
            {
                Console.WriteLine($"    {stat.EffectiveName,-24} {stat.EffectiveDescription}");
            }
        }

        if (record.Headline is { } headline)
        {
            foreach (var (field, value) in headline) Console.WriteLine($"    {field,-16} {value}");
        }

        if (record.Description is { } description) Console.WriteLine($"\n  {description}");

        if (record.CapturedAttributes is { } captures)
        {
            Console.WriteLine($"\n  scales off: {string.Join(", ", captures)}");
        }

        if (record.Effects is { } effects)
        {
            Console.WriteLine($"\n  {effects.Count} applied effect(s):");
            foreach (var effect in effects) ReportEffect(effect, "    ");
        }

        foreach (var token in record.DescriptionTokens ?? [])
        {
            Console.WriteLine($"\n  {{{token.TokenName}}}");
            foreach (var binding in token.Constants ?? [])
            {
                Console.WriteLine($"    {binding.RowName,-64} {binding.Value?.ToString() ?? "unresolved"}");
            }
        }

        if (record.Icon is { } icon) Console.WriteLine($"\n  icon: {icon}");
        if (record.IconImage is { } image)
        {
            Console.WriteLine($"        {image.File}  ({image.Width}x{image.Height}, {image.PixelFormat})");
        }

        if (record.Constants is { } constants)
        {
            Console.WriteLine($"\n  {constants.Count} curve binding(s):");
            foreach (var binding in constants)
            {
                Console.WriteLine($"    {binding.Property,-40} {binding.RowName,-58} {binding.Value?.ToString() ?? "unresolved"}");
            }
        }
    }

    private static void ReportEffect(EffectRef effect, string indent)
    {
        var scales = effect.CapturedAttributes is { } attributes ? $"  scales off {string.Join(", ", attributes)}" : "";
        Console.WriteLine($"{indent}{effect.Property,-32} {AbilityPresentationIndex.AssetName(effect.Object)}{scales}");

        foreach (var binding in effect.Constants ?? [])
        {
            Console.WriteLine($"{indent}  {binding.RowName,-64} {binding.Value?.ToString() ?? "unresolved"}");
        }

        if (effect.Unresolved is { } reason) Console.WriteLine($"{indent}  {reason}");
    }

    private static void ReportAugmentation(Augmentation augmentation, string indent)
    {
        Console.WriteLine($"\n{indent}Ability: {augmentation.EffectiveName ?? augmentation.Asset}");
        foreach (var line in (augmentation.EffectiveDescription ?? "").Split('\n'))
        {
            if (line.Trim().Length > 0) Console.WriteLine($"{indent}  {line.TrimEnd('\r')}");
        }

        foreach (var ability in augmentation.Abilities ?? [])
        {
            foreach (var binding in ability.Constants ?? [])
            {
                Console.WriteLine($"{indent}  {binding.Property,-32} {binding.RowName,-58} {binding.Value?.ToString() ?? "unresolved"}");
            }
        }
    }

    internal static string? TagValue(FAssetData asset, string key) =>
        asset.TagsAndValues.FirstOrDefault(kv => kv.Key.Text.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;
}
