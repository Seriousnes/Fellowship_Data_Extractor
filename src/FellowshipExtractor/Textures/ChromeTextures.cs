using CUE4Parse.FileProvider;
using CUE4Parse_Conversion.Textures;

using FellowshipExtractor.Output;
using FellowshipExtractor.Tooltip;

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

    public static async Task<int> WriteAsync(
        AbstractFileProvider provider, IExportTarget target, CancellationToken cancellationToken)
    {
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

        Console.WriteLine(
            $"\nwriting {textures.Length} tooltip chrome texture(s), " +
            $"{slotIcons.Count} of them the slot glyphs {SlotIcons.Widget} names");

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

            var encoded = TextureExporter.Export(texture, ETextureFormat.Png);
            if (!encoded.Written)
            {
                Console.Error.WriteLine($"{name,-56} did not decode: {encoded.Error}");
                missing++;
                continue;
            }

            await target.WriteImageAsync($"{name}.{encoded.Extension}", encoded.Content!, cancellationToken);
            written++;
        }

        Console.WriteLine($"  {written} of {textures.Length} chrome texture(s) written");
        if (missing > 0) Console.WriteLine($"  {missing} not written, see above");
        Console.WriteLine("  no font is exported: the header face is ArnoPro and is not ours to redistribute");

        return written;
    }
}
