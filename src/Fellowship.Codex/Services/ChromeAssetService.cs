using System.Diagnostics.CodeAnalysis;

using Fellowship.SDK;
using Fellowship.SDK.Documents;

using Fellowship.Codex.Models;

namespace Fellowship.Codex.Services;

public interface IChromeAssetService
{
    string LockGlyph { get; }

    string GoldDivider { get; }

    string SimpleDivider { get; }

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

    string ModifierGlyph(ModifierKind? kind);

    Badge AbilityBadge { get; }

    Badge LegendaryBadge { get; }

    string? Frame(string? rarity);

    string Header(string? rarity);

    string? Ornament(string rarity);

    string SlotBacking(string? rarity);

    string SlotFrame(string? rarity);

    string? SlotBevel(string? rarity);

    string SlotPlate { get; }

    string GemSocketPlate { get; }

    [return: NotNullIfNotNull(nameof(icon))]
    string? Texture(string? icon);

    string TraitNode(string? state);

    string TraitNodeColor(IReadOnlyList<string> categories);

    string TalentBand { get; }

    string TalentTier(int row);

    string TalentSlot(string? state);

    string TalentCost { get; }

    string TalentFrame { get; }

    string BlessingPlate(string? state);

    string? BlessingTexture(string? icon, string? state);

