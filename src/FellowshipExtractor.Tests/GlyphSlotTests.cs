using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Objects;

using Fellowship.SDK.Documents;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class GlyphSlotTests
{
    private const string RankTable =
        "fellowship/Content/Items/DynamicItemAbilityRanks/DT_Generic_DynamicAbilityRank.DT_Generic_DynamicAbilityRank";

    private static IReadOnlyList<GlyphDocument> Glyphs =>
        [.. EntityStoreCorpusTests.Store.Entities.OfType<GlyphDocument>()];

    private static UDataTable Ranks() => Export.Provider.LoadPackageObject<UDataTable>(RankTable);

    [Fact]
    public void EveryGlyphStatesARankCeilingItsPoolsAndItsRankId()
    {
        Assert.NotEmpty(Glyphs);

        foreach (var glyph in Glyphs)
        {
            Assert.True(glyph.MaxRank > 0, $"Glyph {glyph.Id} states no MaxRank.");
            Assert.NotEmpty(glyph.Slots!);
            Assert.True(glyph.AbilityRank > 0, $"Glyph {glyph.Id} states no ability rank id.");
        }
    }

    [Fact]
    public void EveryPoolSlotIsASlotTheSettingsWeigh()
    {
        var known = Export.Loaded.Settings.StatCalculation.SlotWeights.Keys;

        foreach (var glyph in Glyphs)
        {
            Assert.All(glyph.Slots!, slot => Assert.Contains(slot, known, StringComparer.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void EveryRankRowIsOneGlyphCarryingItsTagIdAndEveryPool()
    {
        var rankIds = CsvTables.ReadTags(Export.Provider)
            .Where(tag => tag.Table == DynamicAbilityRanks.Table)
            .ToDictionary(tag => tag.Name, tag => tag.NetId);

        var rows = Ranks().RowMap.Values.ToArray();

        Assert.NotEmpty(rows);
        Assert.Equal(rows.Length, Glyphs.Count);

        foreach (var row in rows)
        {
            var tag = PropertyWalk.GetString(row, "ID")!;
            var pools = Assert.IsType<UScriptArray>(PropertyWalk.Get(row, "PoolIds")?.GenericValue).Properties.Count;

            var glyph = Assert.Single(Glyphs, glyph => glyph.AbilityRank == rankIds[tag]);

            Assert.Equal(pools, glyph.Slots!.Count);
            Assert.Equal(PropertyWalk.AsOrdinal(PropertyWalk.Get(row, "MaxRank")?.GenericValue), glyph.MaxRank);
        }
    }

    [Fact]
    public void EveryGlyphCarriesBothRunesAndTheAbilityItGrants()
    {
        foreach (var row in Ranks().RowMap.Values)
        {
            var augmentation = Export.Provider.LoadPackageObject(PropertyWalk.GetString(row, "GrantedAbilitiesAsset")!);
            var glyph = Glyphs.Single(document => document.Name == PropertyWalk.GetText(augmentation, "DisplayName"));

            Assert.Equal(AbilityPresentationIndex.AssetName(PropertyWalk.GetString(augmentation, "UIIcon")!), glyph.Icon);
            Assert.Equal(
                AbilityPresentationIndex.AssetName(PropertyWalk.GetString(augmentation, "UIIconHovered")!),
                glyph.IconHovered);
            Assert.True(File.Exists(Path.Combine(Export.Loaded.Ui, TextureSizes.File(glyph.IconHovered!, TextureSize.Full))));
            Assert.NotEmpty(glyph.Grants!);
        }
    }

    [Theory]
    [InlineData(700, "DynamicItemAbilityRank.Generic.02")]
    [InlineData(712, "DynamicItemAbilityRank.Generic.06")]
    public void AGlyphCarriesTheGenericRankIdOfTheRowGrantingIt(int glyph, string rank)
    {
        var expected = CsvTables.ReadTags(Export.Provider)
            .Single(tag => tag.Table == DynamicAbilityRanks.Table && tag.Name == rank)
            .NetId;

        Assert.Equal(expected, Glyphs.Single(document => document.Id == glyph).AbilityRank);
    }
}
