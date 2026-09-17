using CUE4Parse.FileProvider;
using CUE4Parse.UE4.AssetRegistry.Objects;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

namespace FellowshipExtractor.Extraction;

public sealed class Leagues
{
    public const string RowStructure = "/Script/fellowship.CRGameFinderLeague";

    private const string NameField = "Name";
    private const string TagField = "LeagueID";
    private const string KindField = "Type";
    private const string MinimumField = "MinimumDifficulty";
    private const string MaximumField = "MaximumDifficulty";
    private const string DifficultiesField = "Difficulties";

    private readonly List<LeagueEntry> _entries = [];

    public IReadOnlyList<LeagueEntry> All => _entries;

    public int Count => _entries.Count;

    public int Ceiling => _entries.Count == 0 ? 0 : _entries.Max(league => league.MaximumDifficulty);

    public Leagues(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                if (Read(rowName.Text, row) is { } league) _entries.Add(league);
            }
        }

        if (_entries.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table of {RowStructure} rows in the build, so nothing states which difficulties a league " +
                "covers and every difficulty would be an unnamed number.");
        }

        _entries.Sort((left, right) => left.MinimumDifficulty.CompareTo(right.MinimumDifficulty));
    }

    private static LeagueEntry? Read(string row, IPropertyHolder table)
    {
        if (PropertyWalk.GetString(table, NameField) is not { Length: > 0 } named) return null;
        if (PropertyWalk.GetString(table, TagField) is not { Length: > 0 } tag) return null;

        var kind = PropertyWalk.AsOrdinal(PropertyWalk.Get(table, KindField)?.GenericValue);

        if (kind is null || !Enum.IsDefined(typeof(LeagueKind), kind.Value))
        {
            throw new InvalidOperationException(
                $"{RowStructure} row {row} states {KindField} {kind?.ToString() ?? "nothing"}, and that names " +
                "no league kind, so nothing says whether its difficulties are ranked, eternal or pinnacle.");
        }

        return new LeagueEntry(row, named, tag, (LeagueKind)kind.Value)
        {
            MinimumDifficulty = PropertyWalk.AsOrdinal(PropertyWalk.Get(table, MinimumField)?.GenericValue) ?? 0,
            MaximumDifficulty = PropertyWalk.AsOrdinal(PropertyWalk.Get(table, MaximumField)?.GenericValue) ?? 0,
            Difficulties = Steps(table),
        };
    }

    private static IReadOnlyList<int> Steps(IPropertyHolder table) =>
        PropertyWalk.Get(table, DifficultiesField)?.GenericValue is UScriptArray listed
            ? [.. listed.Properties.Select(step => PropertyWalk.AsOrdinal(step?.GenericValue)).OfType<int>()]
            : [];
}
