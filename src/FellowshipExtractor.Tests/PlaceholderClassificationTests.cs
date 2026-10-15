using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class PlaceholderClassificationTests
{
    private static JsonNode Token(EntityType type, int id, string key) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body["placeholders"])
            .OfType<JsonNode>()
            .SelectMany(placeholders => placeholders.AsArray())
            .OfType<JsonNode>()
            .First(placeholder => placeholder["key"]?.GetValue<string>() == key);

    [Fact]
    public void TheOakMarksItsArmorConstantAsAMultiplierAndTheOtherPerksDoNot()
    {
        Assert.True(Token(EntityType.TraitPower, 684, "percentage")["multiplies"]?.GetValue<bool>());

        Assert.Null(Token(EntityType.TraitPower, 683, "percentage")["multiplies"]);
        Assert.Null(Token(EntityType.TraitPower, 685, "percentage")["multiplies"]);
    }

    [Fact]
    public void BloodboundSpiritMarksTheSelfBuffItsTokenDeclaresMultiplicitive()
    {
        Assert.True(Token(EntityType.Ability, 2296, "BloodboundBuff")["multiplies"]?.GetValue<bool>());

        Assert.Null(Token(EntityType.Ability, 2296, "Rend")["multiplies"]);
    }

    [Fact]
    public void TheSharesThatAreNotMultipliersAreLeftUnmarked()
    {
        Assert.Null(Token(EntityType.Ability, 1964, "CleanHeal")["multiplies"]);
        Assert.Null(Token(EntityType.Ability, 1973, "CleanHeal")["multiplies"]);
        Assert.Null(Token(EntityType.Ability, 1927, "percentage")["multiplies"]);
        Assert.Null(Token(EntityType.Ability, 1616, "Damage")["multiplies"]);
    }
}
