using System.Text.Json;

using Fellowship.Codex.Endpoints;
using Fellowship.Codex.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FellowshipExtractor.Tests;

public sealed class RollLineTests
{
    private const int FrostedCloak = 2665;

    private static readonly JsonSerializerOptions Reading = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static Fellowship.SDK.Documents.ItemDocument Cloak()
    {
        var body = Export.Loaded.Documents
            .Where(document => document.Type == EntityType.Item && document.Id == FrostedCloak)
            .Select(document => document.Body)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Item {FrostedCloak} is not in the export. Either the id moved or the entity is gone.");

        return new Fellowship.SDK.Documents.ItemDocument
        {
            Id = FrostedCloak,
            Slot = body["slot"]?.GetValue<string>(),
            Stats = body["stats"].Deserialize<IReadOnlyList<StatSlot>>(Reading),
        };
    }

    private static StatParameters Parameters() =>
        new(Export.Loaded.Settings.StatCalculation.Attributes);

    private sealed record Probe
    {
        public int Id { get; init; }
    }

    private static IReadOnlyList<StatLine> Lines(params (string Key, string Value)[] pairs)
    {
        var parameters = Parameters();

        var stats = StatQuery.FromQuery(
            new QueryCollection(pairs
                .GroupBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => new StringValues(group.Select(p => p.Value).ToArray()))),
            RequestProperties.Of(typeof(Probe)),
            parameters,
            out var error);

        Assert.Null(error);

        var query = StatQuery.Read(stats, parameters, out error);

        Assert.Null(error);

        return StatMagnitudes.For(
            Cloak(),
            Export.Loaded.Settings.StatCalculation,
            new HashSet<string>(["Strength", "Intellect", "Agility"], StringComparer.OrdinalIgnoreCase),
            rarity: null,
            Export.Loaded.Settings.DropItemLevels
                .Single(level => level.Row == FellowshipExtractor.Extraction.DropItemLevels.DefaultRow).ItemLevel,
            query);
    }

    private static decimal Line(IReadOnlyList<StatLine> lines, string stat) =>
        lines.Single(line => string.Equals(line.Stat, stat, StringComparison.OrdinalIgnoreCase)).Value;

    [Fact]
    public void ARatedSlotPrintsTheRatingTheCallerSent() =>
        Assert.Equal(17m, Line(Lines(("spirit", "17")), "Spirit"));

    [Fact]
    public void APrimarySlotPrintsTheNumberTheCallerSent() =>
        Assert.Equal(250m, Line(Lines(("agility", "250")), "Agility"));

    [Fact]
    public void AnUnnamedSlotIsStillComputed() =>
        Assert.True(Line(Lines(("spirit", "17")), "Expertise") > 0m);

    [Fact]
    public void ThePercentageFormLeavesTheSlotComputed()
    {
        var computed = Line(Lines(), "Spirit");

        Assert.Equal(computed, Line(Lines(("spiritPercentage", "2.72")), "Spirit"));
        Assert.NotEqual(2.72m, computed);
    }
}
