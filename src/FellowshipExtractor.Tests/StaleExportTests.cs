using System.Text.Json;

using FellowshipExtractor.Api.Models;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Tests;

public sealed class StaleExportTests
{
    private static EntityStore Read(string line)
    {
        var folder = Directory.CreateTempSubdirectory("fellowship-export").FullName;

        try
        {
            File.Copy(Path.Combine(Export.Loaded.Folder, "vocabulary.json"), Path.Combine(folder, "vocabulary.json"));
            File.WriteAllLines(Path.Combine(folder, "entities.jsonl"), [line]);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["FELLOWSHIP_EXPORT"] = folder })
                .Build();

            return new EntityStore(new ExportPaths(configuration), NullLogger<EntityStore>.Instance);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnExportWrittenBeforeDocumentsCarriedATypeNamesTheCommandThatRewritesIt()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => Read("""{"table":"ItemID","netId":5224,"name":"Sunforged Greatsword"}"""));

        Assert.Contains("entities.jsonl", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Line 1", failure.Message, StringComparison.Ordinal);
        Assert.Contains("export-all", failure.Message, StringComparison.Ordinal);
        Assert.IsType<JsonException>(failure.InnerException);
    }

    [Fact]
    public void ALineThatNamesNoTypeCountsFromTheLineItIsOn()
    {
        var document = """{"type":"item","id":1}""";

        var failure = Assert.Throws<InvalidOperationException>(
            () => Read($"{document}\n{document}\n{{\"id\":3}}"));

        Assert.Contains("Line 3", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentStillReads()
    {
        var store = Read("""{"type":"item","id":5224,"name":"Sunforged Greatsword"}""");

        Assert.Equal(1, store.Count);
        Assert.Equal("Sunforged Greatsword", store.Entities[0].Name);
    }
}
