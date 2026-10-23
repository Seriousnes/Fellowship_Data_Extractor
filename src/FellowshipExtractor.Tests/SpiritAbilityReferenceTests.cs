using System.Text.Json.Nodes;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

public sealed class SpiritAbilityReferenceTests
{
    private static JsonArray? References(string hero) =>
        Export.Loaded.Documents
            .Single(document => document.Type == EntityType.Hero && document.Name == hero)
            .Body["spiritAbilityReferences"]?.AsArray();

    [Fact]
    public void ASpiritAbilityNamesTheHerosOwnAbilitiesAndEffectsInTheOrderTheTextNamesThem()
    {
        Assert.Equal(
            ["effect 1532", "effect 1530", "effect 2420", "ability 1122", "effect 3115"],
            References("Meiko")!.Select(reference => $"{reference!["type"]} {reference["id"]}"));
    }

    [Fact]
    public void ANameLeadingALongerCapitalisedPhraseIsNotAReference()
    {
        Assert.Equal(
            ["effect 3115"],
            References("Ardeos")!.Select(reference => $"{reference!["type"]} {reference["id"]}"));
    }

    [Fact]
    public void AQuotedNameResolvesAgainstEffectsNoHeroOwns()
    {
        Assert.Equal(
            ["effect 3115", "ability 1310"],
            References("Elarion")!.Select(reference => $"{reference!["type"]} {reference["id"]}"));
    }
}
