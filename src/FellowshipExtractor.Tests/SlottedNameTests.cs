using System.Text.Json.Nodes;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

public sealed class SlottedNameTests
{
    private static JsonObject? Ability(int id) =>
        Export.Loaded.Documents
            .SingleOrDefault(document => document.Type == EntityType.Ability && document.Id == id).Body;

    [Theory]
    [InlineData(1565, 1260)]
    [InlineData(1581, 1260)]
    [InlineData(1532, 1268)]
    [InlineData(1121, 1122)]
    [InlineData(2241, 2239)]
    [InlineData(999, 995)]
    [InlineData(1239, 1929)]
    [InlineData(2280, 2298)]
    public void AnUnslottedAbilityIsNamedAfterTheSlottedAbilityOfItsName(int unslotted, int slotted)
    {
        Assert.Equal(slotted, (int?)Ability(unslotted)!["namedAfter"]!["id"]);
        Assert.Null(Ability(slotted)!["namedAfter"]);
    }

    [Fact]
    public void ATagIdSupersededByALaterIdForTheSameNameIsNotWritten()
    {
        Assert.Null(Ability(1167));
        Assert.Equal("Kick", (string?)Ability(1200)!["name"]);
    }
}
