using System.Collections.Concurrent;
using System.Text;

using Fellowship.Codex.Models;

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

public sealed class PatchNotesService : IPatchNotesService
{
    private readonly ExportHistory history;

    private readonly Lazy<IReadOnlyDictionary<DocumentKey, IReadOnlyList<string>>> latest;

    private readonly ConcurrentDictionary<string, Lazy<BuildNotes>> notes = new(StringComparer.Ordinal);

    private readonly IReadOnlyList<string> every;

    private readonly ConcurrentDictionary<string, DateTimeOffset?> dates = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Builds { get; }

    public PatchNotesService(ExportHistory history, IExportHistoryService builds)
    {
        this.history = history;

        every = builds.Builds;
        Builds = [.. every.SkipLast(1)];

        latest = new(() => ExportReader.Group(ExportReader.Lines(Encoding.UTF8.GetString(history.Files.Entities.Span))));
    }

    public BuildNotes? Of(string build)
    {
        var at = Position(build);

        if (at < 0 || at + 1 >= every.Count) return null;

        return notes.GetOrAdd(every[at], named => new Lazy<BuildNotes>(() =>
        {
            var previous = every[at + 1];

            return new BuildNotes(named, previous, PatchNoteDiff.Between(Documents(previous), Documents(named)));
        })).Value;
    }

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

    private int Position(string build)
    {
        for (var at = 0; at < every.Count; at++)
        {
            if (string.Equals(every[at], build, StringComparison.Ordinal)) return at;
        }

        return -1;
    }

    private IReadOnlyDictionary<DocumentKey, IReadOnlyList<string>> Documents(string build) =>
        build == history.Latest ? latest.Value : history.Older[build].Documents;
}
