using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class AbilityOwnerTests
{
    [Fact]
    public void AnAbilityStatesTheHeroesThatOwnItInTheExport()
    {
        var owned = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Ability && document.Body["heroes"] is JsonArray)
            .ToArray();

        Assert.NotEmpty(owned);
    }

    [Fact]
    public void AnEffectStatesTheCreatureThatOwnsItInTheExport()
    {
        var owned = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Effect && document.Body["owner"] is JsonObject)
            .ToArray();

        Assert.NotEmpty(owned);
    }
}
