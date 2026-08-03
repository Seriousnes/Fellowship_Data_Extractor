using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Tooltip;

/// <summary>One shipped item trait, as its own tooltip shows it.</summary>
public sealed record ItemTrait(string DisplayName)
{
    public string? Description { get; init; }

    public string? CategoryTag { get; init; }

    public string? Category { get; init; }

    public int? MaxRank { get; init; }

    public int? RandomChance { get; init; }

    public int? Augmentation { get; init; }

    public string? IconPath { get; init; }

    public IReadOnlyList<RankRun>? Runs { get; init; }
}

/// <summary>
/// The traits a weapon or a necklace can carry.
///
/// The table is joined on TraidID, the game's own misspelling of the field holding the tag, and
/// never on the row key: two rows have a key that is not their tag, and one of those two is also
/// the row whose augmentation name is the only route to its tuning.
///
/// The forty rows are two populations. Twenty-nine are weapon traits with four ranks, split between
/// three categories a tree row rolls from, and eleven are necklace traits with one rank and no
/// progression at all. Six of the eleven ship no description whatsoever, which is why a row is kept
/// on its display name rather than on having anything to say.
/// </summary>
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

    private readonly Dictionary<string, ItemTrait> _byTag = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byTag.Count;

    /// <summary>How many of the printed progressions bound to the curve row behind them.</summary>
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

            _byTag[tag] = new ItemTrait(PropertyWalk.GetString(row, DisplayNameField)!)
            {
                Description = description,
                CategoryTag = categoryTag,
                Category = categories.Label(categoryTag),
                MaxRank = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, MaxRankField)?.GenericValue),
                RandomChance = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, RandomChanceField)?.GenericValue),
                Augmentation = augmentations.For(augmentation),
                IconPath = PropertyWalk.GetString(row, IconField),
                Runs = runs,
            };
        }

        if (_byTag.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares {RowStructure}, so every {Table} tag exports as a bare id with no name, no " +
                "text and no icon.");
        }
    }

    /// <summary>The trait an ItemTrait.ID tag names, or null when no row defines it.</summary>
    public ItemTrait? ForTag(string tagName) => _byTag.GetValueOrDefault(tagName);

    /// <summary>
    /// The tuning rows each trait's progressions may be drawn from.
    ///
    /// A trait's rows sit under Trait.&lt;Name&gt;, and the augmentation asset is what names it: the
    /// existing TuningNamespace rule guesses from the asset name and keeps the guess only where the
    /// shipped tables confirm rows really are under it. That answers 30 of the 31 traits with
    /// tuning, including the two whose row key is not their name.
    ///
    /// The one it does not answer is CAA_Trait_GemWhirlwindProc, whose rows sit under
    /// Trait.GemWhirlwind. So a trailing word is dropped from the name and the tables asked again,
    /// and a namespace another trait already claimed outright is not offered to anyone else. That
    /// only narrows the search: a run still binds to a row only when the row's four values
    /// reproduce all four printed ones, so a scope guessed wrongly yields no binding rather than a
    /// wrong one.
    /// </summary>
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

    /// <summary>
    /// A namespace reached by dropping trailing words off an asset name's last segment. Words are
    /// the capitals: GemWhirlwindProc gives GemWhirlwind and then GemWhirl and then Gem, and the
    /// first one the tables confirm and nobody else has claimed wins.
    /// </summary>
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

/// <summary>
/// What the game calls each trait category.
///
/// The tag says Major and the game says Master. The one shipped table that states the rename is the
/// crafting recipes: the recipe whose id is Crafting.Recipes.TraitTrees.RerollMajorTraits is named
/// "Master Attunement", and its three siblings name Defensive, Heroic and Necklace the same way.
/// The tutorial text corroborates it independently, enumerating the weapon rows as "Heroic,
/// Defensive, or Master".
///
/// A second recipe family, Crafting.Recipes.HeroItems.Slots.RerollMajorTraitsValues, keeps the
/// internal word and is named "Randomize Major traits". It is about an item's modifier slots rather
/// than the trait tree, so the join is on the TraitTrees branch alone and not on a substring.
/// </summary>
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

    public TraitCategoryNames(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
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

    /// <summary>
    /// The printed name of a category tag. A category no recipe rerolls keeps the tag's own last
    /// segment, because the tag is then the only name the build gives it.
    /// </summary>
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
