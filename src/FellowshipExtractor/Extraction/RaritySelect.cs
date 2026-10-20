using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Kismet;

namespace FellowshipExtractor.Extraction;

internal static class RaritySelect
{
    public static IReadOnlyDictionary<string, string> Read(
        AbstractFileProvider provider,
        RarityNames rarities,
        string asset,
        string function,
        Func<KismetExpression, string?> read,
        string absence)
    {
        if (EnumSelect.Read(provider, asset, function, read, absence) is { Count: > 0 } found)
        {
            return ByRarity(found, rarities, asset, function);
        }

        throw new InvalidOperationException(
            $"{asset}.{function} selects nothing over the rarity ladder. {absence}");
    }

    private static Dictionary<string, string> ByRarity(
        Dictionary<int, string> byTier, RarityNames rarities, string asset, string function)
    {
        var byRarity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tier, value) in byTier)
        {
            if (rarities.Label(tier) is not { } name)
            {
                throw new InvalidOperationException(
                    $"{asset}.{function} answers rarity {tier}, and no loot rule set row names it, so the " +
                    "table it fills would key on nothing.");
            }

            byRarity[name] = value;
        }

        return byRarity;
    }
}
