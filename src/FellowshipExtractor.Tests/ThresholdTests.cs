using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Fellowship.SDK.Models;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed partial class ThresholdTests
{
    private const string Field = "thresholds";
    private const string Health = "Health";
    private const string MaxHealth = "MaxHealth";

    private static readonly Lazy<Thresholds> Reader = new(() => Thresholds.Read(
        Export.Provider,
        Export.Provider.MappingsContainer?.MappingsForGame
        ?? throw new InvalidOperationException("No mappings are loaded, so no enum reads back."),
        new ConstantsTable(Export.Provider)));

    [GeneratedRegex(@"health\D{0,20}?(\d+)%|(\d+)% health", RegexOptions.IgnoreCase)]
    private static partial Regex Printed();

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents.First(document => document.Type == type && document.Id == id).Body;

    private static JsonObject Only(EntityType type, int id)
    {
        var thresholds = Document(type, id)[Field] as JsonArray
            ?? throw new InvalidOperationException($"{type} {id} states no {Field}.");

        return Assert.IsType<JsonObject>(Assert.Single(thresholds));
    }

    private static double Percent(EntityType type, int id)
    {
        var match = Printed().Match((string?)Document(type, id)["description"] ?? "");
        var printed = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

        return double.Parse(printed, CultureInfo.InvariantCulture) / 100;
    }

    private static int Linked(EntityType type, int id, string field, EntityType linked)
    {
        var held = Document(type, id)[field];

        return (held is JsonArray references ? references.OfType<JsonObject>() : held is JsonObject single ? [single] : [])
            .Where(reference => (string?)reference["type"] == EntityTypes.Slug(linked))
            .Select(reference => (int)reference["id"]!)
            .Single();
    }

    [Fact]
    public void LastLightsGrantsAnEffectRequiringTheLowHealthState()
    {
        var effect = Linked(EntityType.Talent, 164, "grants", EntityType.Effect);
        var threshold = Only(EntityType.Effect, effect);

        Assert.Equal(Health, (string?)threshold["attribute"]);
        Assert.Equal(MaxHealth, (string?)threshold["maximum"]);
        Assert.Equal("LowHealth", (string?)threshold["state"]);
        Assert.Null(threshold["absent"]);
        Assert.True((bool?)threshold["target"]);
        Assert.Equal(Reader.Value.States["LowHealth"], (double)threshold["value"]!);
        Assert.Equal(Percent(EntityType.Talent, 164), (double)threshold["value"]!, 6);
    }

    [Fact]
    public void DefianceAppliesItsBuffWhileTheHealthyStateIsAbsent()
    {
        var ability = Linked(EntityType.Talent, 648, "grants", EntityType.Ability);
        var buff = Export.Loaded.Documents
            .Single(document => document.Type == EntityType.Effect &&
                                document.Body["appliedBy"] is JsonObject applier &&
                                (string?)applier["type"] == EntityTypes.Slug(EntityType.Ability) &&
                                (int)applier["id"]! == ability)
            .Id;
        var threshold = Only(EntityType.Effect, buff);

        Assert.Equal(Health, (string?)threshold["attribute"]);
        Assert.Equal("Healthy", (string?)threshold["state"]);
        Assert.True((bool?)threshold["absent"]);
        Assert.Null(threshold["target"]);
        Assert.Equal(Reader.Value.States["Healthy"], (double)threshold["value"]!);
        Assert.Equal(Percent(EntityType.Talent, 648), (double)threshold["value"]!, 6);
    }

    [Fact]
    public void LatentResurgenceComparesHealthAgainstItsAbilitysThreshold()
    {
        var ability = Linked(EntityType.Effect, 2151, "partOf", EntityType.Ability);
        var threshold = Only(EntityType.Ability, ability);
        var (row, _) = new ConstantsTable(Export.Provider).Lookup("Trait.IncomingHealToStackingHealWithThreshold.MaxHealthThreshold");

        Assert.Equal(Health, (string?)threshold["attribute"]);
        Assert.Equal(MaxHealth, (string?)threshold["maximum"]);
        Assert.Equal("LessThanOrEqualTo", (string?)threshold["comparison"]);
        Assert.Null(threshold["state"]);
        Assert.Equal(row!.Value.Value, (double)threshold["value"]!);
        Assert.Equal(Percent(EntityType.Effect, 2151), (double)threshold["value"]!, 6);
    }
}
