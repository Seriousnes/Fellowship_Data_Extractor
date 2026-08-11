using FellowshipExtractor.Tooltip;

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
}
