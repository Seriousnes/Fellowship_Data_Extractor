using CUE4Parse_Conversion.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Textures;

/// <summary>
/// Writes the item tooltip's chrome to output/export/ui as PNG, so a renderer outside the game can
/// rebuild the tooltip out of the shipped art instead of redrawing it. Only textures: no font file
/// is exported, because the header face is ArnoPro and redistributing it is not ours to do.
/// </summary>
public static class UiCommand
{
    /// <summary>
    /// The tooltip's chrome, named one texture at a time. Nothing in the build enumerates it: the
    /// widget blueprints reference each brush individually, and the DesignSystem texture tree these
    /// all come from holds far more than a tooltip uses, so the set is a curated selection rather
    /// than a query. It is grouped by the part of the layout it serves, and every name resolves to
    /// exactly one of the build's 46,437 packages.
    /// </summary>
    private static readonly string[] Chrome =
    [
        // Frame and body. The rarity frame is not a four-sided box: row 0 is a solid rule across
        // the full width (mean alpha 243), rows 1 to 87 carry only the two side rails with alpha
        // decaying 255 to 1 down the canvas, and rows 88 to 105 are empty. Nine-slicing it
        // vertically would flatten that fade into a constant rail, so it stretches horizontally
        // only. WindowBackground_TooltipSimple is the box that does nine-slice: a 65% opaque
        // #121D28 fill inside a two-pixel #A88956 border whose four corner pixels are cut away.
        "T_Borders_Frame_TooltipRarityFrame",
        "T_Borders_WindowBackground_TooltipSimple",
        "T_Borders_BGPatterns_SmallIndentAlt",

        // Header band. TooltipHeader_Rarity is centre-weighted horizontally, alpha peaking at 223
        // over columns 134 and 135 of 270 and falling to 49 at both edges, over a 223 rule on the
        // last row. That gradient is the shape, so this stretches as a whole and cannot be
        // nine-sliced either. Subheader and TooltipHeader_Sub are that same band baked opaque for
        // sections that take no rarity tint: both run mean luma 26 at the edges to 39 at the centre
        // column with a 46 against 36 lift on the last row, and the two agree to within 1/255 in
        // every corner, so they are interchangeable.
        "T_Backgrounds_TooltipHeader_Rarity",
        "T_Backgrounds_TooltipHeader_Rarity_Alt",
        "T_Backgrounds_TooltipHeader_Subheader",
        "T_Borders_TooltipHeader_Sub",

        // The four rarity ornaments that sit in the header. Pre-coloured, and each one's most
        // saturated pixel reproduces a palette entry to within 1/255, which is what lets the
        // ornament be chosen by matching colours rather than by trusting the legacy file names.
        "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Champion",
        "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Paragon",
        "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Eternal",
        "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Legendary",

        // Dividers. The tooltip draws four of them and the build ships several weights, so all of
        // them go out and the renderer picks: gold for Simple, Decor, DecorLine and End_Fadeout,
        // pale #C3CFD9 for SimpleFade, and a flat #28323C hairline for Linebreaker.
        "T_Borders_Divider_Simple",
        "T_Borders_Divider_SimpleFade",
        "T_Borders_Divider_Decor",
        "T_Borders_Divider_DecorLine",
        "T_Borders_Divider_End_Fadeout",
        "T_Borders_Divider_End_Nub",
        "T_Borders_Divider_Linebreaker",

        // Glyphs. The three Generic ones are white masks the renderer tints; the StatTooltip ones
        // are pre-coloured badges that name their own section, and Arrows_Right is the chevron the
        // trait rows end with. Finesse is the build's codename for what the game prints as
        // Blessing, so TooltipFinesse is the Blessings glyph: Abilities_Feature_Finesse_Name reads
        // "Blessing" and its three sibling keys read "Blessings", "Blessing:" and "Blessing Rank:".
        "T_Icons_Generic_TooltipSlot",
        "T_Icons_Generic_TooltipExpand",
        "T_Icons_Generic_TooltipFinesse",
        "T_Icons_Progression_ItemLevel",
        "T_Icons_StatTooltip_TraitSlot",
        "T_Icons_StatTooltip_AbilitySlot_Pink",
        "T_Icons_StatTooltip_Legendary",
        "T_Icons_StatTooltip_Set_Bonus",
        "T_Icons_StatTooltip_RandomSlot",
        "T_Icons_Arrows_Right",

        // Slot decoration: a bracket and a vertical alpha ramp that runs 0 to 255 over its first
        // 61 rows and holds there.
        "T_Backgrounds_TooltipSlot_Gradient_Decor",
        "T_Backgrounds_TooltipSlot_Gradient_Fade",

        // The square an item's own art sits in. In the client the tooltip never draws the icon,
        // because the icon is the thing being hovered, so a page showing both has to draw the bag
        // square itself: EmptySlot is the finished dark plate behind the art and RarityFrame is the
        // white chamfered outline over it, tinted per rung the same way the tooltip frame is.
        "T_Borders_EmptySlot",
        "T_Borders_RarityFrame",
    ];

