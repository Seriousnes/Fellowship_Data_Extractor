using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class EnchantRanks
{
    public static bool Is(EntityDocument document) => document is EnchantDocument;

    public static int Power(SettingsFile settings, int itemLevel) =>
        settings.Items.StatEnchantPower is { } curve
            ? (int)CurveReading.At(curve, itemLevel)
            : throw new InvalidOperationException(
                "The settings state no enchant power curve, so an enchant has no magnitude at any item level.");

    public static int RowValue(SettingsFile settings, int itemLevel) => Power(settings, itemLevel) - 1;

    public static TraitTooltipText Text(SettingsFile settings) =>
        settings.TraitTooltip
        ?? throw new InvalidOperationException("The settings state no trait tooltip text, so an enchant card has no labels.");

    public static int Level(SettingsFile settings, TooltipQuery query) => query.ItemLevel ?? settings.Items.MaxItemLevel;

    public static IReadOnlyList<ItemTypeText> Sources(SettingsFile settings, TooltipQuery query)
    {
        var types = Text(settings).ItemTypes;

        if (query.RankSources.Count == 0)
        {
            return types is [var first, ..]
                ? [first]
                : throw new InvalidOperationException("The settings name no gear slot, so an enchant has no rank source.");
        }

        return
        [
            .. query.RankSources.Select(source => types.FirstOrDefault(type =>
                    string.Equals(type.Type, source, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"'{source}' names no gear slot. The gear slots are {string.Join(", ", types.Select(type => type.Type))}.")),
        ];
    }

    public static int Rank(SettingsFile settings, TooltipQuery query) =>
        query.EquippedTier ?? Sources(settings, query).Count * Power(settings, Level(settings, query));

    public static IEnumerable<(string Stat, decimal Value)> Granted(
        EnchantDocument enchant, SettingsFile settings, int itemLevel)
    {
        var power = Power(settings, itemLevel);

        foreach (var stat in enchant.Stats ?? [])
        {
            if (stat.Coefficient == 0) continue;

            var scaled = stat.Coefficient * power;
            var value = stat.Lookup is { } lookup ? CurveReading.At(lookup, scaled) : scaled;

            yield return (StatOf(stat.Attribute, settings), (decimal)value);
        }
    }

    private static string StatOf(string attribute, SettingsFile settings) =>
        settings.StatCalculation.Attributes
            .FirstOrDefault(pair => string.Equals(pair.Value.Attribute, attribute, StringComparison.Ordinal))
            .Key
        ?? throw new InvalidOperationException(
            $"No stat in the settings reads the attribute {attribute}, so an enchant granting it has no line.");
}

public static class RankSources
{
    public static IReadOnlyList<string> Parse(string? value) =>
        value is null
            ? []
            : [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    public static string? Write(IEnumerable<string> sources) =>
        string.Join(',', sources) is { Length: > 0 } written ? written : null;
}
