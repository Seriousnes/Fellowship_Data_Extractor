using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class CreatureLocationTests
{
    [Theory]
    [InlineData("BP_NPC_VV_IcebiteReaver_C_10", "BP_NPC_VV_IcebiteReaver")]
    [InlineData("BP_NPC_VV_BlackburnSlaver_C_6", "BP_NPC_VV_BlackburnSlaver")]
    [InlineData("BP_NPC_VV_IcebiteReaver_C", null)]
    [InlineData("PersistentLevel", null)]
    [InlineData("CharacterMaterialHandler", null)]
    [InlineData("NODE_AddArrowComponent_0", null)]
    public void AnObjectNameStripsItsInstanceNumberAndBlueprintSuffix(string objectName, string? expected) =>
        Assert.Equal(expected, CreatureLocations.PlacedFrom(objectName));

    [Fact]
    public void TheIcebiteReaverIsPlacedInRansackOfDrakheim()
    {
        var placed = Export.Resolver.Locations.For("BP_NPC_VV_IcebiteReaver");

        var named = placed.Select(tag => Export.Resolver.Dungeons.For(tag)).ToArray();

        Assert.Contains("Ransack of Drakheim", named);
    }

    [Fact]
    public void EveryDungeonWithAPersistentMapWasRead()
    {
        var locations = Export.Resolver.Locations;

        Assert.True(locations.Dungeons > 0, "No dungeon resolved a persistent map.");
        Assert.True(locations.Maps >= locations.Dungeons, "Fewer umaps were read than there are dungeons.");
        Assert.True(locations.Placed > 0, "No creature was placed in any dungeon.");
    }

    private static readonly Dictionary<string, string> Folders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["FrozenKingdom"] = "fellowship/Content/dungeons/FrozenKingdom/Sublevels/",
        ["PirateRaid"] = "fellowship/Content/dungeons/PirateRaid/Sublevels/",
        ["SnowPeak"] = "fellowship/Plugins/GameFeatures/Adventures/SnowPeak/Content/Levels/",
    };

    [Fact]
    public void OnlyTheFolderSegmentASingleDungeonSitsUnderNamesThatDungeon()
    {
        var naming = CreatureLocations.TagsByFolder(Folders);

        Assert.Equal("FrozenKingdom", naming["FrozenKingdom"]);
        Assert.Equal("SnowPeak", naming["SnowPeak"]);

        Assert.DoesNotContain("fellowship", naming.Keys);
        Assert.DoesNotContain("Content", naming.Keys);
        Assert.DoesNotContain("dungeons", naming.Keys);
        Assert.DoesNotContain("Sublevels", naming.Keys);
    }

    [Theory]
    [InlineData("fellowship/Content/Characters/NPC/FrozenKingdom/BossRangerCaptain/BP_X.uasset", "FrozenKingdom")]
    [InlineData("fellowship/Content/Characters/NPC/SnowPeak/Ancient/BP_Y.uasset", "SnowPeak")]
    [InlineData("fellowship/Content/Characters/NPC/Shared/BP_Z.uasset", null)]
    public void ACreatureIsHousedByTheDungeonFolderItsOwnAssetLivesUnder(string path, string? expected) =>
        Assert.Equal(expected, CreatureLocations.DungeonOf(path, CreatureLocations.TagsByFolder(Folders)));

    [Fact]
    public void ABossNoMapPlacesIsStillHousedByTheDungeonItBelongsTo()
    {
        var placed = Export.Resolver.Locations.For("BP_NPC_FK_BossRangerCaptain");

        var named = placed.Select(tag => Export.Resolver.Dungeons.For(tag)).ToArray();

        Assert.Contains("Cithrel's Fall", named);
    }
}
