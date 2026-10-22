using System.Text.RegularExpressions;

using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Objects.Core.i18N;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed record ModifierLimit(int Maximum, string Warning);

public sealed partial class ModifierRecipes
{
    public const string Implementation =
        "/Script/DynamicLootSystemRuntime.CRRecipe_TargetSpecificSlotWithSpecificModifier";

    private const string RecipeIdField = "RecipeID";
    private const string RecipeNameField = "RecipeName";
    private const string ImplementationField = "RecipeImplementation";
    private const string DataField = "RecipeSpecificData";
    private const string EnabledField = "EnabledInGame";
    private const string CategoryField = "Category";
    private const string SlotIndexField = "SlotIndex";
    private const string ModifierTypeField = "ModifierType";
    private const string OptionsField = "NumberOfOutputOptions";
    private const string AllOptionsField = "GenerateAllPossibleOutputOptions";
    private const string WarningSuffix = "_MaximumLimitWarning";

    private readonly Dictionary<string, List<ModifierRecipe>> _bySlot = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<(string Table, string Key)>> _names = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ModifierLimit> _limits = new(StringComparer.Ordinal);

    public int Count => _bySlot.Values.Sum(recipes => recipes.Count);

    public ModifierRecipes(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, TraitCategoryNames.RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, ImplementationField) != Implementation) continue;
                if (PropertyWalk.Get(row, EnabledField)?.GenericValue is not true) continue;

                if (PropertyWalk.Get(row, DataField)?.GenericValue is not FScriptStruct
                    {
                        StructType: FInstancedStruct { NonConstStruct: IPropertyHolder data },
                    })
                {
                    throw new InvalidOperationException(
                        $"Recipe {rowName.Text} is a {Implementation} and its {DataField} holds no struct, so " +
                        "nothing states which slot it rerolls.");
                }

                var slot = PropertyWalk.GetString(data, ModifierTypeField)
                    ?? throw new InvalidOperationException(
                        $"Recipe {rowName.Text} names no {ModifierTypeField}, so nothing states which slot it " +
                        "rerolls.");

                var recipe = new ModifierRecipe(
                    PropertyWalk.GetString(row, RecipeIdField) ?? rowName.Text,
                    PropertyWalk.AsOrdinal(PropertyWalk.Get(data, SlotIndexField)?.GenericValue) ?? 0)
                {
                    Category = PropertyWalk.GetText(row, CategoryField) is { Length: > 0 } category ? category : null,
                    Options = PropertyWalk.AsOrdinal(PropertyWalk.Get(data, OptionsField)?.GenericValue) ?? 0,
                    AllOptions = PropertyWalk.Get(data, AllOptionsField)?.GenericValue is true,
                };

                if (!_bySlot.TryGetValue(slot, out var recipes)) _bySlot[slot] = recipes = [];
                recipes.Add(recipe);

                if (PropertyWalk.Get(row, RecipeNameField)?.GenericValue is FText
                    {
                        TextHistory: FTextHistory.StringTableEntry entry,
                    })
                {
                    if (!_names.TryGetValue(slot, out var names)) _names[slot] = names = [];
                    names.Add((entry.TableId.Text, entry.Key));
                }
            }
        }

        if (_bySlot.Count == 0)
        {
            throw new InvalidOperationException(
                $"No enabled {TraitCategoryNames.RowStructure} row is a {Implementation}, so no modifier slot " +
                "states the recipes that reroll it.");
        }

        foreach (var (slot, names) in _names)
        {
            if (Limit(slot, names) is { } limit) _limits[slot] = limit;
        }
    }

    public IReadOnlyCollection<string> Slots => _bySlot.Keys;

    public IReadOnlyList<ModifierRecipe> For(string slot) => _bySlot.TryGetValue(slot, out var recipes) ? recipes : [];

    public ModifierLimit? LimitOf(string slot) => _limits.GetValueOrDefault(slot);

    private static ModifierLimit? Limit(string slot, IReadOnlyList<(string Table, string Key)> names)
    {
        var warnings = names
            .Select(name => name.Table)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(table => StringTables.In(table))
            .Where(entry => entry.Key.EndsWith(WarningSuffix, StringComparison.Ordinal))
            .Where(entry => names.All(name =>
                name.Key.StartsWith(entry.Key[..^WarningSuffix.Length] + "_", StringComparison.Ordinal)))
            .ToArray();

        if (warnings.Length == 0) return null;

        if (warnings.Length > 1)
        {
            throw new InvalidOperationException(
                $"The recipes rerolling {slot} share the stem of {warnings.Length} {WarningSuffix} strings: " +
                $"{string.Join(", ", warnings.Select(warning => warning.Key))}.");
        }

        var (key, text) = warnings[0];
        var numbers = Number().Matches(text);

        if (numbers.Count != 1)
        {
            throw new InvalidOperationException(
                $"{key} reads \"{text}\", and the limit is read as its one number.");
        }

        return new ModifierLimit(int.Parse(numbers[0].Value, System.Globalization.CultureInfo.InvariantCulture), text);
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Number();
}
