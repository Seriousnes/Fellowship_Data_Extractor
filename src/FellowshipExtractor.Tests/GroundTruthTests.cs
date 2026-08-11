using System.Text.Json;

using FellowshipExtractor.Api.Models;

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
        var body = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
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
            Lines(5218, 315, "Stamina"),
            ("Armor", 43), ("Stamina", 27), ("Intellect", 4), ("Haste", 9));

    [Fact]
    public void WitchslayersPantsUntemperedAt315() =>
        Reads(
            Lines(5309, 315, "Stamina", "Spirit"),
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
}
