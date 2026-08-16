using FellowshipExtractor.Output;
using FellowshipExtractor.Extraction;

namespace FellowshipExtractor.Tests;

public sealed class SettingsTableTests
{
    private const string OrnamentFamily = "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_";

    private static IReadOnlyDictionary<string, string> Ornaments => Export.Loaded.Settings.RarityOrnaments;

    private static IReadOnlyDictionary<string, string> ColorRows => Export.Loaded.Settings.RarityColorRows;

    private static IReadOnlyDictionary<string, string> HeadingRows => Export.Loaded.Settings.SectionHeadingRows;

    private static string Hex(string row) => Export.Loaded.Settings.Colors[row];

    [Fact]
    public void TheFourTopRungsDrawAHeaderOrnamentAndTheThreeBelowThemDrawNone()
    {
        Assert.Equal(["Champion", "Epic", "Heroic", "Legendary"], Ornaments.Keys.Order(StringComparer.Ordinal));

        Assert.Equal(OrnamentFamily + "Champion", Ornaments["Epic"]);
        Assert.Equal(OrnamentFamily + "Paragon", Ornaments["Champion"]);
        Assert.Equal(OrnamentFamily + "Eternal", Ornaments["Heroic"]);
        Assert.Equal(OrnamentFamily + "Legendary", Ornaments["Legendary"]);
    }

    [Fact]
    public void EveryRungOnTheLadderNamesTheColourRowItPrintsIn()
    {
        Assert.Equal(
            Export.Loaded.Settings.Rarities.Select(rung => rung.Name).Order(StringComparer.Ordinal),
            ColorRows.Keys.Order(StringComparer.Ordinal));

        Assert.Equal("game.rarity-common", ColorRows["Common"]);
        Assert.Equal("game.rarity-uncommon", ColorRows["Uncommon"]);
        Assert.Equal("game.rarity-rare", ColorRows["Rare"]);
        Assert.Equal("game.rarity-epic", ColorRows["Epic"]);
        Assert.Equal("game.game.rarity-champion", ColorRows["Champion"]);
        Assert.Equal("game.game.rarity-heroic", ColorRows["Heroic"]);
        Assert.Equal("game.rarity-legendary", ColorRows["Legendary"]);
    }

    [Fact]
    public void TheThreeSectionHeadingsNameTheColourRowTheirWidgetDraws()
    {
        Assert.Equal(
            [SectionHeadingRows.Ability, SectionHeadingRows.Legendary, SectionHeadingRows.SetBonus],
            HeadingRows.Keys.Order(StringComparer.Ordinal));

        Assert.Equal("game.game.hero-mosse", HeadingRows[SectionHeadingRows.Ability]);
        Assert.Equal("game.rarity-legendary", HeadingRows[SectionHeadingRows.Legendary]);
        Assert.Equal("game.game.character-friendly", HeadingRows[SectionHeadingRows.SetBonus]);
    }

    [Fact]
    public void EverySectionHeadingRowResolvesToTheHexTheGameDrawsItIn()
    {
        Assert.Equal("EA4F84", Hex(HeadingRows[SectionHeadingRows.Ability]));
        Assert.Equal("FF8000", Hex(HeadingRows[SectionHeadingRows.Legendary]));
        Assert.Equal("00BB1D", Hex(HeadingRows[SectionHeadingRows.SetBonus]));
    }

    [Fact]
    public void EveryRarityColourRowResolvesToAHex()
    {
        Assert.NotEmpty(ColorRows);

        Assert.All(ColorRows, row => Assert.True(
            Export.Loaded.Settings.Colors.ContainsKey(row.Value),
            $"{row.Key} draws in {row.Value}, which the colours table does not hold."));
    }

    [Fact]
    public void EveryOrnamentIsWrittenAsAChromeTexture()
    {
        Assert.NotEmpty(Ornaments);

        Assert.All(Ornaments, ornament => Assert.True(
            File.Exists(Path.Combine(Export.Loaded.Folder, ExportLayout.UiFolder, ornament.Value + ".png")),
            $"{ornament.Key} draws {ornament.Value}, which the export does not write."));
    }
}
