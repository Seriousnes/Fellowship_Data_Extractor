using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>A <c>DT_ItemTraits</c> row with no category has an item trait document, and the generator never rolls it.</summary>
public sealed class ItemTraitTests
{
    private static IEnumerable<(EntityType Type, int Id, string? Name, JsonObject Body)> Traits =>
        Export.Loaded.Documents.Where(document => document.Type == EntityType.ItemTrait);

    private static JsonObject Trait(int id) => Traits.Single(trait => trait.Id == id).Body;

    private static JsonObject Ability(int id) =>
        Export.Loaded.Documents.Single(document => document.Type == EntityType.Ability && document.Id == id).Body;

    [Theory]
    [InlineData(3, "Inspired Arms", 173)]
    [InlineData(30, "Topaz Winds", 171)]
    [InlineData(43, "Navigator's Intuition", 1491)]
    public void ATraitNamesItselfAndGrantsItsAbility(int id, string name, int ability)
    {
        var trait = Trait(id);

        Assert.Equal(name, trait["name"]!.GetValue<string>());
        Assert.False(trait["rollable"]!.GetValue<bool>());
        Assert.Equal(ability, Assert.Single(trait["grants"]!.AsArray())!["id"]!.GetValue<int>());
    }

    [Fact]
    public void TheFourRowsWithNoCategoryAreTheItemTraits() =>
        Assert.Equal([3, 30, 43, 50], Traits.Select(trait => trait.Id).Order());

    [Theory]
    [InlineData(386, 3)]
    [InlineData(380, 30)]
    public void TheAugmentationFoldsIntoItsRow(int augmentation, int row)
    {
        Assert.DoesNotContain(Traits, trait => trait.Id == augmentation);
        Assert.NotNull(Trait(row)["description"]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(30)]
    public void ATraitHasItsRankRuns(int id) => Assert.NotEmpty(Trait(id)["rankRuns"]!.AsArray());

    [Theory]
    [InlineData(171, 30)]
    [InlineData(173, 3)]
    public void AGrantedAbilityOriginatesInItsTrait(int ability, int trait)
    {
        var origin = Assert.Single(Ability(ability)["origins"]!.AsArray())!;

        Assert.Equal("item-trait", origin["type"]!.GetValue<string>());
        Assert.Equal(trait, origin["id"]!.GetValue<int>());
    }

    [Fact]
    public void InspiredArmsBorrowsItsNameForTheAbilityItGrants()
    {
        var ability = Ability(173);

        Assert.Equal("Inspired Arms", ability["name"]!.GetValue<string>());
        Assert.Equal("item-trait", ability["nameBorrowedFrom"]!["type"]!.GetValue<string>());
        Assert.Equal(3, ability["nameBorrowedFrom"]!["id"]!.GetValue<int>());
    }
}
