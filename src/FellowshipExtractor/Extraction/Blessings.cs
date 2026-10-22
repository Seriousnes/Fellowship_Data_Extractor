using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Readers;

namespace FellowshipExtractor.Extraction;

public sealed class Blessings
{
    private const string LocresPath = "fellowship/Content/Localization/Game/en/Game.locres";

    public static readonly (string Namespace, string Key) NameKey = ("ST_Items", "Item_Feature_Glyph_Name");
    public static readonly (string Namespace, string Key) TierLevelKey = ("ST_Abilities", "Abilities_Tooltip_Finesse_TierLevel");

    private const string ConstantsNamespace = "Finesse";
    private const string ConstantsTableName = "FinesseConstants";

    private static readonly (string Namespace, string Key)[] Wanted = [NameKey, TierLevelKey];

    public BlessingEntry Wording { get; }

    public Blessings(AbstractFileProvider provider, ConstantsTable constants)
    {
        var shipped = Strings(provider);

        if (Wanted.Where(entry => !shipped.ContainsKey(entry)).ToArray() is { Length: > 0 } missing)
        {
            throw new InvalidOperationException(
                $"The locres no longer holds {string.Join(" or ", missing.Select(entry => $"{entry.Namespace}/{entry.Key}"))}. " +
                "A blessing tooltip takes its heading and its tier line from those keys, so the card would print " +
                "words this app chose rather than the ones in the build.");
        }

        Wording = new BlessingEntry(shipped[NameKey], shipped[TierLevelKey], Tiers(constants));
    }

    private static Dictionary<(string Namespace, string Key), string> Strings(AbstractFileProvider provider)
    {
        if (!provider.Files.TryGetValue(LocresPath, out var file))
        {
            throw new InvalidOperationException(
                $"{LocresPath} is missing. It is the only shipped source of what a blessing tooltip calls itself.");
        }

        using var archive = new FByteArchive(file.Path, file.Read(), provider.Versions);
        var locres = new FTextLocalizationResource(archive);

        var found = new Dictionary<(string Namespace, string Key), string>();

        foreach (var (textNamespace, entries) in locres.Entries)
        {
            foreach (var (key, entry) in entries)
            {
                if (Wanted.Contains((textNamespace.Str, key.Str))) found[(textNamespace.Str, key.Str)] = entry.LocalizedString;
            }
        }

        return found;
    }

    private static int Tiers(ConstantsTable constants)
    {
        var widths = constants
            .AllUnder(ConstantsNamespace)
            .Where(record => record.Table.Equals(ConstantsTableName, StringComparison.OrdinalIgnoreCase))
            .Select(record => record.Columns.Length)
            .Distinct()
            .ToArray();

        if (widths is not [var tiers])
        {
            throw new InvalidOperationException(
                $"{ConstantsTableName} states {(widths.Length == 0 ? "no" : "more than one")} tier ladder " +
                $"({string.Join(", ", widths.Order())}), so nothing says how many tiers a blessing has.");
        }

        return tiers;
    }
}
