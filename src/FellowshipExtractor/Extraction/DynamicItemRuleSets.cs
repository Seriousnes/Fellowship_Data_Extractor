using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class DynamicItemRuleSets
{
    public const string RowStructure = "/Script/DynamicLootSystemRuntime.CRDynamicHeroItemLootRarityRuleSet";

    private const string RuleSetsField = "RuleSets";
    private const string GuaranteedField = "AdditionalGuaranteedSlots";
    private const string GemSocketSlot = "GemSocket";

    private readonly HashSet<string> _withGemSocket = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _withGemSocket.Count;

    public DynamicItemRuleSets(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (!Entries(row, GuaranteedField).Any(
                    slot => slot.Equals(GemSocketSlot, StringComparison.OrdinalIgnoreCase))) continue;

                foreach (var ruleSet in Entries(row, RuleSetsField)) _withGemSocket.Add(ruleSet);
            }
        }
    }

    public bool HasGemSocket(string? ruleSet) => ruleSet is not null && _withGemSocket.Contains(ruleSet);

    private static IEnumerable<string> Entries(IPropertyHolder row, string field)
    {
        if (PropertyWalk.Get(row, field)?.GenericValue is not UScriptArray array) yield break;

        foreach (var element in array.Properties)
        {
            if (PropertyWalk.AsString(element?.GenericValue) is { } text) yield return text;
        }
    }
}
