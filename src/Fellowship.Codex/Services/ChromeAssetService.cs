using System.Diagnostics.CodeAnalysis;

using Azure.Storage.Blobs;

using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface IChromeAssetService
{
    string LockGlyph { get; }

    string GoldDivider { get; }

    string Panel { get; }

    string Window { get; }

    string RoundedWindow { get; }

    string ThinWindow { get; }

    string WindowFill { get; }

    string ItemPowerGlyph { get; }

    string Rule { get; }

    string TraitGlyph { get; }

    string ExpandGlyph { get; }

    string ModifierPlate { get; }

    string ModifierDecor { get; }

    string ModifierGlyph(ModifierKind kind);

    Badge AbilityBadge { get; }

    Badge LegendaryBadge { get; }

    Badge SetBonusBadge { get; }

    string? Frame(string? rarity);

    string Header(string? rarity);

    string? Ornament(string rarity);

    string SlotBacking(string? rarity);

    string SlotFrame(string? rarity);

    string? SlotBevel(string? rarity);

    string SlotPlate { get; }

    [return: NotNullIfNotNull(nameof(icon))]
    string? Art(string? icon);

    string TraitNode(string? state);

    string BlessingPlate(string? state);

    string? BlessingArt(string? icon, string? state);

    string SlotGlyph(string? slot);
}
public sealed class ChromeAssetService(
    BlobContainerClient container,
    IEntityStoreService store,
    IDesignTokenService tokens) : IChromeAssetService
{
    public const string Prefix = "ui";

    private const string FrameMask = "T_Borders_Frame_TooltipRarityFrame";
    private const string IconFrameMask = "T_Borders_RarityFrame";
    private const string HeaderMask = "T_Backgrounds_TooltipHeader_Rarity";

    private const string PanelTexture = "T_Borders_WindowBackground_TooltipSimple";
    private const string WindowTexture = "T_Borders_BGPatterns_SmallIndentAlt";
    private const string RoundedWindowTexture = "T_Borders_Rounded_16";
    private const string ThinWindowTexture = "T_Borders_Thin";
    private const string WindowFillTexture = "T_Borders_WindowBackground_Windowed";
    private const string NeutralHeaderTexture = "T_Backgrounds_TooltipHeader_Subheader";
    private const string SlotTexture = "T_Icons_Generic_TooltipSlot";
    private const string ItemPowerTexture = "T_Icons_Progression_ItemLevel";
    private const string RuleTexture = "T_Borders_Divider_Simple";
    private const string AbilityTexture = "T_Icons_StatTooltip_AbilitySlot_Pink";
    private const string LegendaryTexture = "T_Icons_StatTooltip_Legendary";
    private const string SetBonusTexture = "T_Icons_StatTooltip_Set_Bonus";
    private const string SlotBackingTexture = "T_Borders_EmptyWithoutFrame";
    private const string SlotBevelTexture = "T_Borders_GlassFrame";
    private const string SlotPlateTexture = "T_Borders_EmptySlot";
    private const string PlainSlotBackingTexture = "T_Buttons_AbilityBarBackground_Small";
    private const string PlainSlotFrameTexture = "T_Buttons_SmallAbilityButton_Default";
    private const string TraitTexture = "T_Icons_StatTooltip_TraitSlot";
    private const string ExpandTexture = "T_Icons_Generic_TooltipExpand";

    private const string ModifierPlateTexture = WindowFillTexture;
    private const string ModifierDecorTexture = "T_Backgrounds_TooltipSlot_Gradient_Decor";
    private const string TraitModifierTexture = "T_Icons_Generic_WeaponSkill";
    private const string BlessingModifierTexture = "T_Icons_Generic_TooltipSlot";
    private const string EssenceModifierTexture = "T_Icons_Generic_Gem";

    private const string NodeFamily = "T_Buttons_TraitNode_";
    private const string NodeSuffix = "Default";
    private const string BlessingFamily = "T_Buttons_BlessingButton_";
    private const string BlessingArtFamily = "T_Icons_BlessingRuneIcon_";
    private const string Allocated = "Allocated";
    private const string Unallocated = "Unallocated";
    private const string Unavailable = "Unavailable";
    private const string LockTexture = "T_Icons_Locked";
    private const string DividerTexture = "T_GoldDivider";

    private const string AbilityHeading = "Ability";
    private const string LegendaryHeading = "Legendary";
    private const string SetBonusHeading = "SetBonus";

    private static readonly IReadOnlyDictionary<ModifierKind, string> ModifierGlyphs = Everywhere(
        new Dictionary<ModifierKind, string>
        {
            [ModifierKind.Trait] = TraitModifierTexture,
            [ModifierKind.Blessing] = BlessingModifierTexture,
            [ModifierKind.Essence] = EssenceModifierTexture,
        });

    private static readonly IReadOnlyDictionary<string, string> ModifierTinted =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TraitModifierTexture] = "core.primary-200",
            [BlessingModifierTexture] = "core.primary-main",
            [EssenceModifierTexture] = "core.primary-200",
            [ModifierDecorTexture] = "core.secondary-600",
        };

    private static IReadOnlyDictionary<ModifierKind, string> Everywhere(
        IReadOnlyDictionary<ModifierKind, string> glyphs)
    {
        var missing = Enum.GetValues<ModifierKind>().Where(kind => !glyphs.ContainsKey(kind)).ToArray();

        return missing.Length == 0
            ? glyphs
            : throw new InvalidOperationException(
                $"{string.Join(", ", missing)} {(missing.Length == 1 ? "names" : "name")} no modifier " +
                $"glyph texture in {nameof(ChromeAssetService)}, so a row of that kind draws no glyph.");
    }

    public static readonly IReadOnlyList<string> NodeStates = ["selected", "available", "unavailable"];

    public static readonly IReadOnlyList<string> BlessingStates = ["allocated", "unallocated"];

    public string? Frame(string? rarity) => rarity is null ? null : Url(Name(FrameMask, rarity));

    public string Header(string? rarity) =>
        rarity is null ? Url(Name(NeutralHeaderTexture)) : Url(Name(HeaderMask, rarity));

    public string? Ornament(string rarity) =>
        store.Settings.RarityOrnaments.TryGetValue(rarity, out var texture) ? Url(Name(texture)) : null;

    public string SlotBacking(string? rarity) =>
        Url(Name(rarity is null ? PlainSlotBackingTexture : SlotBackingTexture));

    public string SlotFrame(string? rarity) =>
        rarity is null ? Url(Name(PlainSlotFrameTexture)) : Url(Name(IconFrameMask, rarity));

    public string? SlotBevel(string? rarity) => rarity is null ? null : Url(Name(SlotBevelTexture));

    public string SlotPlate => Url(Name(SlotPlateTexture));

    [return: NotNullIfNotNull(nameof(icon))]
    public string? Art(string? icon) => icon is null ? null : Url($"{Prefix}/{icon}");

    public string TraitNode(string? state) =>
        Url(Name(NodeFamily + Chosen(NodeStates, state) + NodeSuffix));

    public static bool Padlocked(string? state) =>
        string.Equals(Chosen(NodeStates, state), Unavailable, StringComparison.Ordinal);

    public string BlessingPlate(string? state) =>
        Url(Name(BlessingFamily + Chosen(BlessingStates, state)));

    public string? BlessingArt(string? icon, string? state) =>
        icon is { } art &&
        art.StartsWith(BlessingArtFamily + Unallocated, StringComparison.Ordinal) &&
        string.Equals(Chosen(BlessingStates, state), Allocated, StringComparison.Ordinal)
            ? Art(art.Replace(Unallocated, Allocated, StringComparison.Ordinal))
            : null;

    public string LockGlyph => Url(Name(LockTexture));

    public string GoldDivider => Url(Name(DividerTexture));

    public string Panel => Url(Name(PanelTexture));

    public string Window => Url(Name(WindowTexture));

    public string RoundedWindow => Url(Name(RoundedWindowTexture));

    public string ThinWindow => Url(Name(ThinWindowTexture));

    public string WindowFill => Url(Name(WindowFillTexture));

    public string SlotGlyph(string? slot) =>
        Url(Name(slot is not null && store.Settings.SlotIcons.TryGetValue(slot, out var gear)
            ? gear
            : SlotTexture));

    public string ItemPowerGlyph => Url(Name(ItemPowerTexture));

    public string Rule => Url(Name(RuleTexture));

    public string TraitGlyph => Url(Name(TraitTexture));

    public string ExpandGlyph => Url(Name(ExpandTexture));

    public string ModifierPlate => Url(Name(ModifierPlateTexture));

    public string ModifierDecor => Url(Tinted(ModifierDecorTexture));

    public string ModifierGlyph(ModifierKind kind) => Url(Tinted(ModifierGlyphs[kind]));

    private static string Tinted(string texture) =>
        Name(texture, DesignTokenService.Variable(ModifierTinted[texture]));

    public Badge AbilityBadge => Section(AbilityTexture, AbilityHeading);

    public Badge LegendaryBadge => Section(LegendaryTexture, LegendaryHeading);

    public Badge SetBonusBadge => Section(SetBonusTexture, SetBonusHeading);

    private Badge Section(string texture, string heading) => new(Url(Name(texture)), Colour(heading));

    private string? Colour(string heading) =>
        store.Settings.SectionHeadingRows.TryGetValue(heading, out var row) ? tokens.Color(row) : null;

    private static string Name(string texture) => $"{Prefix}/{texture}.png";

    private static string Name(string texture, string rarity) =>
        $"{Prefix}/{texture}-{rarity.ToLowerInvariant()}.png";

    private string Url(string blob) => $"{container.Uri}/{blob}";

    private static string Chosen(IReadOnlyList<string> states, string? state) =>
        states.FirstOrDefault(known => string.Equals(known, state, StringComparison.OrdinalIgnoreCase))
            is { } found
            ? char.ToUpperInvariant(found[0]) + found[1..]
            : char.ToUpperInvariant(states[0][0]) + states[0][1..];
}
