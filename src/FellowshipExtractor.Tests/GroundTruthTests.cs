using System.Text.Json;

using Fellowship.Codex.Models;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class GroundTruthTests
{
    private static readonly JsonSerializerOptions Reading = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static ItemDocument Item(int id)
    {
        var readings = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Id == id)
            .Select(document => document.Body)
            .ToArray();

        var body = readings.FirstOrDefault(reading => reading["hero"]?.GetValue<string>() == "Rime")
            ?? readings.FirstOrDefault()
            ?? throw new InvalidOperationException($"Item {id} is not in the export.");

        return new ItemDocument
        {
            Id = id,
            Slot = body["slot"]?.GetValue<string>(),
            ArmorType = body["armorType"]?.GetValue<string>() is { } armor
                ? Enum.Parse<Fellowship.SDK.ArmorType>(armor)
                : null,
            Stats = body["stats"].Deserialize<IReadOnlyList<StatSlot>>(Reading),
        };
    }

    private static IReadOnlyList<StatLine> Tempered(int id, int itemLevel, int tempers, params string[] rolled) =>
        StatMagnitudes.For(
            Item(id),
            Export.Loaded.Settings.StatCalculation,
            new HashSet<string>(["Strength", "Intellect", "Agility"], StringComparer.OrdinalIgnoreCase),
            rarity: null,
            itemLevel,
            new TooltipQuery { RandomStats = rolled, Upgrades = tempers });

    private static IReadOnlyList<StatLine> Lines(int id, int itemLevel, params string[] rolled) =>
        StatMagnitudes.For(
            Item(id),
            Export.Loaded.Settings.StatCalculation,
            new HashSet<string>(["Strength", "Intellect", "Agility"], StringComparer.OrdinalIgnoreCase),
            rarity: null,
            itemLevel,
            new TooltipQuery { RandomStats = rolled });

    private static void Reads(IReadOnlyList<StatLine> lines, params (string Stat, decimal Printed)[] printed)
    {
        foreach (var (stat, value) in printed)
        {
            var line = lines.SingleOrDefault(l => string.Equals(l.Stat, stat, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"No {stat} line. Drew {string.Join(", ", lines.Select(l => $"{l.Stat} {l.Value}"))}.");

            Assert.Equal(value, line.Value);
        }
    }

    [Fact]
    public void DrakesbloodTapestryAt360() =>
        Reads(
            Lines(5218, 360, "Expertise", "Spirit"),
            ("Armor", 48), ("Stamina", 15), ("Intellect", 5), ("Haste", 9), ("Expertise", 7), ("Spirit", 7));

    [Fact]
    public void SkandisBandsAt360() =>
        Reads(
            Lines(5226, 360, "CritChance", "Expertise"),
            ("Armor", 51), ("Stamina", 15), ("Intellect", 5), ("Haste", 9), ("CritChance", 7), ("Expertise", 7));

    [Fact]
    public void EldrinSignetFirstDropAt360() =>
        Reads(
            Lines(5220, 360, "Intellect", "Haste"),
            ("Stamina", 23), ("CritChance", 8), ("Expertise", 8), ("Intellect", 3), ("Haste", 8));

    [Fact]
    public void EldrinSignetSecondDropAt360() =>
        Reads(Lines(5220, 360, "Haste", "Haste"), ("Stamina", 23), ("CritChance", 8), ("Expertise", 8), ("Haste", 16));

    [Fact]
    public void EldrinSignetThirdDropAt360() =>
        Reads(Lines(5220, 360, "Expertise", "Haste"), ("Stamina", 23), ("CritChance", 8), ("Expertise", 16), ("Haste", 8));

    [Fact]
    public void DrakesbloodTapestryAt315() =>
        Reads(
            Lines(5218, 315, "Stamina", "Stamina"),
            ("Armor", 43), ("Stamina", 27), ("Intellect", 4), ("Haste", 9));

    [Fact]
    public void WitchslayersPantsUntemperedAt315() =>
        Reads(
            Lines(5309, 315, "Stamina", "Stamina", "Spirit"),
            ("Armor", 89), ("Stamina", 66), ("Intellect", 6), ("Expertise", 11), ("Spirit", 12));

    [Fact]
    public void BanefulWebShroudRollsAt285() =>
        Reads(
            Lines(5139, 285, "CritChance", "Expertise"),
            ("Armor", 40), ("CritChance", 6), ("Expertise", 6));

    [Fact]
    public void BetrayersIcyVeilArmourAt195() => Reads(Lines(5135, 195), ("Armor", 30));

    [Fact]
    public void DarkfrostArmsArmourAt315() => Reads(Lines(5323, 315), ("Armor", 303));

    [Fact]
    public void BetrayersIcyVeilTemperedThreeAt195() =>
        Reads(
            Tempered(5135, 195, 3, "Intellect"),
            ("Armor", 30), ("Stamina", 10), ("Intellect", 7), ("CritChance", 8));

    [Fact]
    public void BanefulWebShroudTemperedFourAt285() =>
        Reads(
            Tempered(5139, 285, 4, "CritChance", "Expertise"),
            ("Armor", 40), ("Stamina", 18), ("Intellect", 7), ("Haste", 10),
            ("CritChance", 6), ("Expertise", 6));

    [Fact]
    public void WitchslayersPantsTemperedEightAt315() =>
        Reads(
            Tempered(5309, 315, 8, "Spirit", "Spirit"),
            ("Armor", 89), ("Stamina", 50), ("Intellect", 14), ("Expertise", 19), ("Spirit", 24));

    [Fact]
    public void DarkfrostArmsTemperedEightAt315() =>
        Reads(
            Tempered(5323, 315, 8, "Spirit", "Spirit"),
            ("Armor", 303), ("Stamina", 36), ("Strength", 17), ("Haste", 27), ("Spirit", 28));

    [Fact]
    public void SinbindingStoneTemperedEightAt315() =>
        Reads(
            Tempered(5250, 315, 8, "Haste", "Haste"),
            ("Intellect", 14), ("CritChance", 15), ("Haste", 31));
}