    public static async Task RunAsync(ExtractorOptions options)
    {
        var provider = await ProviderFactory.CreateAsync(options);

        // The curated names are bare so the container's folder layout stays derived rather than
        // written down a second time. A name that stops matching exactly one package is reported
        // rather than skipped, because that means the build moved the art.
        var byName = provider.Files.Values
            .Where(f => f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            .ToLookup(f => Path.GetFileNameWithoutExtension(f.Path), StringComparer.OrdinalIgnoreCase);

        var folder = Path.Combine(options.ResolvedOutputDir, "export", "ui");
        Directory.CreateDirectory(folder);

        Console.WriteLine($"writing {Chrome.Length} tooltip chrome texture(s) to {folder}\n");

        int written = 0, missing = 0;

        foreach (var name in Chrome)
        {
            var packages = byName[name].ToArray();
            if (packages.Length != 1)
            {
                Console.Error.WriteLine($"{name,-56} {packages.Length} packages match, expected one");
                missing++;
                continue;
            }

            var texture = TextureExporter.Load(provider, packages[0].Path);
            if (texture is null)
            {
                Console.Error.WriteLine($"{name,-56} {packages[0].Path} did not load as a texture");
                missing++;
                continue;
            }

            var export = TextureExporter.Export(
                texture, Path.Combine(folder, name), ETextureFormat.Png);

            if (!export.Written)
            {
                Console.Error.WriteLine($"{name,-56} did not decode: {export.Error}");
                missing++;
                continue;
            }

            written++;
            Console.WriteLine(
                $"{name,-56} {export.Width,4}x{export.Height,-4} {texture.Format,-14} " +
                $"{Describe(export.File!),-28} {new FileInfo(export.File!).Length,8} bytes");
        }

        Console.WriteLine($"\nwrote {written} of {Chrome.Length} texture(s) to {folder}");
        if (missing > 0) Console.WriteLine($"  {missing} not written, see above");
        Console.WriteLine("no font is exported: the header face is ArnoPro and is not ours to redistribute");
    }

    /// <summary>
    /// Whether a texture carries its own colour or only a shape the renderer has to tint, read back
    /// off the file that was just written so the description belongs to the shipped bytes. A
    /// tintable one is neutral in every visible pixel; the masks in this set measure a channel
    /// spread of exactly zero, compression included, so the test is equality rather than a
    /// tolerance. Coloured art is described by its most saturated visible pixel, which is the one
    /// that says which rarity or section a badge belongs to, not by its most opaque, which for all
    /// four ornaments is the same near-black outline.
    /// </summary>
    private static string Describe(string file)
    {
        using var image = Image.Load<Rgba32>(file);

        var neutral = true;
        int minLuma = 255, maxLuma = 0, bestChroma = -1, bestLuma = -1;
        Rgba32 saturated = default;

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    if (p.A == 0) continue;

                    var hi = Math.Max(p.R, Math.Max(p.G, p.B));
                    var lo = Math.Min(p.R, Math.Min(p.G, p.B));
                    if (hi != lo) neutral = false;

                    minLuma = Math.Min(minLuma, hi);
                    maxLuma = Math.Max(maxLuma, hi);

                    var chroma = hi - lo;
                    if (p.A >= 128 && (chroma > bestChroma || (chroma == bestChroma && hi > bestLuma)))
                    {
                        bestChroma = chroma;
                        bestLuma = hi;
                        saturated = p;
                    }
                }
            }
        });

        return neutral
            ? $"mask, luma {minLuma} to {maxLuma}"
            : $"colour, peak #{saturated.R:X2}{saturated.G:X2}{saturated.B:X2}";
    }
}
