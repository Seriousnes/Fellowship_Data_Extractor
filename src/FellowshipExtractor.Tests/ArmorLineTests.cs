using System.Text.Json;

using FellowshipExtractor.Api.Models;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class ArmorLineTests
{
    private const int WitchslayersPants = 5309;
    private const int DarkfrostArms = 5323;
    private const int SkandisBands = 5226;
    private const int EldrinSignet = 5220;

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

    private static decimal? Armor(int id, int itemLevel) =>
        StatMagnitudes.Armor(Item(id), Export.Loaded.Settings.StatCalculation, itemLevel);

    [Theory]
    [InlineData(WitchslayersPants, 315, 89)]
    [InlineData(DarkfrostArms, 315, 303)]
    [InlineData(SkandisBands, 360, 51)]
    public void MatchesTheArmourTheGamePrints(int id, int itemLevel, int printed) =>
        Assert.Equal(printed, Armor(id, itemLevel));

    [Fact]
    public void ASlotThatWearsNoArmourHasNone() =>
        Assert.Null(Armor(EldrinSignet, 360));

    [Fact]
    public void RisesWithItemLevel() =>
        Assert.True(Armor(DarkfrostArms, 360) > Armor(DarkfrostArms, 315));
}
