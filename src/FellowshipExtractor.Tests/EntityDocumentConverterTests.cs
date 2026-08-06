using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

/// <summary>
/// The SDK reading back what <c>JsonlOutputFormat</c> writes. Every assertion here runs against the
/// real export, because these tests exist to notice the corpus moving, not to restate a fixture.
/// </summary>
public sealed class EntityDocumentConverterTests
{
    /// <summary>
    /// Mirrors what <c>JsonlOutputFormat</c> writes with, plus the converter under test. Enums are
    /// bare in the SDK - the generator declares no <see cref="JsonConverterAttribute"/> on them - so
    /// they only read and write as the words the export carries when <see cref="JsonStringEnumConverter"/>
    /// is present too; that behaviour is its own, not <see cref="EntityDocumentConverter"/>'s.
    /// </summary>
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

    /// <summary>The export currently holds this many lines. A count moving here is the export changing.</summary>
    [Fact]
    public void EveryLineOfTheExportDeserialises() => Assert.Equal(12_437, Rows.Count);

    [Fact]
    public void ADocumentsRuntimeClassIsTheDocumentOfItsTypeField() =>
        Assert.All(Rows, row => Assert.Equal(EntityTypes.Document(row.Type), row.Document.GetType()));

    /// <summary>
    /// The per-type counts <see cref="TypeResolverTests.TheCorpusHoldsTheseCounts"/> already states
    /// for spec section 3.4, read from its own theory data by reflection rather than restated here.
    /// </summary>
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

    /// <summary>
    /// Every member resolves a class whether or not today's corpus holds a row of it, because a
    /// member with no rows yet is still a type a caller can be handed and must still read.
    /// </summary>
    [Fact]
    public void EveryMemberResolvesADocumentClass() =>
        Assert.All(
            Enum.GetValues<EntityType>(),
            type => Assert.Equal(type.ToString(), EntityTypes.Document(type).Name));

    public static IEnumerable<object[]> Types() =>
        Enum.GetValues<EntityType>().Select(type => new object[] { type });

    /// <summary>One line of each of the twenty-one types, written back out, equals the line read in.</summary>
    [Theory]
    [MemberData(nameof(Types))]
    public void OneLineOfEachTypeWrittenBackEqualsTheLineThatWasRead(EntityType type)
    {
        var row = Rows.First(r => r.Type == type);
        Assert.Equal(row.Line, JsonSerializer.Serialize(row.Document, Options));
    }

    /// <summary>
    /// A reference nested inside a document, checked independently of which line the corpus happens to
    /// put first for <see cref="EntityType.Effect"/>: <c>NameBorrowedFrom</c> is null on most effects,
    /// so the corpus round trip above could pass on a line that never exercises it. The converter
    /// builds the outer object against the document's own runtime type, which is exactly the path a
    /// dropped nested reference would slip through unnoticed.
    /// </summary>
    [Fact]
    public void ANestedReferenceRoundTripsThroughTheConverter()
    {
        var effect = new Effect
        {
            Id = 7788,
            Name = "Searing Blaze",
            NameBorrowedFrom = new EntityReference(EntityType.GemTrait, 4321, "Ruby Edge"),
        };

        var line = JsonSerializer.Serialize<EntityDocument>(effect, Options);
        var read = JsonSerializer.Deserialize<EntityDocument>(line, Options);

        var readEffect = Assert.IsType<Effect>(read);
        Assert.Equal(effect.NameBorrowedFrom, readEffect.NameBorrowedFrom);
    }

    /// <summary>
    /// <see cref="EntityStore"/>'s own <c>Options</c> sets <c>PropertyNameCaseInsensitive</c>, so the
    /// converter's fallback scan for a differently-cased "type" property is a path the running app
    /// reaches and this had no test of its own for.
    /// </summary>
    [Fact]
    public void ADifferentlyCasedTypePropertyResolvesWhenCaseInsensitive()
    {
        var caseInsensitive = new JsonSerializerOptions(Options) { PropertyNameCaseInsensitive = true };

        var read = JsonSerializer.Deserialize<EntityDocument>("""{"Type":"ability","id":1}""", caseInsensitive);

        Assert.IsType<Ability>(read);
    }

    /// <summary>The other side of the same claim: case still matters when the flag is off.</summary>
    [Fact]
    public void ADifferentlyCasedTypePropertyFailsWhenCaseSensitive()
    {
        var caseSensitive = new JsonSerializerOptions(Options) { PropertyNameCaseInsensitive = false };

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<EntityDocument>("""{"Type":"ability","id":1}""", caseSensitive));
    }
}
