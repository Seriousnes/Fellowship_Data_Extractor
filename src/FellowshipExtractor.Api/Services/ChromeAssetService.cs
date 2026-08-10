using Azure.Storage.Blobs;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Fellowship.SDK.Documents;

using FellowshipExtractor.Api.Models;

namespace FellowshipExtractor.Api.Services;

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

    string TraitNode(string? state);

    string BlessingPlate(string? state);

    string? BlessingArt(string? icon, string? state);

    string SlotGlyph(string? slot);

    IReadOnlyList<(string Name, byte[] Content)> Payloads();
}
public sealed class ChromeAssetService(
    BlobContainerClient container,
    IExportPathService paths,
    IEntityStoreService store,
    IDesignTokenService tokens) : IChromeAssetService
{
    public const string Prefix = "ui";

    private const string FrameMask = "T_Borders_Frame_TooltipRarityFrame";
    private const string IconFrameMask = "T_Borders_RarityFrame";
    private const string HeaderMask = "T_Backgrounds_TooltipHeader_Rarity";
    private const string OrnamentFamily = "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_";

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

    private const int DividerCanvasHeight = 56;
    private const int DividerBandFirstRow = 24;
    private const int DividerBandLastRow = 27;
    private const int DividerBandRows = DividerBandLastRow - DividerBandFirstRow + 1;

    private static readonly string[] Badges = [AbilityTexture, LegendaryTexture, SetBonusTexture];

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

    private static readonly string[] RarityTinted = [FrameMask, HeaderMask, IconFrameMask];

    public static readonly IReadOnlyList<string> NodeStates = ["selected", "available", "unavailable"];

    public static readonly IReadOnlyList<string> BlessingStates = ["allocated", "unallocated"];

    private static readonly PngEncoder Encoder = new()
    {
        ColorType = PngColorType.RgbWithAlpha,
        BitDepth = PngBitDepth.Bit8,
        CompressionLevel = PngCompressionLevel.BestCompression,
        FilterMethod = PngFilterMethod.Adaptive,
        SkipMetadata = true,
    };

    private readonly IReadOnlyDictionary<string, string> ornaments = MatchOrnaments(paths, store, tokens);
    private readonly IReadOnlyDictionary<string, string> badgeColours = ReadBadgeColours(paths);

    public string? Frame(string? rarity) => rarity is null ? null : Url(Name(FrameMask, rarity));

    public string Header(string? rarity) =>
        rarity is null ? Url(Name(NeutralHeaderTexture)) : Url(Name(HeaderMask, rarity));

    public string? Ornament(string rarity) =>
        ornaments.TryGetValue(rarity, out var texture) ? Url(Name(texture)) : null;

    public string SlotBacking(string? rarity) =>
        Url(Name(rarity is null ? PlainSlotBackingTexture : SlotBackingTexture));

    public string SlotFrame(string? rarity) =>
        rarity is null ? Url(Name(PlainSlotFrameTexture)) : Url(Name(IconFrameMask, rarity));

    public string? SlotBevel(string? rarity) => rarity is null ? null : Url(Name(SlotBevelTexture));

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
            ? Url($"{Prefix}/{art.Replace(Unallocated, Allocated, StringComparison.Ordinal)}")
            : null;

    public string LockGlyph => Url(Name(LockTexture));

    public string GoldDivider => Url(Name(DividerTexture));

    public string Panel => Url(Name(PanelTexture));

    public string Window => Url(Name(WindowTexture));

    public string RoundedWindow => Url(Name(RoundedWindowTexture));

    public string ThinWindow => Url(Name(ThinWindowTexture));

    public string WindowFill => Url(Name(WindowFillTexture));

    public string SlotGlyph(string? slot) =>
        Url(Name(slot is not null && store.Vocabulary.SlotIcons.TryGetValue(slot, out var gear)
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

    public Badge AbilityBadge => Section(AbilityTexture);

    public Badge LegendaryBadge => Section(LegendaryTexture);

    public Badge SetBonusBadge => Section(SetBonusTexture);

    private Badge Section(string texture) => new(Url(Name(texture)), badgeColours[texture]);

    public IReadOnlyList<(string Name, byte[] Content)> Payloads()
    {
        var payloads = new List<(string, byte[])>();

        foreach (var file in Directory.GetFiles(paths.Ui, "*.png").Order(StringComparer.Ordinal))
        {
            var texture = Path.GetFileNameWithoutExtension(file);

            payloads.Add((
                Name(texture),
                string.Equals(texture, DividerTexture, StringComparison.Ordinal)
                    ? Band(file)
                    : File.ReadAllBytes(file)));
        }

        foreach (var texture in RarityTinted)
        {
            using var mask = Image.Load<Rgba32>(Path.Combine(paths.Ui, texture + ".png"));

            foreach (var rarity in store.Vocabulary.Rarities)
            {
                payloads.Add((Name(texture, rarity.Name), Tint(mask, Colour(tokens, rarity.Name))));
            }
        }

        foreach (var (texture, token) in ModifierTinted)
        {
            using var mask = Image.Load<Rgba32>(Path.Combine(paths.Ui, texture + ".png"));

            payloads.Add((Tinted(texture), Tint(mask, Painted(tokens, token))));
        }

        return payloads;
    }

    private static string Name(string texture) => $"{Prefix}/{texture}.png";

    private static string Name(string texture, string rarity) =>
        $"{Prefix}/{texture}-{rarity.ToLowerInvariant()}.png";

    private string Url(string blob) => $"{container.Uri}/{blob}";

    private static string Chosen(IReadOnlyList<string> states, string? state) =>
        states.FirstOrDefault(known => string.Equals(known, state, StringComparison.OrdinalIgnoreCase))
            is { } found
            ? char.ToUpperInvariant(found[0]) + found[1..]
            : char.ToUpperInvariant(states[0][0]) + states[0][1..];

    private static byte[] Band(string file)
    {
        using var canvas = Image.Load<Rgba32>(file);

        if (canvas.Height != DividerCanvasHeight)
        {
            throw new InvalidOperationException(
                $"{DividerTexture}.png is {canvas.Width}x{canvas.Height}, not {DividerCanvasHeight} rows tall, " +
                $"so rows {DividerBandFirstRow} to {DividerBandLastRow} are no longer the gold band the " +
                "header rule is drawn from.");
        }

        using var band = canvas.Clone(painting =>
            painting.Crop(new Rectangle(0, DividerBandFirstRow, canvas.Width, DividerBandRows)));

        using var png = new MemoryStream();
        band.Save(png, Encoder);
        return png.ToArray();
    }

    private static byte[] Tint(Image<Rgba32> mask, Rgba32 colour)
    {
        using var tinted = mask.Clone();

        tinted.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    pixel.R = (byte)(pixel.R * colour.R / 255);
                    pixel.G = (byte)(pixel.G * colour.G / 255);
                    pixel.B = (byte)(pixel.B * colour.B / 255);
                }
            }
        });

        using var png = new MemoryStream();
        tinted.Save(png, Encoder);
        return png.ToArray();
    }

    private static IReadOnlyDictionary<string, string> MatchOrnaments(
        IExportPathService paths, IEntityStoreService store, IDesignTokenService tokens)
    {
        var rungs = store.Vocabulary.Rarities
            .Select(rarity => (rarity.Name, Colour: Colour(tokens, rarity.Name)))
            .ToArray();

        var matched = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(paths.Ui, OrnamentFamily + "*.png").Order(StringComparer.Ordinal))
        {
            var texture = Path.GetFileNameWithoutExtension(file);

            using var image = Image.Load<Rgba32>(file);
            var peak = Peak(image);

            var nearest = rungs.MinBy(rung => Distance(rung.Colour, peak));

            if (!matched.TryAdd(nearest.Name, texture))
            {
                throw new InvalidOperationException(
                    $"{texture} and {matched[nearest.Name]} both read as the {nearest.Name} ornament. " +
                    $"{texture} peaks at #{peak.R:X2}{peak.G:X2}{peak.B:X2}.");
            }
        }

        return matched;
    }

    private static IReadOnlyDictionary<string, string> ReadBadgeColours(IExportPathService paths)
    {
        var read = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var texture in Badges)
        {
            using var image = Image.Load<Rgba32>(Path.Combine(paths.Ui, texture + ".png"));
            var peak = Peak(image);

            read[texture] = $"#{peak.R:X2}{peak.G:X2}{peak.B:X2}";
        }

        return read;
    }

    private static Rgba32 Peak(Image<Rgba32> image)
    {
        int bestChroma = -1, bestLuma = -1;
        Rgba32 peak = default;

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (pixel.A < 128) continue;

                    var hi = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                    var chroma = hi - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));

                    if (chroma > bestChroma || (chroma == bestChroma && hi > bestLuma))
                    {
                        bestChroma = chroma;
                        bestLuma = hi;
                        peak = pixel;
                    }
                }
            }
        });

        return peak;
    }

    private static int Distance(Rgba32 left, Rgba32 right)
    {
        int r = left.R - right.R, g = left.G - right.G, b = left.B - right.B;
        return (r * r) + (g * g) + (b * b);
    }

    private static Rgba32 Painted(IDesignTokenService tokens, string token) =>
        Color.ParseHex(tokens.Color(token)
            ?? throw new InvalidOperationException(
                $"The palette has no colour {token}, so the modifier chrome it paints cannot be tinted."))
            .ToPixel<Rgba32>();

    private static Rgba32 Colour(IDesignTokenService tokens, string rarity) =>
        Color.ParseHex(tokens.Rarity(rarity)
            ?? throw new InvalidOperationException(
                $"The palette has no colour for rarity {rarity}, so its chrome cannot be tinted."))
            .ToPixel<Rgba32>();
}
