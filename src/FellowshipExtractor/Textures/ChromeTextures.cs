using CUE4Parse.FileProvider;
using CUE4Parse_Conversion.Textures;

using FellowshipExtractor.Output;
using FellowshipExtractor.Extraction;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Textures;

public static class ChromeTextures
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
        "T_Icons_Generic_WeaponSkill",
        "T_Icons_Generic_Gem",
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
        "T_Borders_RarityGlow",
        "T_Borders_GlassFrame",
        "T_Borders_SlotHoverHighlight",
        "T_Borders_Glows_ItemSquare",
        "T_Borders_EmptySlot",
        "T_Icons_Gems_EmptySlot",
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

        "T_Backgrounds_TalentTreeTierBackground_Locked",
        "T_Backgrounds_TalentTreeTierBackground_Unlocked",
        "T_Borders_Frame_TalentTreeCost",
        "T_Borders_Frame_Pattern",

        GoldBand.Texture,
    ];

    private static readonly string[] Families =
    [
        "T_Icons_BlessingRuneIcon_Allocated_",
        "T_Borders_ItemRomanNumerals_",
        "T_Buttons_TalentTreeButton_",
    ];

    private static readonly string[] Tinted =
    [
        "T_Borders_Frame_TooltipRarityFrame",
        "T_Backgrounds_TooltipHeader_Rarity",
        "T_Borders_RarityFrame",
        "T_Borders_RarityGlow",
    ];

    private static readonly IReadOnlyDictionary<string, string> RowTinted =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["T_Icons_Generic_WeaponSkill"] = "core.primary-200",
            ["T_Icons_Generic_TooltipSlot"] = "core.primary-main",
            ["T_Icons_Generic_Gem"] = "core.primary-200",
            ["T_Backgrounds_TooltipSlot_Gradient_Decor"] = "core.secondary-600",
            ["T_Borders_Frame_Pattern"] = "core.primary-200",
        };

    public static async Task<int> WriteAsync(
        AbstractFileProvider provider,
        Settings settings,
        IExportTarget target,
        CancellationToken cancellationToken)
    {
        var slotIcons = SlotIcons.Textures(provider);
        var ladder = RarityImages.Ladder(settings);

        var kin = provider.Files.Values
            .Select(file => Path.GetFileNameWithoutExtension(file.Name))
            .Where(name => Families.Any(family => name.StartsWith(family, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        string[] textures =
        [
            .. Chrome,
            .. settings.RarityOrnaments.Values.Order(StringComparer.Ordinal),
            .. settings.TalentTierPlates.Values.Order(StringComparer.Ordinal),
            .. kin,
            .. slotIcons.Values.Order(StringComparer.Ordinal),
        ];

        var byName = provider.Files.Values
            .Where(f => f.Path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
            .ToLookup(f => Path.GetFileNameWithoutExtension(f.Path), StringComparer.OrdinalIgnoreCase);

        Console.WriteLine(
            $"\nwriting {textures.Length} tooltip chrome texture(s), " +
            $"{slotIcons.Count} of them the slot glyphs {SlotIcons.Widget} names");

        int written = 0, missing = 0, variants = 0;

        var painted = new HashSet<string>(StringComparer.Ordinal);

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

            var encoded = TextureExporter.Export(texture, ETextureFormat.Png);
            if (!encoded.Written)
            {
                Console.Error.WriteLine($"{name,-56} did not decode: {encoded.Error}");
                missing++;
                continue;
            }

            var image = $"{name}.{encoded.Extension}";
            var content = encoded.Content!;

            if (name.Equals(GoldBand.Texture, StringComparison.Ordinal))
            {
                using var canvas = Image.Load<Rgba32>(content);
                content = GoldBand.Crop(canvas);
            }

            await target.WriteImageAsync(image, content, cancellationToken);
            written++;

            if (RowTinted.TryGetValue(name, out var row))
            {
                using var glyph = Image.Load<Rgba32>(content);

                await target.WriteImageAsync(
                    ExportLayout.Variant(image, Variable(row)),
                    RarityImages.Tint(glyph, Colour(settings, row)),
                    cancellationToken);

                painted.Add(name);
            }

            if (!Array.Exists(Tinted, tinted => tinted.Equals(name, StringComparison.Ordinal))) continue;

            using var mask = Image.Load<Rgba32>(content);

            foreach (var rung in ladder)
            {
                await target.WriteImageAsync(
                    ExportLayout.Variant(image, rung.Rarity),
                    RarityImages.Tint(mask, rung.Colour),
                    cancellationToken);

                variants++;
            }
        }

        if (painted.Count != RowTinted.Count)
        {
            var absent = RowTinted.Keys
                .Where(texture => !painted.Contains(texture))
                .Order(StringComparer.Ordinal);

            throw new InvalidOperationException(
                $"The export wrote no {string.Join(", ", absent)}, so a modifier row drawing it has no glyph. " +
                "Every texture a modifier row draws is written multiplied by the colour row it names.");
        }

        Console.WriteLine($"  {written} of {textures.Length} chrome texture(s) written");
        Console.WriteLine(
            $"  {variants} rarity variants, {Tinted.Length} textures multiplied by each of " +
            $"{ladder.Count} rarity colours");
        Console.WriteLine(
            $"  {painted.Count} modifier row texture(s), each multiplied by the colour row the row draws it in");
        Console.WriteLine(
            $"  {GoldBand.Texture} written as rows {GoldBand.FirstRow} to {GoldBand.LastRow} of its " +
            $"{GoldBand.CanvasHeight} row canvas");
        if (missing > 0) Console.WriteLine($"  {missing} not written, see above");
        Console.WriteLine("  no font is exported: the header face is ArnoPro and is not ours to redistribute");

        return written + variants + painted.Count;
    }

    private static string Variable(string row) => row.Replace('.', '-');

    private static Rgba32 Colour(Settings settings, string row) =>
        settings.Colors.TryGetValue(row, out var hex)
            ? Color.ParseHex(hex).ToPixel<Rgba32>()
            : throw new InvalidOperationException(
                $"{row} is not a row the named colour table carries, so the modifier chrome it paints would be " +
                "multiplied by no colour at all.");
}
