using CUE4Parse.UE4.Assets.Exports.Engine;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class LeagueKindTests
{
    private const string LeagueTable = "fellowship/Content/MatchMaking/DT_GameFinderLeagues.DT_GameFinderLeagues";
    private const string KindField = "Type";
    private const string ModeEnum = "ECRGameFinderMode";

    private static UDataTable Leagues() => Export.Provider.LoadPackageObject<UDataTable>(LeagueTable);

    private static IReadOnlySet<string> Modes() =>
        new HashSet<string>(
            EnumOrdinals.Members(Export.Provider.MappingsContainer!.MappingsForGame!, ModeEnum).Values,
            StringComparer.Ordinal);

    private static string Member(string value) => value[(value.IndexOf("::", StringComparison.Ordinal) + 2)..];

    [Fact]
    public void ALeagueKindMemberCarriesItsShippedOrdinal()
    {
        var kinds = SettingsEnums.LeagueKinds(Export.Loaded.Settings);

        Assert.NotEmpty(kinds);
        Assert.Equal(kinds.Select(kind => kind.Name), Enum.GetNames<LeagueKind>());

        foreach (var (name, value) in kinds)
        {
            Assert.Equal(value, (int)Enum.Parse<LeagueKind>(name));
        }
    }

    [Fact]
    public void EveryLeagueRowAGameFinderModeReachesIsWrittenWithItsKind()
    {
        var modes = Modes();
        var byRow = Export.Loaded.Settings.Leagues.ToDictionary(league => league.Row);

        foreach (var (rowName, row) in Leagues().RowMap)
        {
            var member = Member(PropertyWalk.AsString(PropertyWalk.Get(row, KindField)?.GenericValue)!);

            if (modes.Contains(member))
            {
                Assert.Equal(member, byRow[rowName.Text].Kind.ToString());
            }
            else
            {
                Assert.DoesNotContain(rowName.Text, byRow.Keys);
            }
        }
    }

    [Fact]
    public void TheRankedRowsAreUnreachedAndTheCasualRowsAreWritten()
    {
        Assert.DoesNotContain("Ranked", Modes());
        Assert.Equal(
            ["Casual1", "Casual2", "Casual3", "League5", "League6"],
            Export.Loaded.Settings.Leagues.Select(league => league.Row).Order(StringComparer.Ordinal));
        Assert.Equal(3, Export.Loaded.Settings.Leagues.Count(league => league.Kind == LeagueKind.Casual));
    }
}
