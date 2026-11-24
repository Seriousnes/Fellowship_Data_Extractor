using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

/// <summary>
/// A build's documents as the first export of it to record them, one entry per line in
/// <see cref="ExtractionChanges.FileName" /> beside its entities.jsonl. An entry with a <see cref="Label" /> has the
/// field's first value, and <see cref="Older" /> is null where the field is absent. An entry with
/// <see cref="Absent" /> set is a document absent from that first export.
/// </summary>
public sealed record ExtractionChange(string Slug, int Id, string? Label, string? Older, bool Absent);

/// <summary>
/// Changes a new extractor makes to the documents of a build that already has an export. Two exports of one build
/// read the same game data, so each difference between them is an extraction change, and patch notes leave it out.
/// </summary>
public static class ExtractionChanges
{
    public const string FileName = "extraction-changes.jsonl";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    public static IReadOnlyList<ExtractionChange> Read(string folder)
    {
        var file = Path.Combine(folder, FileName);
        if (!File.Exists(file)) return [];

        return [.. File.ReadLines(file)
            .Where(line => line.Length > 0)
            .Select(line => JsonSerializer.Deserialize<ExtractionChange>(line, Json)
                ?? throw new InvalidOperationException($"A line of {file} deserialised to nothing."))];
    }

    /// <summary>
    /// Adds the first value of each field <paramref name="newer" /> changes from <paramref name="previous" /> to the
    /// folder's record. A field the record has keeps its value.
    /// </summary>
    public static int Record(
        string folder,
        IReadOnlyDictionary<DocumentKey, IReadOnlyList<ReadOnlyMemory<byte>>> previous,
        IReadOnlyDictionary<DocumentKey, IReadOnlyList<ReadOnlyMemory<byte>>> newer)
    {
        var entries = Read(folder).ToList();
        var absent = entries.Where(entry => entry.Absent).Select(entry => (entry.Slug, entry.Id)).ToHashSet();
        var recorded = entries.Select(entry => (entry.Slug, entry.Id)).ToHashSet();
        var labels = entries
            .Where(entry => entry.Label is not null)
            .Select(entry => (entry.Slug, entry.Id, entry.Label!))
            .ToHashSet();

        var added = 0;

        foreach (var note in PatchNoteDiff.Between(previous, newer))
        {
            var key = (note.Key.Slug, note.Key.Id);

            if (note.Added)
            {
                if (!recorded.Add(key)) continue;

                absent.Add(key);
                entries.Add(new ExtractionChange(note.Key.Slug, note.Key.Id, null, null, Absent: true));
                added++;
                continue;
            }

            if (absent.Contains(key)) continue;

            foreach (var field in note.Fields)
            {
                if (!labels.Add((note.Key.Slug, note.Key.Id, field.Label))) continue;

                recorded.Add(key);
                entries.Add(new ExtractionChange(note.Key.Slug, note.Key.Id, field.Label, field.Older, Absent: false));
                added++;
            }
        }

        if (added == 0) return 0;

        var text = new StringBuilder();

        foreach (var entry in entries
            .OrderBy(entry => entry.Slug, StringComparer.Ordinal)
            .ThenBy(entry => entry.Id)
            .ThenBy(entry => entry.Label, StringComparer.Ordinal))
        {
            text.Append(JsonSerializer.Serialize(entry, Json)).Append('\n');
        }

        File.WriteAllText(Path.Combine(folder, FileName), text.ToString());

        return added;
    }

    /// <summary>
    /// The notes without each field whose older value is the newer build's first recorded value, and without each
    /// added document the newer build's first export is recorded without.
    /// </summary>
    public static IReadOnlyList<PatchNote> Without(IReadOnlyList<PatchNote> notes, IReadOnlyList<ExtractionChange> changes)
    {
        if (changes.Count == 0) return notes;

        var absent = changes.Where(entry => entry.Absent).Select(entry => (entry.Slug, entry.Id)).ToHashSet();
        var first = changes
            .Where(entry => entry.Label is not null)
            .ToDictionary(entry => (entry.Slug, entry.Id, entry.Label!), entry => entry.Older);

        var kept = new List<PatchNote>(notes.Count);

        foreach (var note in notes)
        {
            if (note.Added)
            {
                if (!absent.Contains((note.Key.Slug, note.Key.Id))) kept.Add(note);
                continue;
            }

            var fields = note.Fields
                .Where(field => !first.TryGetValue((note.Key.Slug, note.Key.Id, field.Label), out var older) ||
                                older != field.Older)
                .ToArray();

            if (fields.Length == note.Fields.Count) kept.Add(note);
            else if (fields.Length > 0) kept.Add(new PatchNote(note.Key, note.Name, note.Icon, false, fields));
        }

        return kept;
    }
}
