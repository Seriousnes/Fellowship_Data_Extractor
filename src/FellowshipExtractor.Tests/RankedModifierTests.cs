using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>
/// A sigil or glyph effect's modifier reading a ranked constants row has the value at every rank, as its description
/// prints them.
/// </summary>
public sealed class RankedModifierTests
{
    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"{type} {id} is not in the export.");

    [Theory]
    [InlineData(2112, "HasteRating", new[] { 6d, 10, 13, 17 })]
    [InlineData(3184, "Haste", new[] { 0.06, 0.1, 0.16, 0.25 })]
    public void ModifierHasEveryRank(int effect, string attribute, double[] ranks)
    {
        var modifier = Assert.Single(
            Document(EntityType.Effect, effect)["modifiers"]!.AsArray().OfType<JsonObject>(),
            modifier => modifier["attribute"]!.GetValue<string>() == attribute);

        Assert.Equal("Additive", modifier["op"]!.GetValue<string>());
        Assert.Equal(ranks[0], modifier["value"]!.GetValue<double>());
        Assert.Equal(ranks, modifier["ranks"]!.AsArray().Select(rank => rank!.GetValue<double>()));
    }

    [Fact]
    public void InspiredAllegianceGrantsTheRankedEffect()
    {
        var granted = Assert.Single(Document(EntityType.Sigil, 2)["grants"]!.AsArray())!["id"]!.GetValue<int>();

        Assert.Equal(granted, Document(EntityType.Effect, 2112)["appliedBy"]!["id"]!.GetValue<int>());
    }

    [Fact]
    public void TheWayfarerHasteIsTheRankedEffect()
    {
        var owner = Document(EntityType.Glyph, 703)["tuning"]!["Buff.AdditionalHaste"]!["owner"]!;

        Assert.Equal(3184, owner["id"]!.GetValue<int>());
    }
}
