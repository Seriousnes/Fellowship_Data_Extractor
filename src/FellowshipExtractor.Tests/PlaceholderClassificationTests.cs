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
        Assert.True(Token(EntityType.ProtectiveEnchant, 62, "percentage")["multiplies"]?.GetValue<bool>());

        Assert.Null(Token(EntityType.ProtectiveEnchant, 54, "percentage")["multiplies"]);
        Assert.Null(Token(EntityType.ProtectiveEnchant, 56, "percentage")["multiplies"]);
    }

    [Fact]
    public void BloodboundSpiritMarksTheSelfBuffItsTokenDeclaresMultiplicitive()
    {
        Assert.True(Token(EntityType.Ability, 2296, "BloodboundBuff")["multiplies"]?.GetValue<bool>());

        Assert.Null(Token(EntityType.Ability, 2296, "Rend")["multiplies"]);
    }

    [Fact]
    public void ShieldSlamsMaximumToughnessIsHelenasToughnessCalculationOverStrength()
    {
        var toughness = Token(EntityType.Ability, 977, "MaxToughness");

        Assert.Equal("Strength", toughness["attribute"]?.GetValue<string>());
        Assert.Equal(7.7, toughness["coefficient"]?.GetValue<double>());
        Assert.Null(toughness["spread"]);

        var terms = toughness["bracket"]!["terms"]!.AsArray()
            .Select(term => (term!["attribute"]!.GetValue<string>(), term["coefficient"]!.GetValue<double>()))
            .ToArray();

        Assert.Equal([("CritChance", 2), ("Expertise", 2), ("Haste", 2), ("Spirit", 2)], terms);
        Assert.Equal(-1, toughness["bracket"]!["addend"]!.GetValue<double>());
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
