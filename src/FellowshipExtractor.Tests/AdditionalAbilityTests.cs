using System.Text.Json.Nodes;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

public sealed class AdditionalAbilityTests
{
    private static JsonObject Ability(int id) =>
        Export.Loaded.Documents.Single(document => document.Type == EntityType.Ability && document.Id == id).Body;

    [Theory]
    [InlineData(2308, 1129, "Backstab", "Mara_backstab")]
    [InlineData(2309, 1213, "Skittering Blades", "Mara_AOEBuilder")]
    public void AnAdditionalAbilityHasTheNameIconAndDescriptionOfTheRowListingIt(
        int additional, int listing, string name, string icon)
    {
        var body = Ability(additional);

        Assert.Equal(name, (string?)body["name"]);
        Assert.Equal(icon, (string?)body["icon"]);
        Assert.Equal((string?)Ability(listing)["description"], (string?)body["description"]);
    }

    [Theory]
    [InlineData(2308, "BackstabDmg")]
    [InlineData(2309, "SkitterDmg")]
    public void AnAdditionalAbilityResolvesTheTokenItsDescriptionPrints(int additional, string token)
    {
        var placeholders = Ability(additional)["placeholders"] as JsonArray;

        Assert.Contains(placeholders!.OfType<JsonNode>(), placeholder => (string?)placeholder["key"] == token);
    }
}
