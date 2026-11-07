using System.Collections.Concurrent;

using Fellowship.SDK.Documents;

namespace Fellowship.Codex.Services;

public sealed record BuildNotes(string Build, string Previous, IReadOnlyList<PatchNote> Notes)
{
    private readonly HashSet<DocumentKey> noted = [.. Notes.Select(note => note.Key)];

    public bool Has(DocumentKey key) => noted.Contains(key);
}

public interface IPatchNotesService
{
    IReadOnlyList<string> Builds { get; }

    BuildNotes? Of(string build);

    DateTimeOffset? Date(string build);
}

/// <summary>Each build's patch notes, read with the export.</summary>
public sealed class PatchNotesService : IPatchNotesService
{
    private readonly ExportHistory history;

    private readonly IReadOnlyDictionary<string, BuildNotes> notes;

    private readonly ConcurrentDictionary<string, DateTimeOffset?> dates = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Builds { get; }

    public PatchNotesService(ExportHistory history, IExportHistoryService builds)
    {
        this.history = history;

        var every = builds.Builds;

        Builds = [.. every.SkipLast(1)];

        notes = every.Zip(every.Skip(1))
            .Where(pair => history.Notes.ContainsKey(pair.First))
            .ToDictionary(
                pair => pair.First,
                pair => new BuildNotes(pair.First, pair.Second, history.Notes[pair.First]),
                StringComparer.Ordinal);
    }

    public BuildNotes? Of(string build) => notes.GetValueOrDefault(build);

    public DateTimeOffset? Date(string build) =>
        dates.GetOrAdd(build, named => named == history.Latest
            ? history.Snapshot.Settings.BuildDate
            : history.Older.TryGetValue(named, out var version) ? Stated(version.Settings) : null);

    private static DateTimeOffset? Stated(string settings)
    {
        try
        {
            return settings.Length > 0 ? ExportReader.ReadSettings(settings).BuildDate : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
