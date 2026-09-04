namespace Fellowship.Codex.Tests;

public class StorageExportSourceTests
{
    private const string Older = """{"$type":"item","id":5224,"name":"Sunforged Greatsword","slot":"Back"}""";

    private const string Newer = """{"$type":"item","id":5224,"name":"Sunforged Greatsword","slot":"Chest"}""";

    private const string Settings = """{"gemTypes":["Ruby"]}""";

    private static StorageExportSource Reading(IReadOnlyDictionary<string, string> objects) =>
        new(new MemoryStorage(objects));

    [Fact]
    public async Task Two_prefixes_in_one_container_load_as_the_newer_and_the_one_beneath_it()
    {
        var source = Reading(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExportReader.Data("1000", ExportReader.EntitiesName)] = Older,
            [ExportReader.Data("1000", ExportReader.SettingsName)] = Settings,
            [ExportReader.Data("1001", ExportReader.EntitiesName)] = Newer,
            [ExportReader.Data("1001", ExportReader.SettingsName)] = Settings,
        });

        var history = await source.LoadAsync(Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("1001", history.Latest);
        Assert.Equal("Chest", Assert.IsType<ItemDocument>(Assert.Single(history.Snapshot.Entities)).Slot);

        var beneath = Assert.Single(history.Older).Value;

        Assert.Equal("1000", beneath.Build);
        Assert.Equal([Older], beneath.Documents[new DocumentKey("item", 5224)]);
    }

    [Fact]
    public async Task A_prefix_carrying_documents_without_settings_names_the_file_it_lacks()
    {
        var source = Reading(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ExportReader.Data("1001", ExportReader.EntitiesName)] = Newer,
        });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => source.LoadAsync(Xunit.TestContext.Current.CancellationToken));

        Assert.Contains(
            ExportReader.Data(ExportReader.SettingsName), failure.Message, StringComparison.Ordinal);
    }
}