    string SlotGlyph(string? slot);
}
public sealed class ChromeAssetService(
    ITextureSource texture,
    IEntityStoreService store,
    IDesignTokenService tokens) : IChromeAssetService
{
    public const string Prefix = TextureSizes.Folder;

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
    private const string SlotBackingTexture = "T_Borders_EmptyWithoutFrame";
    private const string SlotBevelTexture = "T_Borders_GlassFrame";
    private const string SlotPlateTexture = "T_Borders_EmptySlot";
    private const string GemSocketTexture = "T_Icons_Gems_EmptySlot";
    private const string PlainSlotBackingTexture = "T_Buttons_AbilityBarBackground_Small";
    private const string PlainSlotFrameTexture = "T_Buttons_SmallAbilityButton_Default";
    private const string TalentBandTexture = "T_Backgrounds_TalentTreeTierBackground_Unlocked";
    private const string TalentCostTexture = "T_Borders_Frame_TalentTreeCost";
    private const string TalentButtonFamily = "T_Buttons_TalentTreeButton_";
    private const string TalentFrameTexture = "T_Borders_Frame_Pattern";

    private const string TraitTexture = "T_Icons_StatTooltip_TraitSlot";
    private const string ExpandTexture = "T_Icons_Generic_TooltipExpand";

    private const string ModifierPlateTexture = WindowFillTexture;
    private const string ModifierDecorTexture = "T_Backgrounds_TooltipSlot_Gradient_Decor";
    private const string TraitModifierTexture = "T_Icons_Generic_WeaponSkill";
    private const string BlessingModifierTexture = "T_Icons_Generic_TooltipSlot";
    private const string EssenceModifierTexture = "T_Icons_Generic_Gem";
    private const string RandomModifierTexture = BlessingModifierTexture;

    private const string NodeFamily = "T_Buttons_TraitNode_";
    private const string NodeSuffix = "Default";
    private const string NodeColorTexture = "T_Buttons_TraitNode_Color";
    private const string BlessingFamily = "T_Buttons_BlessingButton_";
    private const string BlessingTextureFamily = "T_Icons_BlessingRuneIcon_";
    private const string Allocated = "Allocated";
    private const string Unallocated = "Unallocated";
    private const string Unavailable = "Unavailable";
    private const string LockTexture = "T_Icons_Locked";
    private const string DividerTexture = "T_GoldDivider";
    private const string SimpleDividerTexture = "T_SimpleDivider";

    private const string AbilityHeading = "Ability";
    private const string LegendaryHeading = "Legendary";

    private static readonly IReadOnlyDictionary<ModifierKind, string> ModifierGlyphs = Everywhere(
        new Dictionary<ModifierKind, string>
        {
            [ModifierKind.Trait] = TraitModifierTexture,
            [ModifierKind.Blessing] = BlessingModifierTexture,
            [ModifierKind.Essence] = EssenceModifierTexture,
        });

    private static readonly IReadOnlyDictionary<string, string> RowTinted =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TraitModifierTexture] = "core.primary-200",
            [BlessingModifierTexture] = "core.primary-main",
            [EssenceModifierTexture] = "core.primary-200",
            [ModifierDecorTexture] = "core.secondary-600",
            [TalentFrameTexture] = "core.primary-200",
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

    public static readonly IReadOnlyList<string> TalentStates = ["disabled", "selected", "default", "hover"];

    public string? Frame(string? rarity) => rarity is null ? null : Url(Name(FrameMask, rarity));

    public string Header(string? rarity) =>
        rarity is null ? Url(NeutralHeaderTexture) : Url(Name(HeaderMask, rarity));

    public string? Ornament(string rarity) =>
        store.Settings.RarityOrnaments.TryGetValue(rarity, out var texture) ? Url(texture) : null;

    public string SlotBacking(string? rarity) =>
        Url(rarity is null ? PlainSlotBackingTexture : SlotBackingTexture);

    public string SlotFrame(string? rarity) =>
        rarity is null ? Url(PlainSlotFrameTexture) : Url(Name(IconFrameMask, rarity));

    public string? SlotBevel(string? rarity) => rarity is null ? null : Url(SlotBevelTexture);

    public string SlotPlate => Url(SlotPlateTexture);

    public string GemSocketPlate => Url(GemSocketTexture);

    [return: NotNullIfNotNull(nameof(icon))]
    public string? Texture(string? icon) => icon is null ? null : Url(icon);

    public string TraitNode(string? state) =>
        Url(NodeFamily + Chosen(NodeStates, state) + NodeSuffix);

    public string TraitNodeColor(IReadOnlyList<string> categories)
    {
        var rows = store.Settings.TraitNodeColorRows
            ?? throw new InvalidOperationException(
                "The settings name no trait node colour rows, so a trait tree node has no category colour.");

        var row = categories
            .Select(category => rows.ByCategory.GetValueOrDefault(category))
            .FirstOrDefault(found => found is not null) ?? rows.Otherwise;

        return Url(Name(NodeColorTexture, DesignTokenService.Variable(row)));
    }

    public string TalentBand => Url(TalentBandTexture);

    public string TalentTier(int row) =>
        store.Settings.TalentTierPlates.TryGetValue(row, out var plate)
            ? Url(plate)
            : throw new InvalidOperationException(
                $"The build has no numbered plate for talent row {row}, only rows " +
                $"{string.Join(", ", store.Settings.TalentTierPlates.Keys)}. A row draws its numeral from " +
                "that plate.");

    public string TalentSlot(string? state) =>
        Url(TalentButtonFamily + Chosen(TalentStates, state));

    public string TalentCost => Url(TalentCostTexture);

    public string TalentFrame => Url(Tinted(TalentFrameTexture));

    public static bool Padlocked(string? state) =>
        string.Equals(Chosen(NodeStates, state), Unavailable, StringComparison.Ordinal);

    public string BlessingPlate(string? state) =>
        Url(BlessingFamily + Chosen(BlessingStates, state));

    public string? BlessingTexture(string? icon, string? state) =>
        icon is { } texture &&
        texture.StartsWith(BlessingTextureFamily + Unallocated, StringComparison.Ordinal) &&
        string.Equals(Chosen(BlessingStates, state), Allocated, StringComparison.Ordinal)
            ? Texture(texture.Replace(Unallocated, Allocated, StringComparison.Ordinal))
            : null;

    public string LockGlyph => Url(LockTexture);

    public string GoldDivider => Url(DividerTexture);

    public string SimpleDivider => Url(SimpleDividerTexture);

    public string Panel => Url(PanelTexture);

    public string Window => Url(WindowTexture);

    public string RoundedWindow => Url(RoundedWindowTexture);

    public string ThinWindow => Url(ThinWindowTexture);

    public string WindowFill => Url(WindowFillTexture);

    public string SlotGlyph(string? slot) =>
        Url(slot is not null && store.Settings.SlotIcons.TryGetValue(slot, out var gear)
            ? gear
            : SlotTexture);

    public string ItemPowerGlyph => Url(ItemPowerTexture);

    public string Rule => Url(RuleTexture);

    public string TraitGlyph => Url(TraitTexture);

    public string ExpandGlyph => Url(ExpandTexture);

    public string ModifierPlate => Url(ModifierPlateTexture);

    public string ModifierDecor => Url(Tinted(ModifierDecorTexture));

    public string ModifierGlyph(ModifierKind? kind) =>
        kind is { } named ? Url(Tinted(ModifierGlyphs[named])) : Url(RandomModifierTexture);

    private static string Tinted(string texture) =>
        Name(texture, DesignTokenService.Variable(RowTinted[texture]));

    public Badge AbilityBadge => Section(AbilityTexture, AbilityHeading);

    public Badge LegendaryBadge => Section(LegendaryTexture, LegendaryHeading);

    private Badge Section(string texture, string heading) => new(Url(texture), Colour(heading));

    private string? Colour(string heading) =>
        store.Settings.SectionHeadingRows.TryGetValue(heading, out var row) ? tokens.Color(row) : null;

    private static string Name(string texture, string rarity) => TextureSizes.Variant(texture, rarity);

    private string Url(string name) => texture.Address(name, TextureSize.Full);

    private static string Chosen(IReadOnlyList<string> states, string? state) =>
        states.FirstOrDefault(known => string.Equals(known, state, StringComparison.OrdinalIgnoreCase))
            is { } found
            ? char.ToUpperInvariant(found[0]) + found[1..]
            : char.ToUpperInvariant(states[0][0]) + states[0][1..];
}
