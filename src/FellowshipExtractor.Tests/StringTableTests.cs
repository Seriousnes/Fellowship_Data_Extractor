using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.Core.i18N;

using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class StringTableTests
{
    private const string GemTable = "fellowship/Content/Items/DT_GemDefinitions.DT_GemDefinitions";
    private const string GemStrings = "ST_Gems";
    private const string DisplayNameField = "DisplayName";
    private const string TiersField = "GemTierDefinitions";

    private static UDataTable Gems() => Export.Provider.LoadPackageObject<UDataTable>(GemTable);

    [Fact]
    public void AGemNameIsAStringTableEntryTheLocresResolves()
    {
        var row = Gems().RowMap.First(entry => entry.Key.Text == "Ruby").Value;

        var text = Assert.IsType<FText>(PropertyWalk.Get(row, DisplayNameField)?.GenericValue);
        var entry = Assert.IsType<FTextHistory.StringTableEntry>(text.TextHistory);

        Assert.Equal(GemStrings, entry.TableId.Text);
        Assert.Equal("Ruby", PropertyWalk.GetString(row, DisplayNameField));
        Assert.Equal("Ruby", StringTables.Find(GemStrings, entry.Key));
    }

    [Fact]
    public void EveryGemTypeAndTierReadsItsName()
    {
        foreach (var (key, row) in Gems().RowMap)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(PropertyWalk.GetString(row, DisplayNameField)),
                $"Gem type {key.Text} reads no display name.");

            var tiers = Assert.IsType<UScriptArray>(PropertyWalk.Get(row, TiersField)?.GenericValue).Properties
                .Select(tier => tier?.GenericValue)
                .OfType<FScriptStruct>()
                .Select(tier => tier.StructType)
                .OfType<IPropertyHolder>()
                .ToArray();

            Assert.NotEmpty(tiers);
            Assert.All(tiers, tier => Assert.False(
                string.IsNullOrWhiteSpace(PropertyWalk.GetString(tier, DisplayNameField)),
                $"A tier of gem type {key.Text} reads no display name."));
        }
    }

    [Fact]
    public void TheStringTablesAreNotTuningConstants()
    {
        Assert.DoesNotContain(
            CsvTables.ReadConstants(Export.Provider),
            record => record.Table.StartsWith("ST_", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(22, "Small Amethyst")]
    [InlineData(25, "Flawless Amethyst")]
    public void AGemDocumentCarriesTheNameTheStringTableHolds(int id, string expected)
    {
        var document = Export.Loaded.Documents.Single(document => document.Type == EntityType.Gem && document.Id == id);

        Assert.Equal(expected, document.Name);
    }
}
