using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Models;

namespace FellowshipExtractor.Extraction;

public sealed class ChallengeRewards
{
    private const string CollectionClass = "CRChallengesCollectionDefinition";
    private const string ChallengesField = "Challenges";
    private const string ChallengeIdField = "ChallengeId";
    private const string DisplayNameField = "DisplayName";
    private const string CategoryField = "CategoryName";
    private const string RewardsField = "Rewards";
    private const string ExtensionsField = "RewardExtensions";
    private const string TemplateIdField = "TemplateChallengeId";
    private const string AddedRewardsField = "AddedRewards";
    private const string ItemIdField = "ItemID";
    private const string DropTableField = "DropTable";

    private readonly AbstractFileProvider _provider;
    private readonly Dictionary<string, UObject[]> _packages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ItemSource>> _byItem = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ItemSource>> _byDroptable = new(StringComparer.OrdinalIgnoreCase);

    public int Challenges { get; }

    public int Items => _byItem.Count;

    public ChallengeRewards(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        _provider = provider;

        var collections = assets
            .Where(asset => asset.AssetClass.Text.Equals(CollectionClass, StringComparison.Ordinal))
            .Select(asset => Exports(AbilityPresentationIndex.PackagePath(asset.ObjectPath))
                .FirstOrDefault(export => export.ExportType == CollectionClass))
            .OfType<UObject>()
            .ToArray();

        var named = new Dictionary<string, ItemSource>(StringComparer.Ordinal);

        foreach (var collection in collections)
        {
            foreach (var challenge in Structs(collection, ChallengesField))
            {
                if (PropertyWalk.GetString(challenge, ChallengeIdField) is not { } id) continue;
                if (PropertyWalk.GetText(challenge, DisplayNameField) is not { Length: > 0 } name) continue;

                var source = new ItemSource(SourceKind.Challenge, name)
                {
                    Category = PropertyWalk.GetText(challenge, CategoryField) is { Length: > 0 } category ? category : null,
                };

                named[id] = source;
                Grant(source, References(challenge, RewardsField));
            }
        }

        Challenges = named.Count;

        foreach (var collection in collections)
        {
            foreach (var extension in Structs(collection, ExtensionsField))
            {
                if (PropertyWalk.GetString(extension, TemplateIdField) is not { } id) continue;

                if (!named.TryGetValue(id, out var source))
                {
                    throw new InvalidOperationException(
                        $"{collection.Name} extends challenge {id}, and no {CollectionClass} defines it.");
                }

                Grant(source, References(extension, AddedRewardsField));
            }
        }
    }

    public IReadOnlyList<ItemSource> ForItem(string tagName) =>
        _byItem.TryGetValue(tagName, out var sources) ? sources : [];

    public IReadOnlyList<ItemSource> ForDroptable(string droptableName) =>
        _byDroptable.TryGetValue(droptableName, out var sources) ? sources : [];

    private void Grant(ItemSource source, IEnumerable<string> rewards)
    {
        foreach (var path in rewards)
        {
            if (Reward(path) is not { } reward) continue;

            if (PropertyWalk.GetString(reward, ItemIdField) is { } item) Add(_byItem, item, source);

            if (PropertyWalk.GetString(reward, DropTableField) is { } droptable)
            {
                Add(_byDroptable, AbilityPresentationIndex.AssetName(droptable), source);
            }
        }
    }

    private static void Add(Dictionary<string, List<ItemSource>> sources, string key, ItemSource source)
    {
        if (!sources.TryGetValue(key, out var held)) sources[key] = held = [];
        if (!held.Contains(source)) held.Add(source);
    }

    private UObject? Reward(string path)
    {
        var separator = path.LastIndexOf(':');
        if (separator < 0) return null;

        var exportName = path[(separator + 1)..];
        var dot = exportName.LastIndexOf('.');
        if (dot >= 0) exportName = exportName[(dot + 1)..];

        return Exports(AbilityPresentationIndex.PackagePath(path[..separator]))
            .FirstOrDefault(export => export.Name == exportName);
    }

    private UObject[] Exports(string packagePath)
    {
        if (_packages.TryGetValue(packagePath, out var held)) return held;

        UObject[] exports = _provider.TryLoadPackage(packagePath, out var package) ? [.. package.GetExports()] : [];
        _packages[packagePath] = exports;

        return exports;
    }

    private static IEnumerable<IPropertyHolder> Structs(IPropertyHolder holder, string field) =>
        PropertyWalk.Get(holder, field)?.GenericValue is UScriptArray listed
            ? listed.Properties
                .Select(entry => entry?.GenericValue)
                .OfType<FScriptStruct>()
                .Select(entry => entry.StructType)
                .OfType<IPropertyHolder>()
            : [];

    private static IEnumerable<string> References(IPropertyHolder holder, string field) =>
        PropertyWalk.Get(holder, field)?.GenericValue is UScriptArray listed
            ? listed.Properties.Select(entry => PropertyWalk.AsString(entry?.GenericValue)).OfType<string>()
            : [];
}
