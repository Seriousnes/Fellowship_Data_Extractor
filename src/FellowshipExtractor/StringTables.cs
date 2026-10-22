using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Objects.Core.i18N;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor;

public static class StringTables
{
    private static IReadOnlyDictionary<(string Table, string Key), string> _byEntry =
        new Dictionary<(string Table, string Key), string>();

    public static int Count => _byEntry.Count;

    public static void Read(AbstractFileProvider provider)
    {
        var byEntry = new Dictionary<(string Table, string Key), string>();

        foreach (var entry in LocalizedText.Read(provider, LocalizedText.DefaultCulture))
        {
            byEntry[(entry.Namespace, entry.Key)] = entry.Text;
        }

        _byEntry = byEntry;
    }

    public static string? Find(string table, string key) =>
        _byEntry.TryGetValue((table, key), out var text) ? text : null;

    public static IEnumerable<(string Key, string Text)> In(string table) =>
        _byEntry.Where(entry => entry.Key.Table == table).Select(entry => (entry.Key.Key, entry.Value));

    public static string? Resolve(FText text) =>
        text.TextHistory is FTextHistory.StringTableEntry entry
            ? Find(entry.TableId.Text, entry.Key)
            : text.Text;
}
