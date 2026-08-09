using System.Globalization;
using System.Text.Json;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Api.Models;

public static class CodexView
{
    internal const string Absent = "not in the build";

    private const double Floor = 0.16;

    public static string? Readable(string? colour)
    {
        if (colour is null || colour.Length != 7 || colour[0] != '#') return colour;

        if (!int.TryParse(colour.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return colour;
        }

        double r = (packed >> 16) & 0xFF, g = (packed >> 8) & 0xFF, b = packed & 0xFF;

        for (var mix = 0; mix <= 10; mix++)
        {
            var k = mix / 10d;
            double lr = r + (255 - r) * k, lg = g + (255 - g) * k, lb = b + (255 - b) * k;

            if (Luminance(lr, lg, lb) >= Floor || mix == 10)
            {
                return $"#{(int)Math.Round(lr):X2}{(int)Math.Round(lg):X2}{(int)Math.Round(lb):X2}";
            }
        }

        return colour;
    }

    private static double Luminance(double r, double g, double b) =>
        0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);

    private static double Channel(double value)
    {
        var scaled = value / 255d;
        return scaled <= 0.03928 ? scaled / 12.92 : Math.Pow((scaled + 0.055) / 1.055, 2.4);
    }

    public static IReadOnlyList<Placeholder> PlaceholdersOf(EntityDocument document) => document switch
    {
        AbilityDocument ability => ability.Placeholders ?? [],
        BlessingDocument blessing => blessing.Placeholders ?? [],
        EffectDocument effect => effect.Placeholders ?? [],
        GemTraitDocument trait => trait.Placeholders ?? [],
        LegendaryPowerDocument legendary => legendary.Placeholders ?? [],
        TalentDocument talent => talent.Placeholders ?? [],
        TraitPowerDocument power => power.Placeholders ?? [],
        WeaponPowerDocument power => power.Placeholders ?? [],
        _ => [],
    };

    public static string Number(double value) => Codex.Number(value);

    public static string Constant(object? value) => value switch
    {
        null => Absent,
        JsonElement { ValueKind: JsonValueKind.Array } array =>
            string.Join(" / ", array.EnumerateArray().Select(item => Constant(item))),
        JsonElement { ValueKind: JsonValueKind.Number } number => Number(number.GetDouble()),
        JsonElement { ValueKind: JsonValueKind.Null } => Absent,
        JsonElement element => element.ToString(),
        double number => Number(number),
        _ => value.ToString() ?? string.Empty,
    };
}

public sealed record TuningRow(string Label, string Value, bool Absent);

public sealed record TokenReading(
    int Ordinal, string Reading, bool Resolved, string Working, IReadOnlyList<TuningRow> Tuning);
