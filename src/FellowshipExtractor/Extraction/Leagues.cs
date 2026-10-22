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
    private const string EnumScope = "::";
    private const string ModeEnum = "ECRGameFinderMode";
    private const string BucketDifficultyField = "Difficulty";

    private static readonly string[] BucketFields = ["EternalBuckets", "PinnacleBuckets"];

    private readonly List<LeagueEntry> _entries = [];
    private readonly List<string> _unreached = [];
    private readonly SortedSet<int> _listed = [];
    private readonly SortedSet<int> _matched = [];

    public IReadOnlySet<int> Listed => _listed;

    public IReadOnlySet<int> Matched => _matched;

    public IReadOnlyList<LeagueEntry> All => _entries;

    public IReadOnlyList<string> Unreached => _unreached;

    public IReadOnlyList<LeagueKindEntry> Kinds { get; }

    public int Count => _entries.Count;

    public int Ceiling => _entries.Count == 0 ? 0 : _entries.Max(league => league.MaximumDifficulty);

    public Leagues(AbstractFileProvider provider, IReadOnlyList<FAssetData> assets)
    {
        var rows = new List<(string Row, IPropertyHolder Fields)>();
        string? enumName = null;

        foreach (var objectPath in TableFinder.ByRowStructure(assets, RowStructure))
        {
            if (!provider.TryLoadPackageObject<UDataTable>(
                AbilityPresentationIndex.PackagePath(objectPath), out var table)) continue;

            foreach (var (rowName, row) in table.RowMap)
            {
                rows.Add((rowName.Text, row));

                if (PropertyWalk.AsString(PropertyWalk.Get(row, KindField)?.GenericValue) is { } text &&
                    text.IndexOf(EnumScope, StringComparison.Ordinal) is var scope and > 0)
                {
                    enumName ??= text[..scope];
                }
            }
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table of {RowStructure} rows in the build, so nothing states which difficulties a league " +
                "covers and every difficulty would be an unnamed number.");
        }

        var mappings = provider.MappingsContainer?.MappingsForGame
            ?? throw new InvalidOperationException($"No mappings are loaded, so nothing names a league's {KindField}.");

        if (enumName is null)
        {
            throw new InvalidOperationException(
                $"No {RowStructure} row writes its {KindField} as an enum member, so nothing names the enum a " +
                "league's kind is read from.");
        }

        var members = EnumOrdinals.Members(mappings, enumName);
        var modes = new HashSet<string>(EnumOrdinals.Members(mappings, ModeEnum).Values, StringComparer.Ordinal);

        Kinds =
        [
            .. members.OrderBy(member => member.Key)
                .Select(member => new LeagueKindEntry(member.Value, (int)member.Key)),
        ];

        foreach (var (rowName, row) in rows)
        {
            if (PropertyWalk.AsOrdinal(PropertyWalk.Get(row, KindField)?.GenericValue) is { } ordinal &&
                members.TryGetValue(ordinal, out var member) &&
                !modes.Contains(member))
            {
                _unreached.Add(rowName);
                continue;
            }

            if (Read(rowName, row, members, enumName) is not { } league) continue;

            _entries.Add(league);
            _listed.UnionWith(league.Difficulties);
            _matched.UnionWith(Buckets(row, league));
        }

        _entries.Sort((left, right) => left.MinimumDifficulty.CompareTo(right.MinimumDifficulty));
    }

    private static LeagueEntry? Read(
        string row, IPropertyHolder table, IReadOnlyDictionary<long, string> members, string enumName)
    {
        if (PropertyWalk.GetString(table, NameField) is not { Length: > 0 } named) return null;
        if (PropertyWalk.GetString(table, TagField) is not { Length: > 0 } tag) return null;

        var kind = PropertyWalk.AsOrdinal(PropertyWalk.Get(table, KindField)?.GenericValue);

        if (kind is null || !members.ContainsKey(kind.Value))
        {
            throw new InvalidOperationException(
                $"{RowStructure} row {row} states {KindField} {kind?.ToString() ?? "nothing"}, and {enumName} has " +
                "no member at it.");
        }

        return new LeagueEntry(row, named, tag, (LeagueKind)kind.Value)
        {
            MinimumDifficulty = PropertyWalk.AsOrdinal(PropertyWalk.Get(table, MinimumField)?.GenericValue) ?? 0,
            MaximumDifficulty = PropertyWalk.AsOrdinal(PropertyWalk.Get(table, MaximumField)?.GenericValue) ?? 0,
            Difficulties = Steps(table),
        };
    }

    private static IEnumerable<int> Buckets(IPropertyHolder row, LeagueEntry league)
    {
        foreach (var field in BucketFields)
        {
            if (PropertyWalk.Get(row, field)?.GenericValue is not UScriptArray buckets) continue;

            foreach (var bucket in buckets.Properties)
            {
                if (bucket?.GenericValue is not FScriptStruct { StructType: IPropertyHolder held }) continue;

                var step = PropertyWalk.AsOrdinal(PropertyWalk.Get(held, BucketDifficultyField)?.GenericValue) ?? 0;

                if (step < 0 || step >= league.Difficulties.Count)
                {
                    throw new InvalidOperationException(
                        $"{RowStructure} row {league.Row} has a {field} entry at step {step}, and the league lists " +
                        $"{league.Difficulties.Count} difficulties.");
                }

                yield return league.Difficulties[step];
            }
        }
    }

    private static IReadOnlyList<int> Steps(IPropertyHolder table) =>
        PropertyWalk.Get(table, DifficultiesField)?.GenericValue is UScriptArray listed
            ? [.. listed.Properties.Select(step => PropertyWalk.AsOrdinal(step?.GenericValue)).OfType<int>()]
            : [];
}
