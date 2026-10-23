using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Readers;

namespace FellowshipExtractor.Extraction;

public sealed class GlyphWording
{
    private const string LocresPath = "fellowship/Content/Localization/Game/en/Game.locres";

    public static readonly (string Namespace, string Key) NameKey = ("ST_Items", "Item_Feature_Glyph_Name");
    public static readonly (string Namespace, string Key) AugmentationKey = ("ST_Items", "Item_Feature_Augmentation_Name");
    public static readonly (string Namespace, string Key) TierLevelKey = ("ST_Abilities", "Abilities_Tooltip_Finesse_TierLevel");
    public static readonly (string Namespace, string Key) TierSourcesKey = ("ST_Abilities", "Abilities_Tooltip_Finesse_TierSources");

    private const string ConstantsNamespace = "Finesse";
    private const string ConstantsTableName = "FinesseConstants";

    public static readonly (string Namespace, string Key)[] Wanted = [NameKey, AugmentationKey, TierLevelKey, TierSourcesKey];

    public GlyphEntry Wording { get; }

    public GlyphWording(AbstractFileProvider provider, ConstantsTable constants)
    {
        var shipped = Strings(provider);

        if (Wanted.Where(entry => !shipped.ContainsKey(entry)).ToArray() is { Length: > 0 } missing)
        {
            throw new InvalidOperationException(
                $"The locres no longer holds {string.Join(" or ", missing.Select(entry => $"{entry.Namespace}/{entry.Key}"))}. " +
                "A glyph tooltip takes its heading and its tier line from those keys, so the card would print " +
                "words this app chose rather than the ones in the build.");
        }

        Wording = new GlyphEntry(
            shipped[NameKey], shipped[AugmentationKey], shipped[TierLevelKey], shipped[TierSourcesKey], Tiers(constants));
    }

    private static Dictionary<(string Namespace, string Key), string> Strings(AbstractFileProvider provider)
    {
        if (!provider.Files.TryGetValue(LocresPath, out var file))
        {
            throw new InvalidOperationException(
                $"{LocresPath} is missing. It is the only shipped source of what a glyph tooltip calls itself.");
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
                $"({string.Join(", ", widths.Order())}), so nothing says how many tiers a glyph has.");
        }

        return tiers;
    }
}
