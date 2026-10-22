using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed record ItemTrait(string DisplayName)
{
    public string? Description { get; init; }

    public string? CategoryTag { get; init; }

    public string? Category { get; init; }

    public int? MaxRank { get; init; }

    public int? RandomChance { get; init; }

    public int? Augmentation { get; init; }

    public string? AugmentationPath { get; init; }

    public bool NameFromAugmentation { get; init; }

    public string? IconPath { get; init; }

    public IReadOnlyList<RankRun>? Runs { get; init; }
}

public sealed class ItemTraits
{
    public const string RowStructure = "/Script/fellowship.CRItemTraitsDefinitionRow";
    public const string Table = "ItemTrait.ID";

    private const string TagField = "TraidID";
    private const string CategoryField = "TraitTags";
    private const string DisplayNameField = "DisplayName";
    private const string DescriptionField = "Description";
    private const string IconField = "TraitIcon";
    private const string MaxRankField = "MaxTraitRank";
    private const string RandomChanceField = "RandomChance";
    private const string AugmentationField = "Augmentation";
    private const string NameFromAugmentationField = "UseNameAndDescriptionFromAugmentation";

    private readonly Dictionary<string, ItemTrait> _byTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, ItemTrait> _byAugmentation = [];
    private readonly HashSet<int> _augmentations = [];

    public int Count => _byTag.Count;

    public int BoundRuns { get; private set; }

    public int TotalRuns { get; private set; }

    public ItemTraits(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        ConstantsTable constants,
        AugmentationIds augmentations,
        TraitCategoryNames categories)
    {
        var rows = new List<(string Tag, FStructFallback Row, string? Augmentation)>();

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, TagField) is not { } tag) continue;
                if (PropertyWalk.GetString(row, DisplayNameField) is null) continue;

                rows.Add((tag, row, PropertyWalk.GetString(row, AugmentationField)));
            }
        }

        var scopes = Scopes(rows, constants);

        foreach (var (tag, row, augmentation) in rows)
        {
            var description = PropertyWalk.GetText(row, DescriptionField);
            var runs = RankRuns.For(description, scopes.TryGetValue(tag, out var scope) ? scope : []);
            var categoryTag = PropertyWalk.GetString(row, CategoryField);

            TotalRuns += runs?.Count ?? 0;
            BoundRuns += runs?.Count(run => run.Key is not null) ?? 0;

            var augmentationId = augmentations.For(augmentation);

            _byTag[tag] = new ItemTrait(PropertyWalk.GetString(row, DisplayNameField)!)
            {
                Description = description,
                CategoryTag = categoryTag,
                Category = categories.Label(categoryTag),
                MaxRank = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, MaxRankField)?.GenericValue),
                RandomChance = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, RandomChanceField)?.GenericValue),
                Augmentation = augmentationId,
                AugmentationPath = augmentation,
                NameFromAugmentation = PropertyWalk.Get(row, NameFromAugmentationField)?.GenericValue is true,
                IconPath = PropertyWalk.GetString(row, IconField),
                Runs = runs,
            };
        }

        foreach (var trait in _byTag.Values)
        {
            if (trait.Augmentation is not { } pointed) continue;

            _augmentations.Add(pointed);
            _byAugmentation.TryAdd(pointed, trait);
        }

        if (_byTag.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so every {Table} tag exports as a bare id with no name, no " +
                "text and no icon.");
        }
    }

    public ItemTrait? ForTag(string tagName) => _byTag.GetValueOrDefault(tagName);

    public int MaxRank => _byTag.Values.Max(trait => trait.MaxRank ?? 0);

    public IEnumerable<ItemTrait> All => _byTag.Values;

    public ItemTrait? ForAugmentation(int netId) => _byAugmentation.GetValueOrDefault(netId);

    public bool PointsAt(int netId) => _augmentations.Contains(netId);

    private static Dictionary<string, IReadOnlyList<ConstantRecord>> Scopes(
        IReadOnlyList<(string Tag, FStructFallback Row, string? Augmentation)> rows, ConstantsTable constants)
    {
        var direct = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tag, _, augmentation) in rows)
        {
            if (augmentation is null) continue;
            if (TuningNamespace.For(AbilityPresentationIndex.AssetName(augmentation), constants) is not { } scope)
            {
                continue;
            }

            direct[tag] = scope;
            claimed.Add(scope);
        }

        var scopes = new Dictionary<string, IReadOnlyList<ConstantRecord>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tag, _, augmentation) in rows)
        {
            var scope = direct.GetValueOrDefault(tag)
                ?? (augmentation is null ? null : Trimmed(AbilityPresentationIndex.AssetName(augmentation), constants, claimed));

            if (scope is null) continue;

            scopes[tag] = [.. constants.AllUnder(scope)];
        }

        return scopes;
    }

    private static string? Trimmed(string assetName, ConstantsTable constants, HashSet<string> claimed)
    {
        var segments = assetName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2) return null;

        foreach (var start in (int[])[0, 1])
        {
            if (start == 1 && segments.Length <= 2) continue;

            var head = string.Join('.', segments[start..^1]);
            var last = segments[^1];

            while (Cut(last) is { } cut)
            {
                last = last[..cut];
                var candidate = $"{head}.{last}";

                if (constants.IsNamespace(candidate) && !claimed.Contains(candidate)) return candidate;
            }
        }

        return null;
    }

    private static int? Cut(string word)
    {
        for (var i = word.Length - 1; i > 0; i--)
        {
            if (char.IsUpper(word[i])) return i;
        }

        return null;
    }
}

public sealed class TraitCategoryNames
{
    public const string RowStructure = "/Script/fellowship.CRCraftingRecipe";

    private const string RecipeIdField = "RecipeID";
    private const string RecipeNameField = "RecipeName";
    private const string IdPrefix = "Crafting.Recipes.TraitTrees.Reroll";
    private const string IdSuffix = "Traits";
    private const string CategoryPrefix = "ItemTrait.TraitCategory.";

    private readonly Dictionary<string, string> _byCategory = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byCategory.Count;

    public TraitCategoryNames(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        IReadOnlyDictionary<string, ItemSlotDefinition> slots,
        IReadOnlyList<ModifierTypeEntry> modifiers)
    {
        foreach (var modifier in modifiers)
        {
            if (modifier.Printed is not { } printed) continue;
            if (slots.GetValueOrDefault(modifier.Name)?.ItemTraitPool is not [var pooled]) continue;
            if (!pooled.StartsWith(CategoryPrefix, StringComparison.OrdinalIgnoreCase)) continue;

            _byCategory[pooled[CategoryPrefix.Length..]] = printed;
        }

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, RecipeIdField) is not { } id) continue;
                if (!id.StartsWith(IdPrefix, StringComparison.Ordinal)) continue;
                if (!id.EndsWith(IdSuffix, StringComparison.Ordinal)) continue;
                if (PropertyWalk.GetString(row, RecipeNameField) is not { } name) continue;

                var category = id[IdPrefix.Length..^IdSuffix.Length];
                var space = name.IndexOf(' ');

                _byCategory.TryAdd(category, space < 0 ? name : name[..space]);
            }
        }
    }

    public string? Label(string? categoryTag)
    {
        if (categoryTag is null || !categoryTag.StartsWith(CategoryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var category = categoryTag[CategoryPrefix.Length..];
        return _byCategory.GetValueOrDefault(category) ?? category;
    }
}
