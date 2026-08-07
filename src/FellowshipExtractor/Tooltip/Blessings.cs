using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Localization;
using CUE4Parse.UE4.Readers;

namespace FellowshipExtractor.Tooltip;

public sealed class Blessings
{
    private const string LocresPath = "fellowship/Content/Localization/Game/en/Game.locres";
    private const string TextNamespace = "ST_Abilities";
    private const string NameKey = "Abilities_Feature_Finesse_Name";
    private const string HeroBlessingKey = "Abilities_Tooltip_Finesse_HeroBlessing";
    private const string TierLevelKey = "Abilities_Tooltip_Finesse_TierLevel";

    private const string ConstantsNamespace = "Finesse";
    private const string ConstantsTableName = "FinesseConstants";

    private static readonly string[] Wanted = [NameKey, HeroBlessingKey, TierLevelKey];

    public BlessingEntry Wording { get; }

    public Blessings(AbstractFileProvider provider, ConstantsTable constants)
    {
        var shipped = Strings(provider);

        if (Wanted.Where(key => !shipped.ContainsKey(key)).ToArray() is { Length: > 0 } missing)
        {
            throw new InvalidOperationException(
                $"The {TextNamespace} locres namespace no longer holds {string.Join(" or ", missing)}, which is " +
                "where a blessing tooltip takes its heading and its tier line from, so the card would print words " +
                "this app chose rather than the ones the build ships.");
        }

        Wording = new BlessingEntry(
            shipped[NameKey], shipped[HeroBlessingKey], shipped[TierLevelKey], Tiers(constants));
    }

    private static Dictionary<string, string> Strings(AbstractFileProvider provider)
    {
        if (!provider.Files.TryGetValue(LocresPath, out var file))
        {
            throw new InvalidOperationException(
                $"{LocresPath} is missing. It is the only shipped source of what a blessing tooltip calls itself.");
        }

        using var archive = new FByteArchive(file.Path, file.Read(), provider.Versions);
        var locres = new FTextLocalizationResource(archive);

        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (textNamespace, entries) in locres.Entries)
        {
            if (!textNamespace.Str.Equals(TextNamespace, StringComparison.Ordinal)) continue;

            foreach (var (key, entry) in entries)
            {
                if (Wanted.Contains(key.Str, StringComparer.Ordinal)) found[key.Str] = entry.LocalizedString;
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
