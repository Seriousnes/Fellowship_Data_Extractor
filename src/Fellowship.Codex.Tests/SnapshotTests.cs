namespace Fellowship.Codex.Tests;

public class SnapshotTests
{
    [Fact]
    public async Task A_snapshot_missing_its_settings_names_the_command_that_fills_it()
    {
        var folder = Directory.CreateTempSubdirectory("fellowship-snapshot").FullName;

        try
        {
            File.WriteAllLines(
                Path.Combine(folder, ExportReader.EntitiesName),
                ["""{"$type":"item","id":5224,"name":"Sunforged Greatsword"}"""]);

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new FolderExportSource(folder).LoadAsync(Xunit.TestContext.Current.CancellationToken));

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
    public async Task A_snapshot_carrying_its_settings_reads_the_documents_beside_them()
    {
        var history = await new FolderExportSource(Export.Found())
            .LoadAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.NotEmpty(history.Snapshot.Entities);
        Assert.NotEmpty(history.Snapshot.Settings.Rarities);
        Assert.NotEmpty(history.Snapshot.Settings.RarityColorRows);
    }
}
