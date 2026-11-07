using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

/// <summary>Writes each build's patch notes against the build before it in the output folder.</summary>
public static class PatchNoteWriter
{
    public static async Task WriteAsync(string output, CancellationToken cancellationToken)
    {
        var folders = new DirectoryInfo(output)
            .EnumerateDirectories(ExportFolders.Pattern)
            .Where(folder => ExportFolders.Build(folder.Name) >= 0 &&
                             File.Exists(Path.Combine(folder.FullName, ExportLayout.EntitiesFile)))
            .OrderByDescending(folder => ExportFolders.Build(folder.Name))
            .ToArray();

        foreach (var (newer, older) in folders.Zip(folders.Skip(1)))
        {
            var notes = PatchNoteDiff.Between(await GroupAsync(older, cancellationToken), await GroupAsync(newer, cancellationToken));

            await File.WriteAllTextAsync(Path.Combine(newer.FullName, PatchNotesFile.FileName), PatchNotesFile.Write(notes), cancellationToken);

            Console.WriteLine($"{newer.Name}: {notes.Count} patch notes against {older.Name}.");
        }
    }

    private static async Task<IReadOnlyDictionary<DocumentKey, IReadOnlyList<ReadOnlyMemory<byte>>>> GroupAsync(
        DirectoryInfo folder, CancellationToken cancellationToken) =>
        ExportLines.Group(await File.ReadAllBytesAsync(Path.Combine(folder.FullName, ExportLayout.EntitiesFile), cancellationToken));
}
