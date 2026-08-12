namespace Fellowship.Codex.Tests;

public class SnapshotTests
{
    [Fact]
    public async Task A_snapshot_missing_its_settings_names_the_command_that_fills_it()
    {
        var folder = Directory.CreateTempSubdirectory("fellowship-snapshot").FullName;

        try
        {
            Directory.CreateDirectory(Path.Combine(folder, ExportReader.Data));

            File.WriteAllLines(
                Path.Combine(folder, ExportReader.Data, ExportReader.EntitiesName),
                ["""{"$type":"item","id":5224,"name":"Sunforged Greatsword"}"""]);

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new FolderExportSource(folder).LoadAsync(Xunit.TestContext.Current.CancellationToken));

            Assert.Contains(
                $"{ExportReader.Data}/{ExportReader.SettingsName}", failure.Message, StringComparison.Ordinal);
            Assert.Contains("FellowshipExtractor.Publisher", failure.Message, StringComparison.Ordinal);
            Assert.Contains("export-all", failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task A_snapshot_carrying_its_settings_reads_the_documents_beside_them()
    {
        var snapshot = await new FolderExportSource(Export.Found())
            .LoadAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.NotEmpty(snapshot.Entities);
        Assert.NotEmpty(snapshot.Settings.Rarities);
        Assert.NotEmpty(snapshot.Settings.RarityColorRows);
    }
}
