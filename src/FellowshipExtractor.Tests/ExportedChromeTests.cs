using FellowshipExtractor.Output;
using FellowshipExtractor.Textures;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Tests;

public sealed class ExportedChromeTests
{
    private const string NumeralFamily = "T_Borders_ItemRomanNumerals_";

    private static readonly string[] Tinted =
    [
        "T_Borders_Frame_TooltipRarityFrame",
        "T_Backgrounds_TooltipHeader_Rarity",
        "T_Borders_RarityFrame",
        "T_Borders_RarityGlow",
    ];

    private static readonly string[] SlotStack =
    [
        "T_Borders_EmptyWithoutFrame",
        "T_Borders_Glows_ItemSquare",
        "T_Borders_RarityFrame",
        "T_Borders_RarityGlow",
        "T_Borders_GlassFrame",
        "T_Borders_SlotHoverHighlight",
    ];

    private static readonly string[] Numerals = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII"];

    private static readonly string[] SlotPlates = ["T_Borders_EmptySlot", "T_Icons_Gems_EmptySlot"];

    private static readonly (string Texture, string Row)[] ModifierChrome =
    [
        ("T_Icons_Generic_WeaponSkill", "core.primary-200"),
        ("T_Icons_Generic_TooltipSlot", "core.primary-main"),
        ("T_Icons_Generic_Gem", "core.primary-200"),
        ("T_Backgrounds_TooltipSlot_Gradient_Decor", "core.secondary-600"),
    ];

    private static string Ui(string name) => Path.Combine(Export.Loaded.Folder, ExportLayout.UiFolder, name);

    private static IReadOnlyList<RarityEntry> Ladder => Export.Loaded.Settings.Rarities;

    private static Rgba32 Colour(string rarity) =>
        Color.ParseHex(Export.Loaded.Settings.Colors[Export.Loaded.Settings.RarityColorRows[rarity]])
            .ToPixel<Rgba32>();

    [Fact]
    public void TheGoldRuleIsWrittenAsFourRowsAndEveryOneOfThemIsPainted()
    {
        var file = Ui(GoldBand.Texture + ".png");
        Assert.True(File.Exists(file), $"{GoldBand.Texture} was not written.");

        using var band = Image.Load<Rgba32>(file);

        Assert.Equal(GoldBand.Height, band.Height);

        var blank = new List<string>();

        for (var y = 0; y < band.Height; y++)
        {
            var painted = false;
            for (var x = 0; x < band.Width && !painted; x++) painted = band[x, y].A > 0;

            if (!painted) blank.Add($"row {y} of the band is empty");
        }

        Assert.Empty(blank);
    }

    [Fact]
    public void TheGoldRuleIsCroppedFromRowsTwentyFourToTwentySevenOfAFiftySixRowCanvas()
    {
        using var canvas = new Image<Rgba32>(2, GoldBand.CanvasHeight);

        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++) canvas[x, y] = new Rgba32((byte)y, 0, 0, byte.MaxValue);
        }

        using var band = Image.Load<Rgba32>(GoldBand.Crop(canvas));

        Assert.Equal(GoldBand.Height, band.Height);
        Assert.Equal(
            [GoldBand.FirstRow, GoldBand.FirstRow + 1, GoldBand.FirstRow + 2, GoldBand.LastRow],
            [.. Enumerable.Range(0, band.Height).Select(y => (int)band[0, y].R)]);

        using var shorter = new Image<Rgba32>(2, GoldBand.CanvasHeight - 1);

        var refused = Assert.Throws<InvalidOperationException>(() => GoldBand.Crop(shorter));
        Assert.Contains(GoldBand.Texture, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EachOfTheFourTintedTexturesIsWrittenOncePerRarityBesideItsUntintedSelf()
    {
        var missing = new List<string>();

        foreach (var texture in Tinted)
        {
            if (!File.Exists(Ui(texture + ".png"))) missing.Add($"{texture}.png");

            foreach (var rung in Ladder)
            {
                var variant = ExportLayout.Variant(texture + ".png", rung.Name);
                if (!File.Exists(Ui(variant))) missing.Add(variant);
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryTintedVariantIsItsTextureMultipliedByTheRarityColourWithTheAlphaUntouched()
    {
        var wrong = new List<string>();

        foreach (var texture in Tinted)
        {
            using var source = Image.Load<Rgba32>(Ui(texture + ".png"));

            foreach (var rung in Ladder)
            {
                var variant = ExportLayout.Variant(texture + ".png", rung.Name);
                using var tinted = Image.Load<Rgba32>(Ui(variant));

                if (Mismatch(source, tinted, Colour(rung.Name)) is { } reading) wrong.Add($"{variant} {reading}");
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void EveryTextureTheSlotStackPaintsIsWritten()
    {
        string[] stack = [.. SlotStack, .. Numerals.Select(numeral => NumeralFamily + numeral)];
        string[] missing = [.. stack.Where(texture => !File.Exists(Ui(texture + ".png")))];

        Assert.Empty(missing);
    }

    [Fact]
    public void TheEmptySlotPlateAndTheEmptyGemSocketAreWritten()
    {
        string[] missing = [.. SlotPlates.Where(texture => !File.Exists(Ui(texture + ".png")))];

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryTextureAModifierRowDrawsIsWrittenPlainAndMultipliedByTheColourRowItNames()
    {
        var wrong = new List<string>();

        foreach (var (texture, row) in ModifierChrome)
        {
            var plain = Ui(texture + ".png");

            if (!File.Exists(plain))
            {
                wrong.Add($"{texture}.png was not written");
                continue;
            }

            var variant = ExportLayout.Variant(texture + ".png", row.Replace('.', '-'));

            if (!File.Exists(Ui(variant)))
            {
                wrong.Add($"{variant} was not written");
                continue;
            }

            using var source = Image.Load<Rgba32>(plain);
            using var painted = Image.Load<Rgba32>(Ui(variant));

            var colour = Color.ParseHex(Export.Loaded.Settings.Colors[row]).ToPixel<Rgba32>();

            if (Mismatch(source, painted, colour) is { } reading) wrong.Add($"{variant} {reading}");
        }

        Assert.Empty(wrong);
    }

    private static string? Mismatch(Image<Rgba32> source, Image<Rgba32> tinted, Rgba32 colour)
    {
        if (tinted.Width != source.Width || tinted.Height != source.Height)
        {
            return $"is {tinted.Width}x{tinted.Height} where the texture is {source.Width}x{source.Height}";
        }

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var was = source[x, y];

                var expected = new Rgba32(
                    (byte)(was.R * colour.R / 255),
                    (byte)(was.G * colour.G / 255),
                    (byte)(was.B * colour.B / 255),
                    was.A);

                if (tinted[x, y] != expected) return $"reads {tinted[x, y]} at {x},{y} where the multiply gives {expected}";
            }
        }

        return null;
    }
}
