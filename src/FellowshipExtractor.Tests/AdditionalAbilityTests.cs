using System.Text.Json.Nodes;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

public sealed class AdditionalAbilityTests
{
    private static JsonObject Ability(int id) =>
        Export.Loaded.Documents.Single(document => document.Type == EntityType.Ability && document.Id == id).Body;

    private static readonly string[] Links = ["replaces", "triggeredBy", "subcastOf"];

    [Theory]
    [InlineData(1018, 2227)]
    [InlineData(1129, 2308)]
    [InlineData(1213, 2309)]
    [InlineData(1106, 2310)]
    [InlineData(2176, 2312)]
    public void TheListingAbilityListsTheIdOfItsAdditionalAbility(int listing, int additional) =>
        Assert.Contains(
            additional,
            Ability(listing)["additionalAbilities"]!.AsArray().Select(entry => (int)entry!["id"]!));

    [Theory]
    [InlineData(2308, 1129, "replaces", "Core")]
    [InlineData(2309, 1213, "replaces", "Core")]
    [InlineData(2227, 1018, "subcastOf", "Power")]
    [InlineData(2310, 1106, "triggeredBy", "Power")]
    [InlineData(2312, 2176, "triggeredBy", null)]
    public void AnAdditionalAbilityHasARecordLinkedToTheListingAbility(
        int additional, int listing, string link, string? category)
    {
        var record = Ability(additional);

        Assert.Equal(listing, (int?)record[link]!["id"]);
        Assert.Equal(category, (string?)record["category"]);
    }

    [Fact]
    public void EveryAdditionalAbilityHasExactlyOneLinkToItsListingAbility()
    {
        foreach (var listing in Export.Loaded.Documents.Where(document =>
                     document.Type == EntityType.Ability && document.Body["additionalAbilities"] is not null))
        {
            foreach (var entry in listing.Body["additionalAbilities"]!.AsArray())
            {
                var record = Ability((int)entry!["id"]!);
                var linked = Links.Where(link => record[link] is not null).ToArray();

                Assert.Single(linked);
                Assert.Equal(listing.Id, (int?)record[linked[0]]!["id"]);
            }
        }
    }

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
    public void AReferenceToAnAdditionalAbilityNamesTheAdditionalAbility()
    {
        var effect = Export.Loaded.Documents.Single(document => document.Type == EntityType.Effect && document.Id == 1380).Body;

        Assert.Equal(2227, (int?)effect["nameBorrowedFrom"]!["id"]);
    }
}
