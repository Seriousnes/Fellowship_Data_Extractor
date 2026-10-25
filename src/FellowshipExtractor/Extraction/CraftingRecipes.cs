using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed record CraftingRecipeRow(string RowName, IPropertyHolder Row);

public sealed class CraftingRecipes
{
    public const string RowStructure = AugmentationCategoryNames.RowStructure;

    private const string RecipeIdField = "RecipeID";
    private const string RecipeNameField = "RecipeName";
    private const string EnabledField = "EnabledInGame";
    private const string CategoryField = "Category";
    private const string DescriptionField = "OutputDescription";
    private const string VendorField = "AvailableAtVendor";
    private const string GoldCostField = "GoldCost";
    private const string DynamicGoldCostField = "UseItemLevelDynamicGoldCost";
    private const string CostField = "CraftingCost";
    private const string CurrencyField = "Currency";
    private const string AmountField = "Amount";
    private const string InputsField = "NumInputItemsRequired";
    private const string CurrencyTable = "ItemID";

    private static readonly Dictionary<string, string?> Vendors = new(StringComparer.Ordinal)
    {
        ["Vendor.Id.Inscriber"] = "Shrine of Infusion",
        ["Vendor.Id.Jeweler"] = "Gem Removal Table",
        ["Vendor.Id.Crafter"] = "Master Craftsman",
        ["Vendor.Id.Blacksmith"] = null,
    };

    public IReadOnlyList<CraftingRecipeRow> Rows { get; }

    public IReadOnlyList<CraftingRecipeEntry> All { get; }

    public CraftingRecipes(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        IReadOnlyList<TagRecord> tags,
        ItemTableNames currencies)
    {
        var rows = new List<CraftingRecipeRow>();

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (PropertyWalk.Get(row, EnabledField)?.GenericValue is not true) continue;

                rows.Add(new CraftingRecipeRow(rowName.Text, row));
            }
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException($"No enabled {RowStructure} row loads, so no crafting recipe is known.");
        }

        Rows = rows;
        All = [.. rows.Select(row => Entry(row, tags, currencies))];
    }

    private static CraftingRecipeEntry Entry(
        CraftingRecipeRow recipe, IReadOnlyList<TagRecord> tags, ItemTableNames currencies)
    {
        var row = recipe.Row;
        var id = PropertyWalk.GetString(row, RecipeIdField) ?? recipe.RowName;

        var name = PropertyWalk.GetText(row, RecipeNameField) is { Length: > 0 } named
            ? named
            : throw new InvalidOperationException($"Recipe {id} has no {RecipeNameField}.");

        return new CraftingRecipeEntry(id, name)
        {
            Category = PropertyWalk.GetText(row, CategoryField) is { Length: > 0 } category ? category : null,
            Description = PropertyWalk.GetText(row, DescriptionField) is { Length: > 0 } description
                ? description
                : null,
            Vendor = Vendor(id, PropertyWalk.GetString(row, VendorField)),
            GoldCost = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, GoldCostField)?.GenericValue) ?? 0,
            GoldCostScalesWithItemLevel = PropertyWalk.Get(row, DynamicGoldCostField)?.GenericValue is true,
            Costs = Costs(id, row, tags, currencies),
            Inputs = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, InputsField)?.GenericValue) ?? 1,
        };
    }

    private static string? Vendor(string id, string? tag)
    {
        if (tag is null) return null;

        return Vendors.TryGetValue(tag, out var vendor)
            ? vendor
            : throw new InvalidOperationException(
                $"Recipe {id} is available at {tag}, and no vendor name is known for it. The vendors named are " +
                $"{string.Join(", ", Vendors.Keys)}.");
    }

    private static IReadOnlyList<RecipeCost> Costs(
        string id, IPropertyHolder row, IReadOnlyList<TagRecord> tags, ItemTableNames currencies)
    {
        if (PropertyWalk.Get(row, CostField)?.GenericValue is not UScriptArray array) return [];

        var costs = new List<RecipeCost>();

        foreach (var element in array.Properties)
        {
            if (element?.GenericValue is not FScriptStruct { StructType: IPropertyHolder cost })
            {
                throw new InvalidOperationException($"Recipe {id} has a {CostField} entry that holds no struct.");
            }

            var tag = PropertyWalk.GetString(cost, CurrencyField)
                ?? throw new InvalidOperationException($"Recipe {id} has a {CostField} entry naming no {CurrencyField}.");

            if (currencies.Find(tag) is null)
            {
                throw new InvalidOperationException(
                    $"Recipe {id} costs {tag}, and no {ItemTableNames.CurrencyRowStructure} row defines it.");
            }

            var currency = tags.FirstOrDefault(record =>
                record.Table.Equals(CurrencyTable, StringComparison.OrdinalIgnoreCase) &&
                record.Name.Equals(tag, StringComparison.OrdinalIgnoreCase));

            if (currency.Name is null)
            {
                throw new InvalidOperationException(
                    $"Recipe {id} costs {tag}, and no {CurrencyTable} tag has that name, so the currency has no id.");
            }

            costs.Add(new RecipeCost(
                currency.NetId,
                PropertyWalk.AsOrdinal(PropertyWalk.Get(cost, AmountField)?.GenericValue)
                    ?? throw new InvalidOperationException($"Recipe {id} costs {tag} with no {AmountField}.")));
        }

        return costs;
    }
}
