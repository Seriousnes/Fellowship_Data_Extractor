using FellowshipExtractor.Output;
using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Tests;

public sealed class ExtractionChangeTests
{
    private const string Sword = """{"$type":"ability","id":1112,"name":"Sword","cooldown":45}""";
    private const string SwordWithout = """{"$type":"ability","id":1112,"name":"Sword"}""";
    private const string SwordAt30 = """{"$type":"ability","id":1112,"name":"Sword","cooldown":30}""";
    private const string Shield = """{"$type":"ability","id":7,"name":"Shield"}""";

    /// <summary>Holds the cooldown field in the shape, so a document without it reads as a removal.</summary>
    private const string Bow = """{"$type":"ability","id":8,"name":"Bow","cooldown":10}""";

    [Fact]
    public async Task A_field_the_extractor_removes_is_absent_from_the_patch_notes()
    {
        var notes = await NotesAsync(older: Sword, firstExport: Sword, reExport: SwordWithout);

        Assert.Empty(notes);
    }

    [Fact]
    public async Task A_field_the_game_changed_is_in_the_patch_notes()
    {
        var notes = await NotesAsync(older: SwordAt30, firstExport: Sword, reExport: SwordWithout);

        var field = Assert.Single(Assert.Single(notes).Fields);
        Assert.Equal("30", field.Older);
        Assert.Null(field.Newer);
    }

    [Fact]
    public async Task A_document_the_extractor_adds_is_absent_from_the_patch_notes()
    {
        var notes = await NotesAsync(older: Sword, firstExport: Sword, reExport: Sword + "\n" + Shield);

        Assert.Empty(notes);
    }

    [Fact]
    public async Task A_later_change_to_a_recorded_field_keeps_its_first_value()
    {
        var output = Directory.CreateTempSubdirectory().FullName;

        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(output, ExportFolders.Name("1001"))).FullName;

            ExtractionChanges.Record(folder, Group(Sword), Group(SwordWithout));
            ExtractionChanges.Record(folder, Group(SwordWithout), Group(SwordAt30));

            var change = Assert.Single(ExtractionChanges.Read(folder));
            Assert.Equal("45", change.Older);
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static async Task<IReadOnlyList<PatchNote>> NotesAsync(string older, string firstExport, string reExport)
    {
        var output = Directory.CreateTempSubdirectory().FullName;

        try
        {
            await WriteAsync(output, "1000", older);
            var folder = await WriteAsync(output, "1001", reExport);

            ExtractionChanges.Record(folder, Group(firstExport), Group(reExport));
            await PatchNoteWriter.WriteAsync(output, CancellationToken.None);

            return PatchNotesFile.Read(await File.ReadAllTextAsync(Path.Combine(folder, PatchNotesFile.FileName)));
        }
        finally
        {
            Directory.Delete(output, recursive: true);
        }
    }

    private static async Task<string> WriteAsync(string output, string build, string lines)
    {
        var folder = Directory.CreateDirectory(Path.Combine(output, ExportFolders.Name(build))).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, ExportLayout.EntitiesFile), lines + "\n" + Bow + "\n");
        return folder;
    }

    private static IReadOnlyDictionary<DocumentKey, IReadOnlyList<ReadOnlyMemory<byte>>> Group(string lines) =>
        ExportLines.Group(System.Text.Encoding.UTF8.GetBytes(lines + "\n" + Bow + "\n"));
}
