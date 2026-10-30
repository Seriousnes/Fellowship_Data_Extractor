using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class SystemsReaderTests
{
    private const int CrownOfTheWild = 36;

    [Fact]
    public void ADungeonPlacesItsCreaturesOnTheFloorsItsAlwaysLoadedSublevelsHold()
    {
        var crown = Export.Loaded.Documents.Single(row => row.Type == EntityType.Dungeon && row.Id == CrownOfTheWild).Body;

        Assert.NotEmpty(crown["floors"]!.AsArray());
        Assert.NotEmpty(crown["placements"]!.AsArray());
        Assert.All(crown["placements"]!.AsArray(), placed => Assert.NotNull(placed!["floor"]));
    }

    [Fact]
    public void AnAcquireDescriptionFillsEveryArgumentItsConditionStates()
    {
        var acquired = Export.Loaded.Documents
            .Select(row => row.Body["acquire"]?.GetValue<string>())
            .OfType<string>()
            .ToArray();

        Assert.NotEmpty(acquired);
        Assert.DoesNotContain(acquired, text => text.Contains('{'));
        Assert.Contains("Low chance of dropping when completing <rt.italic>Crown of the Wild</>.", acquired);
        Assert.Contains("Unlocked by reaching 12,500 Dungeon Score with Tariq.", acquired);
    }

    [Fact]
    public void ARelicAHeroCollectionGrantsComesFromTheTemplateChallengeItExtends()
    {
        var orbs = Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Item && row.Name?.StartsWith("Orb of", StringComparison.Ordinal) is true)
            .ToArray();

        Assert.Contains(orbs, row => row.Body["sources"] is JsonArray sources &&
                                     sources.Any(source => source!["name"]?.GetValue<string>() == "An Orb to Ponder"));
    }
}
