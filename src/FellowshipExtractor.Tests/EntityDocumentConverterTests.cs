using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

public sealed class EntityDocumentConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new EntityDocumentConverter() },
    };

    private static readonly Lazy<IReadOnlyList<(EntityType Type, string Line, EntityDocument Document)>> Loaded =
        new(() =>
        [
            .. File.ReadLines(Path.Combine(Export.Loaded.Folder, "entities.jsonl"))
                .Where(line => line.Length > 0)
                .Select(line => (
                    Type: EntityTypes.Parse(JsonNode.Parse(line)!["type"]!.GetValue<string>()),
                    Line: line,
                    Document: JsonSerializer.Deserialize<EntityDocument>(line, Options)
                        ?? throw new InvalidOperationException($"A line deserialised to nothing: {line}"))),
        ]);

    private static IReadOnlyList<(EntityType Type, string Line, EntityDocument Document)> Rows => Loaded.Value;

    [Fact]
    public void EveryLineOfTheExportDeserialises() => Assert.Equal(12_437, Rows.Count);

    [Fact]
    public void ADocumentsRuntimeClassIsTheDocumentOfItsTypeField() =>
        Assert.All(Rows, row => Assert.Equal(EntityTypes.Document(row.Type), row.Document.GetType()));

    public static IEnumerable<object[]> CorpusCounts()
    {
        var method = typeof(TypeResolverTests).GetMethod(nameof(TypeResolverTests.TheCorpusHoldsTheseCounts))!;
        foreach (var attribute in method.GetCustomAttributes<InlineDataAttribute>())
        {
            foreach (var data in attribute.GetData(method)) yield return data;
        }
    }

    [Theory]
    [MemberData(nameof(CorpusCounts))]
    public void ThePerTypeCountsMatchTheSpec(EntityType type, int expected) =>
        Assert.Equal(expected, Rows.Count(row => row.Type == type));

    [Fact]
    public void EveryMemberResolvesADocumentClass() =>
        Assert.All(
            Enum.GetValues<EntityType>(),
            type => Assert.Equal($"{type}Document", EntityTypes.Document(type).Name));

    public static IEnumerable<object[]> Types() =>
        Enum.GetValues<EntityType>().Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(Types))]
    public void OneLineOfEachTypeWrittenBackEqualsTheLineThatWasRead(EntityType type)
    {
        var row = Rows.First(r => r.Type == type);
        Assert.Equal(row.Line, JsonSerializer.Serialize(row.Document, Options));
    }

    [Fact]
    public void ANestedReferenceRoundTripsThroughTheConverter()
    {
        var effect = new EffectDocument
        {
            Id = 7788,
            Name = "Searing Blaze",
            NameBorrowedFrom = new EntityReference(EntityType.GemTrait, 4321, "Ruby Edge"),
        };

        var line = JsonSerializer.Serialize<EntityDocument>(effect, Options);
        var read = JsonSerializer.Deserialize<EntityDocument>(line, Options);

        var readEffect = Assert.IsType<EffectDocument>(read);
        Assert.Equal(effect.NameBorrowedFrom, readEffect.NameBorrowedFrom);
    }

    [Fact]
    public void ADifferentlyCasedTypePropertyResolvesWhenCaseInsensitive()
    {
        var caseInsensitive = new JsonSerializerOptions(Options) { PropertyNameCaseInsensitive = true };

        var read = JsonSerializer.Deserialize<EntityDocument>("""{"Type":"ability","id":1}""", caseInsensitive);

        Assert.IsType<AbilityDocument>(read);
    }

    [Fact]
    public void ADifferentlyCasedTypePropertyFailsWhenCaseSensitive()
    {
        var caseSensitive = new JsonSerializerOptions(Options) { PropertyNameCaseInsensitive = false };

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<EntityDocument>("""{"Type":"ability","id":1}""", caseSensitive));
    }
}
