using System.Text.Json;
using FellowshipExtractor.Output;
using Fellowship.SDK.Documents;
using Fellowship.SDK.Models;

namespace FellowshipExtractor.Tests;

public sealed class OutputFormatTests
{
    [Fact]
    public async Task AJsonlLineIsTheDocumentAndItsType()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using (var output = new JsonlOutputFormat(folder))
        {
            await output.WriteAsync(new AbilityDocument { Id = 1122, Name = "Stone Shield" }, default);
        }

        var line = (await File.ReadAllLinesAsync(Path.Combine(folder, "entities.jsonl")))[0];
        using var document = JsonDocument.Parse(line);

        Assert.Equal("ability", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(1122, document.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Stone Shield", document.RootElement.GetProperty("name").GetString());
        Assert.False(document.RootElement.TryGetProperty("table", out _));
    }

    [Fact]
    public async Task AReferenceCarriesTheTypesSlug()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using (var output = new JsonlOutputFormat(folder))
        {
            await output.WriteAsync(
                new EffectDocument
                {
                    Id = 7788,
                    Name = "Searing Blaze",
                    NameBorrowedFrom = new EntityReference(EntityType.GemTrait, 4321, null),
                },
                default);
        }

        var line = (await File.ReadAllLinesAsync(Path.Combine(folder, "entities.jsonl")))[0];
        using var document = JsonDocument.Parse(line);

        Assert.Equal("effect", document.RootElement.GetProperty("type").GetString());

        var reference = document.RootElement.GetProperty("nameBorrowedFrom");

        Assert.Equal("gem-trait", reference.GetProperty("type").GetString());
        Assert.Equal(4321, reference.GetProperty("id").GetInt32());
    }

    [Fact]
    public void AReferencesTypeReadsBackAsTheMemberItWasWrittenFrom() =>
        Assert.All(Enum.GetValues<EntityType>(), type =>
        {
            var json = JsonSerializer.Serialize(new EntityReference(type, 1, null));

            Assert.Contains($"\"{EntityTypes.Slug(type)}\"", json);
            Assert.Equal(type, JsonSerializer.Deserialize<EntityReference>(json)!.Type);
        });
}
