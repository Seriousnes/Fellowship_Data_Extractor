using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Models;

public static class Enchants
{
    public static bool Is(EntityDocument document) => document is TraitDocument { Stats: { Count: > 0 } };

    public static int Power(SettingsFile settings, int itemLevel) =>
        settings.Items.StatEnchantPower is { } curve
            ? (int)CurveReading.At(curve, itemLevel)
            : throw new InvalidOperationException(
                "The settings state no enchant power curve, so an enchant has no magnitude at any item level.");

    public static int RowValue(SettingsFile settings, int itemLevel) => Power(settings, itemLevel) - 1;

    public static IEnumerable<(string Stat, decimal Value)> Granted(
        TraitDocument enchant, SettingsFile settings, int itemLevel)
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
