using System.Text.Json.Nodes;

using Fellowship.Codex.Models;

namespace FellowshipExtractor.Tests;

public sealed class AttributeTableTests
{
    private static readonly string[] RatedStats = ["Haste", "CritChance", "Expertise", "Spirit"];

    private static AttributeDisplayView Attribute(string stat)
    {
        var attributes = Export.Loaded.Settings.StatCalculation.Attributes;

        Assert.True(attributes.ContainsKey(stat), $"DT_AttributeProperties no longer has a '{stat}' row.");
        return new AttributeDisplayView(attributes[stat]);
    }

    [Theory]
    [InlineData("Haste")]
    [InlineData("CritChance")]
    [InlineData("Expertise")]
    [InlineData("Spirit")]
    public void RatedStatsConvertAtSixteenHundredths(string stat) =>
        Assert.Equal(0.16, Attribute(stat).Value.BaseStatMultiplier);

    [Theory]
    [InlineData("Haste")]
    [InlineData("CritChance")]
    [InlineData("Expertise")]
    [InlineData("Spirit")]
    public void RatedStatsShareTheSameLadder(string stat)
    {
        var attribute = Attribute(stat).Value;
        var brackets = attribute.Brackets;

        Assert.NotNull(brackets);
        Assert.Equal(4, brackets!.Count);

        Assert.Equal([10d, 15d, 20d, 25d], brackets.Select(b => b.MaxBracketValue));
        Assert.Equal([1d, 0.98d, 0.96d, 0.94d], brackets.Select(b => b.PenaltyPercentage));
        Assert.Equal(0.92, attribute.FinalPenaltyPercentage);
    }

    [Theory]
    [InlineData("Haste", "HasteRating")]
    [InlineData("CritChance", "CritRating")]
    [InlineData("Expertise", "ExpertiseRating")]
    [InlineData("Spirit", "SpiritRating")]
    public void RatedStatsNameARatingAttribute(string stat, string expected)
    {
        Assert.Equal(expected, Attribute(stat).Value.Attribute);
        Assert.NotEqual(stat, Attribute(stat).Value.Attribute);
    }

    [Theory]
    [InlineData("Strength")]
    [InlineData("Agility")]
    [InlineData("Intellect")]
    [InlineData("Stamina")]
    public void PrimaryStatsAreNotRatings(string stat)
    {
        var attribute = Attribute(stat).Value;

        Assert.Equal(1, attribute.BaseStatMultiplier);
        Assert.Null(attribute.Brackets);
        Assert.False(StatConversion.Converts(attribute));
    }

    [Fact]
    public void NoCoefficientMultipliesAStatThatConverts()
    {
        var attributes = Export.Loaded.Settings.StatCalculation.Attributes;

        var multiplied = Export.Loaded.Documents
            .Select(document => document.Body["placeholders"])
            .OfType<JsonArray>()
            .SelectMany(tokens => tokens)
            .OfType<JsonObject>()
            .Where(token => token["coefficient"] is not null)
            .Select(token => token["attribute"]?.GetValue<string>())
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(multiplied);

        var converting = multiplied
            .Where(a => attributes.TryGetValue(a, out var declared) && StatConversion.Converts(declared))
            .ToArray();

        Assert.True(
            converting.Length == 0,
            $"A coefficient now multiplies {string.Join(", ", converting)}, which the conversion rescales. " +
            "Every damage number using it has changed scale.");
    }

    [Fact]
    public void EveryStatTheApiAcceptsHasARow()
    {
        var attributes = Export.Loaded.Settings.StatCalculation.Attributes;

        foreach (var stat in RatedStats.Concat(["Strength", "Agility", "Intellect", "Stamina", "Armor"]))
        {
            Assert.True(attributes.ContainsKey(stat), $"No DT_AttributeProperties row for '{stat}'.");
            Assert.False(
                string.IsNullOrWhiteSpace(attributes[stat].DisplayName),
                $"'{stat}' has no display name, so a tooltip would print the internal name.");
        }
    }

    [Fact]
    public void CritChancePrintsAsCriticalStrike() =>
        Assert.Equal("Critical Strike", Attribute("CritChance").Value.DisplayName);

    [Fact]
    public void CooldownReductionStillRepeatsItsBandCeilings()
    {
        var brackets = Attribute("CooldownReduction").Value.Brackets;

        Assert.NotNull(brackets);

        var ceilings = brackets!.Select(b => b.MaxBracketValue).ToArray();
        var ascending = ceilings.Zip(ceilings.Skip(1), (a, b) => b > a).All(rising => rising);

        Assert.False(
            ascending,
            $"CooldownReduction's band ceilings now ascend ({string.Join(", ", ceilings)}), so bands that " +
            "never applied before now do. The numbers that stat converts to have changed.");
    }

    private readonly record struct AttributeDisplayView(AttributeDisplay Value);
}
