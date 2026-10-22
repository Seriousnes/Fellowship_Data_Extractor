using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using System.Globalization;

using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed record CustomizationItem(string? DisplayName)
{
    public string? Description { get; init; }

    public string? Acquire { get; init; }

    public string? TitleText { get; init; }

    public string? IconPath { get; init; }

    public string? Rarity { get; init; }

    public int? RarityTier { get; init; }
}

public sealed class CustomizationItems
{
    public const string RowStructurePrefix = "/Script/ProfileCustomizationsRuntime.CRPlayerCustomization";
    public const string RowStructureSuffix = "ItemTableRow";
    public const string Segment = "PlayerCustomization";

    private const string TagField = "ItemId";
    private const string DisplayNameField = "DisplayName";
    private const string TitleTextField = "TitleText";
    private const string DescriptionField = "Description";
    private const string AcquireField = "AcquireDescription";
    private const string IconField = "ItemIcon";
    private const string RarityField = "Rarity";
    private const string ConditionField = "AcquireCondition";
    private const string ArgsField = "Args";
    private const string KeyField = "Key";
    private const string SourceField = "Source";
    private const string HeroIdField = "HeroId";
    private const string LevelIdField = "LevelId";
    private const string IntField = "IntValue";
    private const string FloatField = "FloatValue";
    private const string FormatField = "NumberFormattingOptions";
    private const string GroupingField = "UseGrouping";
    private const string SignField = "AlwaysSign";
    private const string MinimumFractionField = "MinimumFractionalDigits";
    private const string MaximumFractionField = "MaximumFractionalDigits";
    private const string EnumScope = "::";

    private readonly Dictionary<string, CustomizationItem> _byTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedDictionary<string, string> _nameSourceByKind = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, int> _rowsByKind = new(StringComparer.Ordinal);

    public int Count => _byTag.Count;

    public int TableCount => _rowsByKind.Count;

    public IReadOnlyDictionary<string, int> RowsByKind => _rowsByKind;

    public IReadOnlyDictionary<string, string> NameSourceByKind => _nameSourceByKind;

    public int Unnamed { get; private set; }

    public CustomizationItems(
        AbstractFileProvider provider,
        IReadOnlyList<FAssetData> assets,
        RarityNames rarities,
        HeroNames heroes,
        DungeonNames dungeons)
    {
        foreach (var (objectPath, rowStructure) in
            TableFinder.ByRowStructureAround(assets, RowStructurePrefix, RowStructureSuffix))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            var kind = rowStructure[RowStructurePrefix.Length..^RowStructureSuffix.Length];

            var declared = 0;
            var titled = 0;

            foreach (var (_, row) in table.RowMap)
            {
                if (PropertyWalk.GetString(row, TagField) is not { } tag) continue;

                var displayName = PropertyWalk.GetString(row, DisplayNameField);
                var titleText = PropertyWalk.GetString(row, TitleTextField);
                var tier = rarities.Tier(PropertyWalk.Get(row, RarityField)?.GenericValue);

                if (displayName is not null) declared++;
                else if (titleText is not null) titled++;
                else Unnamed++;

                _rowsByKind[kind] = _rowsByKind.GetValueOrDefault(kind) + 1;

                _byTag.TryAdd(
                    tag,
                    new CustomizationItem(displayName ?? titleText)
                    {
                        Description = PropertyWalk.GetText(row, DescriptionField),
                        Acquire = Acquired(row, heroes, dungeons),
                        TitleText = titleText,
                        IconPath = PropertyWalk.GetString(row, IconField),
                        Rarity = rarities.Label(tier),
                        RarityTier = tier,
                    });
            }

            _nameSourceByKind[kind] = $"{DisplayNameField} {declared}, {TitleTextField} {titled}";
        }

        if (_byTag.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table declares a {RowStructurePrefix}*{RowStructureSuffix} row structure, so every " +
                $"ItemID.{Segment} tag exports as a bare id with no name, no text and no icon.");
        }
    }

    public CustomizationItem? Find(string tagName) => _byTag.GetValueOrDefault(tagName);

    public int UnfilledArguments { get; private set; }

    private string? Acquired(IPropertyHolder row, HeroNames heroes, DungeonNames dungeons)
    {
        if (PropertyWalk.GetText(row, AcquireField) is not { } text) return null;
        if (PropertyWalk.GetStruct(row, ConditionField) is not { } condition) return text;
        if (PropertyWalk.Get(condition, ArgsField)?.GenericValue is not UScriptArray args) return text;

        foreach (var entry in args.Properties)
        {
            if (entry?.GenericValue is not FScriptStruct { StructType: IPropertyHolder arg }) continue;
            if (PropertyWalk.GetString(arg, KeyField) is not { } key) continue;

            var source = PropertyWalk.AsString(PropertyWalk.Get(arg, SourceField)?.GenericValue);
            var member = source?[(source.IndexOf(EnumScope, StringComparison.Ordinal) + EnumScope.Length)..];

            var value = member switch
            {
                "Hero" => PropertyWalk.GetString(arg, HeroIdField) is { } hero ? heroes.Shipped(hero) : null,
                "Level" => PropertyWalk.GetString(arg, LevelIdField) is { } level ? dungeons.Find(level)?.DisplayName : null,
                "Integer" => Number(arg, PropertyWalk.AsNumber(PropertyWalk.Get(arg, IntField)?.GenericValue) ?? 0),
                "Float" => Number(arg, PropertyWalk.AsNumber(PropertyWalk.Get(arg, FloatField)?.GenericValue) ?? 0),
                _ => null,
            };

            if (value is null)
            {
                UnfilledArguments++;
                continue;
            }

            text = text.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return text;
    }

    private static string Number(IPropertyHolder arg, double value)
    {
        var format = PropertyWalk.GetStruct(arg, FormatField);

        var grouping = format is not null && PropertyWalk.Get(format, GroupingField)?.GenericValue is true;
        var sign = format is not null && PropertyWalk.Get(format, SignField)?.GenericValue is true;
        var least = format is null ? 0 : PropertyWalk.AsOrdinal(PropertyWalk.Get(format, MinimumFractionField)?.GenericValue) ?? 0;
        var most = format is null ? 0 : PropertyWalk.AsOrdinal(PropertyWalk.Get(format, MaximumFractionField)?.GenericValue) ?? 0;
        most = Math.Clamp(most, least, 15);

        var pattern = (grouping ? "#,0" : "0") +
                      (most > 0 ? "." + new string('0', least) + new string('#', most - least) : string.Empty);

        var printed = value.ToString(pattern, CultureInfo.InvariantCulture);

        return sign && value >= 0 ? $"+{printed}" : printed;
    }
}
