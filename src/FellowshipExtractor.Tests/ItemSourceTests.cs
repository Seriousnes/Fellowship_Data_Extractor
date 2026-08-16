using System.Text.Json.Nodes;

using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class ItemSourceTests
{
    private const string DefinitionClass = "CRDynamicHeroItemDefinition";
    private const string SpecificDungeonsField = "DropsInSpecificDungeons";
    private const string LegendaryDroptable = "SDT_HeroPath_LegendaryItem";
    private const int SecondThresholdOnly = 4;

    private static readonly Lazy<string[]> Referenced = new(ReadReferencedDungeons, isThreadSafe: true);

    private static string[] ReadReferencedDungeons()
    {
        var provider = Export.Provider;
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in AssetRegistry.ReadAssets(provider))
        {
            if (!asset.AssetClass.Text.Equals(DefinitionClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (DungeonNames.Load(provider, asset.ObjectPath) is not { } definition) continue;
            if (PropertyWalk.Get(definition, SpecificDungeonsField)?.GenericValue is not UScriptArray listed) continue;

            foreach (var entry in listed.Properties)
            {
                if (PropertyWalk.AsString(entry?.GenericValue) is { } tag) named.Add(tag);
            }
        }

        return [.. named.Order(StringComparer.Ordinal)];
    }

    [Fact]
    public void EveryDungeonADefinitionDropsInIsNamed()
    {
        var dungeons = Export.Resolver.Dungeons;
        var unnamed = Referenced.Value.Where(tag => dungeons.For(tag) is null).ToArray();

        Assert.Equal(15, Referenced.Value.Length);
        Assert.True(
            unnamed.Length == 0,
            $"{unnamed.Length} of {Referenced.Value.Length} dungeons a definition names have no display name, " +
            $"so an item would state it drops nowhere readable: {string.Join(", ", unnamed)}");
    }

    [Fact]
    public void TheThreeVendorTiersEachGrantOneItemLevel()
    {
        var byTier = Export.Resolver.Sources.Grants
            .Where(grant => grant.Kind == SourceKind.Vendor)
            .SelectMany(grant => grant.Tags.Select(tag => (Tag: tag, grant.ItemLevel)))
            .GroupBy(entry => entry.Tag, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.ItemLevel).Distinct().ToArray());

        Assert.Equal(3, byTier.Count);
        Assert.Equal([15], byTier["VendorGear"]);
        Assert.Equal([75], byTier["VendorGear.League1"]);
        Assert.Equal([135], byTier["VendorGear.League2"]);
    }

    [Fact]
    public void TheVendorTagsSplitTheTaggedRowsIntoOneHundredAndFourEach()
    {
        var counted = Export.Resolver.HeroItems.Rows.Values
            .Where(row => row.Tags.Count > 0)
            .ToArray();

        Assert.Equal(324, counted.Length);

        foreach (var tier in (string[])["VendorGear", "VendorGear.League1", "VendorGear.League2"])
        {
            Assert.Equal(
                104,
                counted.Count(row => row.Tags.Count == 1 && row.Tags[0].Equals(tier, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void EveryVendorRowStatesTheVendorThatSellsIt()
    {
        var sold = Export.Resolver.HeroItems.Rows
            .Where(row => row.Value.Tags.Count > 0)
            .Select(row => Export.Resolver.Sources.Fixed(row.Key, out _))
            .ToArray();

        var unsourced = sold.Count(sources => sources.All(source => source.Kind != SourceKind.Vendor));

        Assert.Equal(4, unsourced);
        Assert.All(
            sold.Where(sources => sources.Any(source => source.Kind == SourceKind.Vendor)),
            sources => Assert.Contains(sources, source => source.Name == "Equipment Vendor"));
    }

    [Fact]
    public void AHeroStartingOutfitArrivesAtItemLevelOne()
    {
        var outfits = Export.Resolver.HeroItems.Rows
            .Where(row => row.Value.DefaultItemLevel is not null)
            .ToArray();

        Assert.Equal(66, outfits.Length);
        Assert.All(outfits, row => Assert.Equal(1, row.Value.DefaultItemLevel));

        foreach (var (tag, _) in outfits)
        {
            var sources = Export.Resolver.Sources.Fixed(tag, out var level);

            Assert.Equal(1, level);
            Assert.Contains(sources, source => source.Kind == SourceKind.Character);
        }
    }

    [Fact]
    public void TheLegendaryStarmapGrantStatesItemLevelThreeHundredAndFifteen()
    {
        var granted = Export.Resolver.Sources.Granting(LegendaryDroptable);

        Assert.NotNull(granted);
        Assert.Equal(315, granted.ItemLevel);
        Assert.Equal(SourceKind.Starmap, granted.Kind);
    }

    [Fact]
    public void ADroppedItemToppsOutAtThreeHundredAndFifteenAndALegendaryAtThreeHundredAndSixty()
    {
        Assert.Equal(315, Export.Resolver.Sources.DropLevel(legendary: false));
        Assert.Equal(360, Export.Resolver.Sources.DropLevel(legendary: true));
    }

    [Fact]
    public void EveryItemThatCarriesStatsStatesWhereItComesFrom()
    {
        var carrying = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Body["stats"] is JsonArray)
            .ToArray();

        Assert.Equal(2528, carrying.Length);

        var unsourced = carrying.Where(document => document.Body["sources"] is not JsonArray).ToArray();

        Assert.Equal(2524, carrying.Length - unsourced.Length);
        Assert.Equal(SecondThresholdOnly, unsourced.Length);
    }

    [Fact]
    public void TheOnlyRowsNoDroptableSellsAreTheSecondThresholdOnes()
    {
        var stranded = Export.Resolver.HeroItems.Rows
            .Where(row => row.Value.Tags.Count > 0)
            .Where(row => Export.Resolver.Sources.Fixed(row.Key, out _).Count == 0)
            .ToArray();

        Assert.Equal(SecondThresholdOnly, stranded.Length);
        Assert.All(stranded, row => Assert.Equal(["VendorGear.SecondThreshold"], row.Value.Tags));
    }

    [Fact]
    public void EveryItemThatCarriesStatsArrivesAtAStatedItemLevel()
    {
        var carrying = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Body["stats"] is JsonArray)
            .ToArray();

        var unlevelled = carrying.Where(document => document.Body["itemLevel"] is null).ToArray();

        Assert.Equal(2524, carrying.Length - unlevelled.Length);
        Assert.Equal(SecondThresholdOnly, unlevelled.Length);
    }
}
