namespace Fellowship.Codex.Tests;

public class HistoryTests
{
    private const string Older = """{"$type":"item","id":5224,"name":"Sunforged Greatsword","slot":"Back"}""";

    private const string Newer = """{"$type":"item","id":5224,"name":"Sunforged Greatsword","slot":"Chest"}""";

    private const string Settings = """{"gemTypes":["Ruby"]}""";

    private static string Version(string output, string build, string line)
    {
        var folder = Path.Combine(output, ExportFolders.Name(build));

        Directory.CreateDirectory(Path.Combine(folder, ExportReader.Data));

        File.WriteAllLines(Path.Combine(folder, ExportReader.Data, ExportReader.EntitiesName), [line]);
        File.WriteAllText(Path.Combine(folder, ExportReader.Data, ExportReader.SettingsName), Settings);

        return folder;
    }

    [Fact]
    public async Task Two_builds_side_by_side_load_as_the_newer_and_the_one_beneath_it()
    {
        var output = Directory.CreateTempSubdirectory("fellowship-history").FullName;

        try
        {
            Version(output, "1000", Older);
            var newest = Version(output, "1001", Newer);

            var history = await new FolderExportSource(newest)
                .LoadAsync(Xunit.TestContext.Current.CancellationToken);

            Assert.Equal("1001", history.Latest);
            Assert.Equal("Chest", Assert.IsType<ItemDocument>(Assert.Single(history.Snapshot.Entities)).Slot);

            var beneath = Assert.Single(history.Older).Value;

            Assert.Equal("1000", beneath.Build);
            Assert.Equal(Settings, beneath.Settings);
            Assert.Equal([Older], beneath.Documents[new DocumentKey("item", 5224)]);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task A_build_reads_none_of_the_builds_above_it()
    {
        var output = Directory.CreateTempSubdirectory("fellowship-history").FullName;

        try
        {
            var beneath = Version(output, "1000", Older);
            Version(output, "1001", Newer);

            var history = await new FolderExportSource(beneath)
                .LoadAsync(Xunit.TestContext.Current.CancellationToken);

            Assert.Equal("1000", history.Latest);
            Assert.Empty(history.Older);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public async Task A_folder_named_for_no_build_carries_no_history()
    {
        var output = Directory.CreateTempSubdirectory("fellowship-history").FullName;
        var named = Path.Combine(output, "pinned");

        try
        {
            Version(output, "1000", Older);

            Directory.CreateDirectory(Path.Combine(named, ExportReader.Data));
            File.WriteAllLines(
                Path.Combine(named, ExportReader.Data, ExportReader.EntitiesName), [Newer]);
            File.WriteAllText(
                Path.Combine(named, ExportReader.Data, ExportReader.SettingsName), Settings);

            var history = await new FolderExportSource(named)
                .LoadAsync(Xunit.TestContext.Current.CancellationToken);

            Assert.Equal("pinned", history.Latest);
            Assert.Empty(history.Older);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void A_line_carrying_no_type_or_id_joins_no_key()
    {
        var grouped = ExportReader.Group([Older, "{}", "not json at all", string.Empty, Newer]);

        Assert.Equal(2, Assert.Single(grouped).Value.Count);
        Assert.Equal([Older, Newer], grouped[new DocumentKey("item", 5224)]);
    }
}
