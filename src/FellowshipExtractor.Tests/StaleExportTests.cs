using System.Text.Json;

using Fellowship.Codex.Models;
using Fellowship.Codex.Services;

using Microsoft.Extensions.Logging.Abstractions;

namespace FellowshipExtractor.Tests;

public sealed class StaleExportTests
{
    private static EntityStoreService Read(string line)
    {
        var folder = Directory.CreateTempSubdirectory("fellowship-export").FullName;

        try
        {
            File.Copy(
                Path.Combine(Export.Loaded.Folder, ExportReader.SettingsName),
                Path.Combine(folder, ExportReader.SettingsName));

            File.WriteAllLines(Path.Combine(folder, ExportReader.EntitiesName), [line]);

            var snapshot = new FolderExportSource(folder)
                .LoadAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .Snapshot;

            return new EntityStoreService(snapshot, NullLogger<EntityStoreService>.Instance);
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
        Assert.Contains("src/FellowshipExtractor", failure.Message, StringComparison.Ordinal);
        Assert.IsType<NotSupportedException>(failure.InnerException);
    }

    [Fact]
    public void ALineThatNamesNoTypeCountsFromTheLineItIsOn()
    {
        var document = """{"$type":"item","id":1}""";

        var failure = Assert.Throws<InvalidOperationException>(
            () => Read($"{document}\n{document}\n{{\"id\":3}}"));

        Assert.Contains("Line 3", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFolderCarryingNoSettingsNamesThePublisherAndTheCommandThatFillsIt()
    {
        var folder = Directory.CreateTempSubdirectory("fellowship-export").FullName;

        try
        {
            var failure = Assert.Throws<InvalidOperationException>(() =>
                new FolderExportSource(folder).LoadAsync(CancellationToken.None).GetAwaiter().GetResult());

            Assert.Contains(ExportReader.SettingsName, failure.Message, StringComparison.Ordinal);
            Assert.Contains("FellowshipExtractor.Publisher", failure.Message, StringComparison.Ordinal);
            Assert.Contains("src/FellowshipExtractor", failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ADocumentStillReads()
    {
        var store = Read("""{"$type":"item","id":5224,"name":"Sunforged Greatsword"}""");

        Assert.Equal(1, store.Count);
        Assert.Equal("Sunforged Greatsword", store.Entities[0].Name);
    }
}
