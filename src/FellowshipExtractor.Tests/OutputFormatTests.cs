using System.Text.Json;
using FellowshipExtractor.Output;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class OutputFormatTests
{
    [Fact]
    public async Task AJsonlLineIsTheDocumentAndItsType()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using (var output = new JsonlOutputFormat(folder))
        {
            await output.WriteAsync(new Ability { Id = 1122, Name = "Stone Shield" }, default);
        }

        var line = (await File.ReadAllLinesAsync(Path.Combine(folder, "entities.jsonl")))[0];
        using var document = JsonDocument.Parse(line);

        Assert.Equal("ability", document.RootElement.GetProperty("type").GetString());
        Assert.Equal(1122, document.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Stone Shield", document.RootElement.GetProperty("name").GetString());
        Assert.False(document.RootElement.TryGetProperty("table", out _));
    }

    /// <summary>
    /// A reference nested in a line spells its type the way a line spells its own. GemTrait is one of
    /// the four whose slug differs from the member by more than case, so written as the member it
    /// joins to none of the 60 lines it names.
    /// </summary>
    [Fact]
    public async Task AReferenceCarriesTheTypesSlug()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        await using (var output = new JsonlOutputFormat(folder))
        {
            await output.WriteAsync(
                new Effect
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

    /// <summary>
    /// Every type survives the round trip, so a reader deserialising what the export writes gets the
    /// member back rather than a slug nothing parses.
    /// </summary>
    [Fact]
    public void AReferencesTypeReadsBackAsTheMemberItWasWrittenFrom() =>
        Assert.All(Enum.GetValues<EntityType>(), type =>
        {
            var json = JsonSerializer.Serialize(new EntityReference(type, 1, null));

            Assert.Contains($"\"{EntityTypes.Slug(type)}\"", json);
            Assert.Equal(type, JsonSerializer.Deserialize<EntityReference>(json)!.Type);
        });
}
