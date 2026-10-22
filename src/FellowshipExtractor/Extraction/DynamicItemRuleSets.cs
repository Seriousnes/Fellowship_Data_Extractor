using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class DynamicItemRuleSets
{
    public const string RowStructure = "/Script/DynamicLootSystemRuntime.CRDynamicHeroItemLootRarityRuleSet";
    public const string DefinitionClass = "CRDynamicHeroItemDefinition";
    public const string EnabledField = "EnabledInGame";

    private const string RuleSetsField = "RuleSets";
    private const string GuaranteedField = "AdditionalGuaranteedSlots";
    private const string GemSocketSlot = "GemSocket";
    private const string ItemRuleSetField = "RuleSet";

    private readonly HashSet<string> _withGemSocket = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedSet<int>> _live = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _withGemSocket.Count;

    public int Disabled { get; }

    public DynamicItemRuleSets(
        AbstractFileProvider provider, IReadOnlyList<FAssetData> assets, RarityNames rarities, RarityDropChances drops)
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

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(DefinitionClass, StringComparison.OrdinalIgnoreCase)) continue;

            if (!provider.TryLoadPackageObject<UObject>(
                AbilityPresentationIndex.PackagePath(asset.ObjectPath), out var definition)) continue;

            if (PropertyWalk.Get(definition, EnabledField)?.GenericValue is false)
            {
                Disabled++;
                continue;
            }

            if (PropertyWalk.GetString(definition, ItemRuleSetField) is not { } ruleSet) continue;

            if (!_live.TryGetValue(ruleSet, out var tiers)) _live[ruleSet] = tiers = [];

            tiers.UnionWith(DynamicItemVisuals.Tiers(definition, rarities, drops));
        }

        if (_live.Count == 0)
        {
            throw new InvalidOperationException(
                $"No enabled {DefinitionClass} names a rule set, so no loot rule set row is one an item rolls on.");
        }
    }

    public bool HasGemSocket(string? ruleSet) => ruleSet is not null && _withGemSocket.Contains(ruleSet);

    public bool Rolls(string ruleSet, int tier) => _live.TryGetValue(ruleSet, out var tiers) && tiers.Contains(tier);

    public IEnumerable<(string RuleSet, int Tier)> Rolled =>
        _live.SelectMany(entry => entry.Value.Select(tier => (entry.Key, tier)));

    private static IEnumerable<string> Entries(IPropertyHolder row, string field)
    {
        if (PropertyWalk.Get(row, field)?.GenericValue is not UScriptArray array) yield break;

        foreach (var element in array.Properties)
        {
            if (PropertyWalk.AsString(element?.GenericValue) is { } text) yield return text;
        }
    }
}
