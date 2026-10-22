using CUE4Parse.UE4.Assets.Exports.Engine;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class LeagueKindTests
{
    private const string LeagueTable = "fellowship/Content/MatchMaking/DT_GameFinderLeagues.DT_GameFinderLeagues";
    private const string KindField = "Type";

    private static UDataTable Leagues() => Export.Provider.LoadPackageObject<UDataTable>(LeagueTable);

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
    public void EveryLeagueRowStatesTheKindItsEnumMemberNames()
    {
        var byRow = Export.Loaded.Settings.Leagues.ToDictionary(league => league.Row);

        foreach (var (rowName, row) in Leagues().RowMap)
        {
            var member = PropertyWalk.AsString(PropertyWalk.Get(row, KindField)?.GenericValue)!;

            Assert.Equal(member[(member.IndexOf("::", StringComparison.Ordinal) + 2)..], byRow[rowName.Text].Kind.ToString());
        }
    }

    [Fact]
    public void TheCasualRowsReadAsCasual()
    {
        Assert.Contains(Export.Loaded.Settings.Leagues, league => league.Kind == LeagueKind.Casual);
    }
}
