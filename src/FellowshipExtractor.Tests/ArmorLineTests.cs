using System.Text.Json;

using Fellowship.Codex.Models;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class ArmorLineTests
{
    private const int SahrilsArms = 5981;
    private const int RubelliteRing = 5930;

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

    [Fact]
    public void ASlotThatWearsNoArmourHasNone() =>
        Assert.Null(Armor(RubelliteRing, 360));

    [Fact]
    public void RisesWithItemLevel() =>
        Assert.True(Armor(SahrilsArms, 360) > Armor(SahrilsArms, 315));
}
