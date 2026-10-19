using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class CreatureLocationTests
{
    [Theory]
    [InlineData("BP_NPC_DR_BloodfangMauler_C", "BP_NPC_DR_BloodfangMauler")]
    [InlineData("BP_NPC_VV_IcebiteReaver_C", "BP_NPC_VV_IcebiteReaver")]
    [InlineData("CapsuleComponent", null)]
    [InlineData("LevelStreamingAlwaysLoaded", null)]
    public void APlacedCreatureIsNamedByTheBlueprintClassOfItsActor(string className, string? expected) =>
        Assert.Equal(expected, DungeonLayouts.CreatureOf(className));

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
}
