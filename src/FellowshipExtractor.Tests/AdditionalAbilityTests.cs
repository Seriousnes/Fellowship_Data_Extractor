using System.Text.Json.Nodes;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

public sealed class AdditionalAbilityTests
{
    private static JsonObject Ability(int id) =>
        Export.Loaded.Documents.Single(document => document.Type == EntityType.Ability && document.Id == id).Body;

    [Theory]
    [InlineData(2227)]
    [InlineData(2308)]
    [InlineData(2309)]
    [InlineData(2310)]
    public void AnAdditionalAbilityHasNoDocumentOfItsOwn(int additional) =>
        Assert.DoesNotContain(
            Export.Loaded.Documents,
            document => document.Type == EntityType.Ability && document.Id == additional);

    [Fact]
    public void TheListingAbilityCarriesTheTargetCountFalloffOfItsAdditionalAbility()
    {
        var falloff = Assert.Single((Ability(1018)["targetCountFalloff"] as JsonArray)!);

        Assert.Equal(12, (int?)falloff!["threshold"]);
    }

    [Theory]
    [InlineData(1129, "BackstabDmg")]
    [InlineData(1213, "SkitterDmg")]
    public void TheListingAbilityResolvesTheTokenItsDescriptionPrints(int listing, string token)
    {
        var placeholders = Ability(listing)["placeholders"] as JsonArray;

        Assert.Contains(placeholders!.OfType<JsonNode>(), placeholder => (string?)placeholder["key"] == token);
    }

    [Fact]
    public void AReferenceToAnAdditionalAbilityNamesTheListingAbility()
    {
        var effect = Export.Loaded.Documents.Single(document => document.Type == EntityType.Effect && document.Id == 1380).Body;

        Assert.Equal(1018, (int?)effect["nameBorrowedFrom"]!["id"]);
    }
}
