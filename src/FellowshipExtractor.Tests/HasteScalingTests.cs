using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

public sealed class HasteScalingTests
{
    private const int Incinerate = 1270;
    private const int Chronoshift = 355;

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"{type} {id} is not in the export. Either the id moved or the entity is gone.");

    [Fact]
    public void IncinerateDeclaresHasteScaling() =>
        Assert.True(Document(EntityType.Ability, Incinerate)["scalesWithHaste"]?.GetValue<bool>());

    [Fact]
    public void IncinerateTicksEveryHalfSecond()
    {
        var ability = Document(EntityType.Ability, Incinerate);

        Assert.Equal(0.5, ability["channelTick"]?.GetValue<double>());
        Assert.Equal(0.5, ability["period"]?.GetValue<double>());
        Assert.Equal(2.5, ability["channelTime"]?.GetValue<double>());
        Assert.Equal(1.5, ability["castTime"]?.GetValue<double>());
    }

    [Fact]
    public void ChronoshiftPulsesEveryChannelTick()
    {
        var power = Document(EntityType.WeaponPower, Chronoshift);

        Assert.Equal(1.5, power["channelTick"]?.GetValue<double>());
    }

    [Fact]
    public void ResolvedTickratesAreMarkedAsSuch()
    {
        var tickrates = Tokens()
            .Where(token => token["tickrate"]?.GetValue<bool>() == true)
            .ToArray();

        Assert.NotEmpty(tickrates);
        Assert.All(tickrates, token => Assert.NotNull(token["value"]));
    }

    [Fact]
    public void TheHasteOptOutIsStillShipped()
    {
        var exempt = Export.Loaded.Documents
            .Where(document => document.Body["tickrateIgnoresHaste"]?.GetValue<bool>() == true)
            .ToArray();

        Assert.True(
            exempt.Length > 0,
            "No effect declares EffectLogic.TickrateNotAffectedByHasteExclusively any more. Either the " +
            "tag is gone from the build or it stopped being read, and the tickrate default rests on it.");
    }

    private static IEnumerable<JsonObject> Tokens() =>
        Export.Loaded.Documents
            .Select(document => document.Body["placeholders"])
            .OfType<JsonArray>()
            .SelectMany(tokens => tokens)
            .OfType<JsonObject>();
}
