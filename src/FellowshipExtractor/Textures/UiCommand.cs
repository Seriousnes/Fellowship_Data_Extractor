using CUE4Parse_Conversion.Textures;
using FellowshipExtractor.Tooltip;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Textures;

public static class UiCommand
{
    private static readonly string[] Chrome =
    [
        "T_Borders_Frame_TooltipRarityFrame",
        "T_Borders_WindowBackground_TooltipSimple",
        "T_Borders_BGPatterns_SmallIndentAlt",

        "T_Borders_Thin",
        "T_Borders_Rounded_16",
        "T_Borders_WindowBackground_Windowed",

        "T_Backgrounds_TooltipHeader_Rarity",
        "T_Backgrounds_TooltipHeader_Rarity_Alt",
        "T_Backgrounds_TooltipHeader_Subheader",
        "T_Borders_TooltipHeader_Sub",

        "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Champion",
        "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Paragon",
        "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Eternal",
        "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_Legendary",

        "T_Borders_Divider_Simple",
        "T_Borders_Divider_SimpleFade",
        "T_Borders_Divider_Decor",
        "T_Borders_Divider_DecorLine",
        "T_Borders_Divider_End_Fadeout",
        "T_Borders_Divider_End_Nub",
        "T_Borders_Divider_Linebreaker",

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

        "T_Backgrounds_TooltipSlot_Gradient_Decor",
        "T_Backgrounds_TooltipSlot_Gradient_Fade",

        "T_Borders_EmptyWithoutFrame",
        "T_Borders_RarityFrame",
        "T_Borders_GlassFrame",
        "T_Buttons_AbilityBarBackground_Small",
        "T_Buttons_SmallAbilityButton_Default",

        "T_Buttons_TraitNode_SelectedDefault",
        "T_Buttons_TraitNode_SelectedHover",
        "T_Buttons_TraitNode_SelectedPressed",
        "T_Buttons_TraitNode_AvailableDefault",
        "T_Buttons_TraitNode_AvailableHover",
        "T_Buttons_TraitNode_UnavailableDefault",
        "T_Buttons_TraitNode_UnavailableHover",
        "T_Buttons_TraitNode_Color",
        "T_Buttons_TraitNode_Mask",

        "T_Buttons_BlessingButton_Allocated",
        "T_Buttons_BlessingButton_AllocatedHover",
        "T_Buttons_BlessingButton_Unallocated",
        "T_Buttons_BlessingButton_UnallocatedHover",

        "T_Borders_Modifier_Frame_Hexagon",
        "T_Icons_Locked",
        "T_GoldDivider",
    ];

    private static readonly string[] Families = ["T_Icons_BlessingRuneIcon_Allocated_"];

    public static async Task RunAsync(ExtractorOptions options)
    {
        var provider = await ProviderFactory.CreateAsync(options);

        var slotIcons = SlotIcons.Textures(provider);

        var kin = provider.Files.Values
            .Select(file => Path.GetFileNameWithoutExtension(file.Name))
            .Where(name => Families.Any(family => name.StartsWith(family, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        string[] textures = [.. Chrome, .. kin, .. slotIcons.Values.Order(StringComparer.Ordinal)];

        var byName = provider.Files.Values
            .Where(f => f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            .ToLookup(f => Path.GetFileNameWithoutExtension(f.Path), StringComparer.OrdinalIgnoreCase);

        var folder = Path.Combine(options.ResolvedOutputDir, "export", "ui");
        Directory.CreateDirectory(folder);

        Console.WriteLine(
            $"writing {textures.Length} tooltip chrome texture(s) to {folder}, " +
            $"{slotIcons.Count} of them the slot glyphs {SlotIcons.Widget} names\n");

        int written = 0, missing = 0;

        foreach (var name in textures)
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

        Console.WriteLine($"\nwrote {written} of {textures.Length} texture(s) to {folder}");
        if (missing > 0) Console.WriteLine($"  {missing} not written, see above");
        Console.WriteLine("no font is exported: the header face is ArnoPro and is not ours to redistribute");
    }

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
