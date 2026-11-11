using System.Text.Json.Nodes;

namespace FellowshipExtractor.Tests;

/// <summary>
/// A document's <c>fieldSources</c> names the rows each scalar field is set from, and the keys of its <c>tuning</c>
/// for those rows.
/// </summary>
public sealed class FieldSourceTests
{
    private const int Apocalypse = 1273;

    private static JsonObject Document(EntityType type, int id) =>
        Export.Loaded.Documents
            .Where(document => document.Type == type && document.Id == id)
            .Select(document => document.Body)
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"{type} {id} is not in the export.");

    private static IEnumerable<(string Record, JsonObject Body, string Field, JsonObject Source)> Sources() =>
        Export.Loaded.Documents.SelectMany(document =>
            (document.Body["fieldSources"] as JsonObject ?? new JsonObject())
            .Select(entry => ($"{document.Type} {document.Id}", document.Body, entry.Key, entry.Value!.AsObject())));

    private static string[] Strings(JsonNode? node) =>
        node is JsonArray values ? [.. values.Select(value => value!.GetValue<string>())] : [];

    /// <summary>
    /// Apocalypse's tooltip hook computes the cast time from <c>CastTime</c>, so the row is also in its tuning. The range
    /// is the row its targeting binds, <c>MaxRangeHorizontal</c>, and <c>MaxRangeDown</c> is a row of its own.
    /// </summary>
    [Fact]
    public void ApocalypsesFieldsAreTheRowsItsAssetSetsThemFrom()
    {
        var sources = Document(EntityType.Ability, Apocalypse)["fieldSources"]!.AsObject();

        var castTime = sources["castTime"]!;
        var range = sources["range"]!;

        Assert.Equal("ConstantsRow", castTime["kind"]!.GetValue<string>());
        Assert.Equal(["Firemage.CastedPlacedAoe.CastTime"], Strings(castTime["rows"]));
        Assert.Equal(["CastTime"], Strings(castTime["tuning"]));

        Assert.Equal("ConstantsRow", range["kind"]!.GetValue<string>());
        Assert.Equal(["Firemage.CastedPlacedAoe.MaxRangeHorizontal"], Strings(range["rows"]));
        Assert.Null(range["tuning"]);

        Assert.Equal(["Firemage.CastedPlacedAoe.Cooldown"], Strings(sources["cooldown"]!["rows"]));
        Assert.Equal(["Firemage.CastedPlacedAoe.Radius"], Strings(sources["radius"]!["rows"]));
        Assert.Null(sources["maxTargets"]);
    }

    [Fact]
    public void EveryTuningKeyOfASourceIsAKeyOfItsRecordsTuning()
    {
        var unkeyed = Sources()
            .SelectMany(found => Strings(found.Source["tuning"])
                .Where(key => found.Body["tuning"]?[key] is null)
                .Select(key => $"{found.Record}.{found.Field}: {key}"))
            .ToArray();

        Assert.Empty(unkeyed);
    }

    [Fact]
    public void EveryFieldWithASourceIsAMemberOfItsRecord()
    {
        var absent = Sources()
            .Where(found => found.Body[found.Field] is null)
            .Select(found => $"{found.Record}.{found.Field}")
            .ToArray();

        Assert.Empty(absent);
    }

    /// <summary>A row with a value per rank reads its first rank.</summary>
    private static JsonNode? Stated(JsonNode? value) => value is JsonArray ranks ? ranks.FirstOrDefault() : value;

    [Fact]
    public void AConstantsRowFieldWithOneTuningKeyHasThatKeysValue()
    {
        var differing = Sources()
            .Where(found => found.Source["kind"]!.GetValue<string>() == "ConstantsRow")
            .Where(found => Strings(found.Source["tuning"]) is [_])
            .Where(found => Stated(found.Body["tuning"]![Strings(found.Source["tuning"])[0]]!["value"]) is not JsonValue stated ||
                            stated.GetValue<double>() != found.Body[found.Field]!.GetValue<double>())
            .Select(found => $"{found.Record}.{found.Field}: {found.Body[found.Field]} against " +
                             $"{found.Body["tuning"]![Strings(found.Source["tuning"])[0]]!["value"]?.ToJsonString()}")
            .ToArray();

        Assert.Empty(differing);
    }
}
