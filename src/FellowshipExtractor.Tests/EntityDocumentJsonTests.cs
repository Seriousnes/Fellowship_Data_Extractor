using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;
using FellowshipExtractor.Output;

namespace FellowshipExtractor.Tests;

public sealed class EntityDocumentJsonTests
{
    private static readonly JsonSerializerOptions Options = EntityDocuments.Options;

    private static readonly Lazy<IReadOnlyList<(EntityType Type, string Line, EntityDocument Document)>> Loaded =
        new(() =>
        [
            .. File.ReadLines(Path.Combine(
                    Export.Loaded.Folder, ExportLayout.DataFolder, ExportLayout.EntitiesFile))
                .Where(line => line.Length > 0)
                .Select(line => (
                    Type: EntityTypes.Parse(JsonNode.Parse(line)![EntityDocuments.Discriminator]!.GetValue<string>()),
                    Line: line,
                    Document: JsonSerializer.Deserialize<EntityDocument>(line, Options)
                        ?? throw new InvalidOperationException($"A line deserialised to nothing: {line}"))),
        ]);

    private static IReadOnlyList<(EntityType Type, string Line, EntityDocument Document)> Rows => Loaded.Value;

    [Fact]
    public void EveryLineOfTheExportDeserialises() => Assert.Equal(12_655, Rows.Count);

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

    [Fact]
    public void EveryLineWrittenBackEqualsTheLineThatWasRead() =>
        Assert.All(Rows, row => Assert.Equal(row.Line, JsonSerializer.Serialize(row.Document, Options)));

    [Fact]
    public void ANestedReferenceRoundTripsThroughTheDiscriminator()
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
    public void ADocumentKeepsATypePropertyOfItsOwnBesideTheDiscriminator()
    {
        var gem = new GemTypeDocument { Id = 1, Type = GemType.Ruby, Attribute = "RubyPower" };

        var line = JsonSerializer.Serialize<EntityDocument>(gem, Options);
        var body = JsonNode.Parse(line)!.AsObject();

        Assert.Equal("gem-type", body[EntityDocuments.Discriminator]!.GetValue<string>());
        Assert.Equal("Ruby", body["type"]!.GetValue<string>());

        Assert.Equal(GemType.Ruby, Assert.IsType<GemTypeDocument>(
            JsonSerializer.Deserialize<EntityDocument>(line, Options)).Type);
    }

    [Fact]
    public void ADiscriminatorAfterThePropertiesStillNamesTheClass()
    {
        var read = JsonSerializer.Deserialize<EntityDocument>(
            $$"""{"id":1,"name":"Searing Blaze","{{EntityDocuments.Discriminator}}":"ability"}""", Options);

        Assert.Equal("Searing Blaze", Assert.IsType<AbilityDocument>(read).Name);
    }
}
