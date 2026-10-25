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

    private static IReadOnlyDictionary<int, bool> Currencies() =>
        Export.Loaded.Documents
            .Where(row => row.Type == EntityType.Currency)
            .ToDictionary(row => row.Id, row => row.Body["enabledInGame"]?.GetValue<bool>() is not false);

    [Fact]
    public void ACurrencyRowIsADocumentEnabledInGameExactlyWhenTheRowIs()
    {
        var ids = NetIds();
        var written = Currencies();
        var rows = Export.Provider.LoadPackageObject<UDataTable>(CurrencyTable).RowMap;

        Assert.NotEmpty(rows);

        foreach (var (rowName, row) in rows)
        {
            if (!ids.TryGetValue(TagPrefix + rowName.Text, out var id)) continue;

            var enabled = PropertyWalk.Get(row, EnabledField)?.GenericValue is true;

            Assert.True(written.ContainsKey(id), $"{rowName.Text} is not written.");
            Assert.True(
                enabled == written[id],
                $"{rowName.Text} is {(enabled ? "enabled" : "disabled")} and its document is {(written[id] ? "enabled" : "disabled")}.");
        }
    }

    [Theory]
    [InlineData("QuickplayToken", false)]
    [InlineData("RareDust", false)]
    [InlineData("CHIP", true)]
    [InlineData("CosmicThread", true)]
    public void SuppliesAndTheRareMaterialsAreDisabledInGame(string row, bool enabled) =>
        Assert.Equal(enabled, Currencies()[NetIds()[TagPrefix + row]]);
}
