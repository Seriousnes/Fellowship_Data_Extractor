using System.Text;

namespace Fellowship.Codex.Tests;

public class PatchNotesFileTests
{
    private const string Settings = """{"gemTypes":["Ruby"]}""";

    private const string Older = """{"$type":"item","id":5224,"name":"Sunforged Greatsword","slot":"Back"}""";

    private const string Newer = """{"$type":"item","id":5224,"name":"Sunforged Greatsword","slot":"Chest"}""";

    private static readonly PatchNote Note = new(
        new DocumentKey("item", 5224), "Sunforged Greatsword", null, false, [new PatchNoteField("Slot", "Back", "Chest")]);

    private static ExportHistory History(IReadOnlyDictionary<string, IReadOnlyList<PatchNote>> notes) =>
        new(
            "1001",
            new ExportSnapshot(new SettingsFile(), []),
            new ExportFiles(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty),
            new Dictionary<string, ExportVersion>
            {
                ["1000"] = new("1000", ExportText.Group([Older]), "{}"),
            },
            notes);

    private static PatchNotesService Service(ExportHistory history) => new(history, new ExportHistoryService(history));

    [Fact]
    public void A_note_reads_back_as_written()
    {
        var read = Assert.Single(PatchNotesFile.Read(PatchNotesFile.Write([Note])));

        Assert.Equal(Note.Key, read.Key);
        Assert.Equal(Note.Name, read.Name);
        Assert.Null(read.Icon);
        Assert.False(read.Added);
        Assert.Equal(Note.Fields, read.Fields);
    }

    [Fact]
    public void An_empty_file_reads_as_no_notes()
    {
        Assert.Empty(PatchNotesFile.Read(PatchNotesFile.Write([])));
    }

    [Fact]
    public void Lines_slice_the_file_with_either_line_ending()
    {
        var grouped = ExportLines.Group(Encoding.UTF8.GetBytes($"{Older}\r\n{Newer}\n"));

        Assert.Equal([Older, Newer], ExportText.Read(grouped[new DocumentKey("item", 5224)]));
    }

    [Fact]
    public void A_build_names_the_build_before_it_and_its_noted_entities()
    {
        var notes = Service(History(new Dictionary<string, IReadOnlyList<PatchNote>> { ["1001"] = [Note] })).Of("1001");

        Assert.NotNull(notes);
        Assert.Equal("1000", notes.Previous);
        Assert.True(notes.Has(Note.Key));
        Assert.False(notes.Has(new DocumentKey("item", 1)));
    }

    [Fact]
    public void The_oldest_build_and_a_build_with_no_file_have_no_notes()
    {
        var service = Service(History(new Dictionary<string, IReadOnlyList<PatchNote>>()));

        Assert.Null(service.Of("1001"));
        Assert.Null(service.Of("1000"));
        Assert.Equal(["1001"], service.Builds);
    }

    [Fact]
    public async Task A_folder_reads_each_builds_file()
    {
        var output = Directory.CreateTempSubdirectory().FullName;

        try
        {
            foreach (var (build, line) in new[] { ("1000", Older), ("1001", Newer) })
            {
                var folder = Directory.CreateDirectory(Path.Combine(output, ExportFolders.Name(build))).FullName;

                File.WriteAllText(Path.Combine(folder, ExportReader.EntitiesName), line);
                File.WriteAllText(Path.Combine(folder, ExportReader.SettingsName), Settings);
            }

            var newest = Path.Combine(output, ExportFolders.Name("1001"));

            File.WriteAllText(Path.Combine(newest, PatchNotesFile.FileName), PatchNotesFile.Write([Note]));

            var history = await new FolderExportSource(newest).LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(Note.Key, Assert.Single(history.Notes["1001"]).Key);
            Assert.False(history.Notes.ContainsKey("1000"));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void The_validator_follows_the_image_and_the_export_alone()
    {
        var export = new ExportFiles(Encoding.UTF8.GetBytes(Older), Encoding.UTF8.GetBytes(Settings));
        var changed = new ExportFiles(Encoding.UTF8.GetBytes(Newer), Encoding.UTF8.GetBytes(Settings));

        var tag = CrawlerValidators.Tag("abc1234", export);

        Assert.Equal(tag, CrawlerValidators.Tag("abc1234", export));
        Assert.NotEqual(tag, CrawlerValidators.Tag("def5678", export));
        Assert.NotEqual(tag, CrawlerValidators.Tag("abc1234", changed));
    }
}
