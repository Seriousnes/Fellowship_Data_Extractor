using CUE4Parse.UE4.Assets.Exports.Engine;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class CurrencyTableTests
{
    private const string CurrencyTable = "fellowship/Content/Items/DT_Currencies.DT_Currencies";
    private const string TagPrefix = "ItemID.Currency.";
    private const string EnabledField = "EnabledIngame";

    private static IReadOnlyDictionary<string, int> NetIds() =>
        CsvTables.Current([.. CsvTables.ReadTags(Export.Provider)])
            .Where(tag => tag.Table == "ItemID" && tag.Name.StartsWith(TagPrefix, StringComparison.Ordinal))
            .ToDictionary(tag => tag.Name, tag => tag.NetId, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<int> Currencies() =>
        Export.Loaded.Documents.Where(row => row.Type == EntityType.Currency).Select(row => row.Id).ToHashSet();

    [Fact]
    public void ACurrencyRowIsADocumentExactlyWhenItIsEnabledInGame()
    {
        var ids = NetIds();
        var written = Currencies();
        var rows = Export.Provider.LoadPackageObject<UDataTable>(CurrencyTable).RowMap;

        Assert.NotEmpty(rows);

        foreach (var (rowName, row) in rows)
        {
            if (!ids.TryGetValue(TagPrefix + rowName.Text, out var id)) continue;

            var enabled = PropertyWalk.Get(row, EnabledField)?.GenericValue is true;

            Assert.True(
                enabled == written.Contains(id),
                $"{rowName.Text} is {(enabled ? "enabled" : "disabled")} and {(written.Contains(id) ? "is" : "is not")} written.");
        }
    }

    [Theory]
    [InlineData("QuickplayToken", false)]
    [InlineData("RareDust", false)]
    [InlineData("CHIP", true)]
    [InlineData("CosmicThread", true)]
    public void SuppliesAndTheRareMaterialsAreGone(string row, bool written) =>
        Assert.Equal(written, Currencies().Contains(NetIds()[TagPrefix + row]));
}
