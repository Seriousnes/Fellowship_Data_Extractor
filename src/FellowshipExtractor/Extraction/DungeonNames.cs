using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;

namespace FellowshipExtractor.Extraction;

public sealed record Dungeon(string Tag, string DisplayName)
{
    public string? Description { get; init; }

    public string? LevelName { get; init; }

    public string? BannerPath { get; init; }

    public string? BackgroundPath { get; init; }

    public int? MaxPlayers { get; init; }

    public double? QualifierTime { get; init; }

    public int? RequiredScore { get; init; }

    public int? MinimumHeroRank { get; init; }

    public string? LevelType { get; init; }

    public bool? SeasonalPinnacle { get; init; }

    public bool? AvailableForCasual { get; init; }

    public int? MinimumDifficulty { get; init; }
}

public sealed class DungeonNames
{
    public const string RowStructure = "/Script/fellowship.CRLevelInfoRow";

    private const string LevelInfoClass = "CRLevelInfo";
    private const string DisplayNameField = "DisplayName";
    private const string LevelIdField = "LevelID";
    private const string InfoField = "Info";
    private const string DescriptionField = "LevelDescription";
    private const string LevelNameField = "LevelName";
    private const string BannerField = "BannerTexture";
    private const string BackgroundField = "BackgroundTexture";
    private const string MaxPlayersField = "MaxPlayers";
    private const string QualifierTimeField = "DungeonQualifierTime";
    private const string RequiredScoreField = "RequiredDungeonScoreToComplete";
    private const string MinimumHeroRankField = "MinimumHeroRankRequired";
    private const string LevelTypeField = "LevelType";
    private const string PinnacleField = "IsSeasonalPinnacleDungeon";
    private const string CasualField = "AvailableForCasual";
    private const string MinimumDifficultyField = "MinimumDifficulty";

    private readonly Dictionary<string, Dungeon> _byTag = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dungeon> _byLevelName = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byTag.Count;

    public IReadOnlyCollection<Dungeon> All => _byTag.Values;

    public DungeonNames(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (Read(rowName.Text, row) is { } dungeon) Keep(dungeon);
            }
        }

        foreach (var asset in assets)
        {
            if (!asset.AssetClass.Text.Equals(LevelInfoClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (Load(provider, asset.ObjectPath) is not { } level) continue;
            if (PropertyWalk.GetString(level, LevelIdField) is not { } tag) continue;
            if (PropertyWalk.GetStruct(level, InfoField) is not { } info) continue;

            if (Read(tag, info) is { } dungeon) Keep(dungeon);
        }
    }

    private void Keep(Dungeon dungeon)
    {
        if (!_byTag.TryAdd(dungeon.Tag, dungeon)) return;
        if (dungeon.LevelName is { Length: > 0 } level) _byLevelName.TryAdd(level, dungeon);
    }

    private static Dungeon? Read(string tag, IPropertyHolder row)
    {
        if (PropertyWalk.GetText(row, DisplayNameField) is not { Length: > 0 } printed) return null;

        return new Dungeon(tag, printed)
        {
            Description = PropertyWalk.GetText(row, DescriptionField),
            LevelName = PropertyWalk.GetString(row, LevelNameField),
            BannerPath = PropertyWalk.GetString(row, BannerField),
            BackgroundPath = PropertyWalk.GetString(row, BackgroundField),
            MaxPlayers = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, MaxPlayersField)?.GenericValue),
            QualifierTime = PropertyWalk.AsNumber(PropertyWalk.Get(row, QualifierTimeField)?.GenericValue),
            RequiredScore = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, RequiredScoreField)?.GenericValue),
            MinimumHeroRank = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, MinimumHeroRankField)?.GenericValue),
            LevelType = Member(PropertyWalk.GetString(row, LevelTypeField)),
            SeasonalPinnacle = PropertyWalk.Get(row, PinnacleField)?.GenericValue is true ? true : null,
            AvailableForCasual = PropertyWalk.Get(row, CasualField)?.GenericValue is true ? true : null,
            MinimumDifficulty = PropertyWalk.AsOrdinal(PropertyWalk.Get(row, MinimumDifficultyField)?.GenericValue),
        };
    }

    private static string? Member(string? value) =>
        value?.LastIndexOf("::", StringComparison.Ordinal) is { } scoped and >= 0 ? value[(scoped + 2)..] : value;

    public string? For(string levelTag) => Find(levelTag)?.DisplayName;

    public Dungeon? Find(string levelTag) => _byTag.GetValueOrDefault(levelTag);

    public Dungeon? ByLevelName(string levelName) => _byLevelName.GetValueOrDefault(levelName);

    public static UObject? Load(AbstractFileProvider provider, string objectPath)
    {
        if (!provider.TryLoadPackage(AbilityPresentationIndex.PackagePath(objectPath), out var package))
        {
            return null;
        }

        var exports = package.GetExports().ToArray();

        return exports.FirstOrDefault(export => export.Name.StartsWith("Default__", StringComparison.Ordinal))
            ?? exports.FirstOrDefault();
    }
}
