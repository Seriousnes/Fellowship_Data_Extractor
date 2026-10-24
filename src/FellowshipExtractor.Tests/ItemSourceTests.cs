using System.Text.Json.Nodes;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class ItemSourceTests
{
    private const int SecondThresholdOnly = 4;
    private const int EquipmentVendorOnly = 320;
    private const int OutOfRotationOnly = 12;
    private const string EquipmentInventory = "fellowship/Content/Vendors/VendorInventories/VI_Equipment";
    private const int BattleRes = 5815;

    private static IEnumerable<JsonNode> Sources =>
        Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Body["sources"] is JsonArray)
            .SelectMany(document => document.Body["sources"]!.AsArray())
            .OfType<JsonNode>();

    [Fact]
    public void EveryItemThatCarriesStatsStatesWhereItComesFrom()
    {
        var carrying = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Body["stats"] is JsonArray)
            .ToArray();

        Assert.Equal(1956, carrying.Length);

        var unsourced = carrying.Where(document => document.Body["sources"] is not JsonArray).ToArray();

        Assert.Equal(1620, carrying.Length - unsourced.Length);
        Assert.Equal(SecondThresholdOnly + EquipmentVendorOnly + OutOfRotationOnly, unsourced.Length);
    }

    [Fact]
    public void EveryItemThatCarriesStatsArrivesAtAStatedItemLevel()
    {
        var carrying = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Body["stats"] is JsonArray)
            .ToArray();

        var unlevelled = carrying.Where(document => document.Body["itemLevel"] is null).ToArray();

        Assert.Equal(1632, carrying.Length - unlevelled.Length);
        Assert.Equal(SecondThresholdOnly + EquipmentVendorOnly, unlevelled.Length);
    }

    [Fact]
    public void ADungeonSourceNamesADungeonInRotation()
    {
        var rotating = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Dungeon &&
                               (document.Body["availableForCasual"] is not null ||
                                document.Body["difficulties"] is JsonArray { Count: > 0 }))
            .Select(document => document.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var named = Sources
            .Where(source => source["kind"]?.GetValue<string>() == "Dungeon" && source["dungeon"] is not null)
            .Select(source => source["name"]!.GetValue<string>())
            .Distinct()
            .ToArray();

        Assert.NotEmpty(named);
        Assert.All(named, name => Assert.Contains(name, rotating));
        Assert.DoesNotContain("Xul, The Blood Monolith", named);
    }

    [Fact]
    public void ACharmComesFromTheHeroChallengeThatGrantsIt()
    {
        var charm = Export.Loaded.Documents.First(document => document.Type == EntityType.Item && document.Id == BattleRes);

        Assert.Equal("Charm", charm.Body["slot"]?.GetValue<string>());

        var source = Assert.Single(charm.Body["sources"]!.AsArray())!;

        Assert.Equal("Challenge", source["kind"]!.GetValue<string>());
        Assert.Equal("Life Support", source["name"]!.GetValue<string>());
        Assert.Equal("Adventure Progression", source["category"]!.GetValue<string>());
    }

    [Fact]
    public void NoItemComesFromADisabledVendor()
    {
        var inventory = Export.Provider.LoadPackage(EquipmentInventory).GetExports()
            .Single(export => export.ExportType == "CRVendorInventory");

        Assert.True(PropertyWalk.Get(inventory, "IsDisabled")?.GenericValue is true);

        var vendor = PropertyWalk.GetText(inventory, "VendorName");

        Assert.Equal("Equipment Vendor", vendor);
        Assert.DoesNotContain(Sources, source => source["name"]?.GetValue<string>() == vendor);
    }
}
