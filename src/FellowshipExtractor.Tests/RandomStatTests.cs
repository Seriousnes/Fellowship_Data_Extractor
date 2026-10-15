using System.Text.Json;

using Fellowship.Codex.Models;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class RandomStatTests
{
    private const int EldrinSignet = 5220;
    private const int SkandisBands = 5226;
    private const int WitchslayersPants = 5309;

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

    private static StatLine Line(IReadOnlyList<StatLine> lines, string stat) =>
        lines.Single(line => string.Equals(line.Stat, stat, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void TwoRollsOnHasteReadTheSixteenTheEldrinSignetPrints() =>
        Assert.Equal(16m, Line(Lines(EldrinSignet, 360, "Haste", "Haste"), "Haste").Value);

    [Fact]
    public void SkandisBandsRollsReadTheSevensItPrints()
    {
        var lines = Lines(SkandisBands, 360, "CritChance", "Expertise");

        Assert.Equal(7m, Line(lines, "CritChance").Value);
        Assert.Equal(7m, Line(lines, "Expertise").Value);
    }

    [Fact]
    public void ARollOnADeclaredStatAggregatesIntoTheOneLine()
    {
        var lines = Lines(EldrinSignet, 360, "Expertise");

        var expertise = Line(lines, "Expertise");

        Assert.Equal(1, lines.Count(line => string.Equals(line.Stat, "Expertise", StringComparison.OrdinalIgnoreCase)));
        Assert.True(expertise.Rolled);
        Assert.True(expertise.Guaranteed);
        Assert.True(expertise.Value > Line(Lines(EldrinSignet, 360), "Expertise").Value);
    }

    [Fact]
    public void ARollOnAStatTheItemDoesNotDeclareReadsAsRolledOnly()
    {
        var haste = Line(Lines(EldrinSignet, 360, "Haste"), "Haste");

        Assert.True(haste.Rolled);
        Assert.False(haste.Guaranteed);
    }

    [Fact]
    public void EveryNamedRollIsDrawnEvenPastTheSlotsTheRungGuarantees() =>
        Assert.Equal(3, Lines(WitchslayersPants, 315, "Stamina", "Spirit", "Haste")
            .Count(line => line.Rolled));

    [Fact]
    public void ARollOnStaminaOutweighsARollOnARating()
    {
        var lines = Lines(SkandisBands, 360, "Stamina", "Spirit");

        Assert.True(Line(lines, "Stamina").Rolled);
        Assert.True(Line(lines, "Stamina").Value > 3 * Line(lines, "Spirit").Value);
    }

    [Fact]
    public void NamingNothingLeavesTheSlotsTheRungGuarantees() =>
        Assert.DoesNotContain(Lines(EldrinSignet, 360), line => line.Rolled);
}
