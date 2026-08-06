using Azure.Storage.Blobs;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Api.Models;

public sealed class ChromeAssets(
    BlobContainerClient container,
    ExportPaths paths,
    EntityStore store,
    DesignTokens tokens)
{
    public const string Prefix = "ui";

    private const string FrameMask = "T_Borders_Frame_TooltipRarityFrame";
    private const string IconFrameMask = "T_Borders_RarityFrame";
    private const string HeaderMask = "T_Backgrounds_TooltipHeader_Rarity";
    private const string OrnamentFamily = "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_";

    private const string PanelTexture = "T_Borders_WindowBackground_TooltipSimple";
    private const string WindowTexture = "T_Borders_BGPatterns_SmallIndentAlt";
    private const string NeutralHeaderTexture = "T_Backgrounds_TooltipHeader_Subheader";
    private const string SlotTexture = "T_Icons_Generic_TooltipSlot";
    private const string ItemPowerTexture = "T_Icons_Progression_ItemLevel";
    private const string RuleTexture = "T_Borders_Divider_Simple";
    private const string AbilityTexture = "T_Icons_StatTooltip_AbilitySlot_Pink";
    private const string LegendaryTexture = "T_Icons_StatTooltip_Legendary";
    private const string SetBonusTexture = "T_Icons_StatTooltip_Set_Bonus";
    private const string SlotPlateTexture = "T_Borders_EmptySlot";
    private const string TraitTexture = "T_Icons_StatTooltip_TraitSlot";
    private const string ExpandTexture = "T_Icons_Generic_TooltipExpand";

    private static readonly string[] Badges = [AbilityTexture, LegendaryTexture, SetBonusTexture];

    private static readonly string[] RarityTinted = [FrameMask, HeaderMask, IconFrameMask];

    private static readonly string[] NeutralTinted = [FrameMask, IconFrameMask];

    private const string NeutralRung = "neutral";

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
    private readonly Rgba32 border = BorderColour(paths);

    public string Frame(string? rarity) => Url(Name(FrameMask, rarity ?? NeutralRung));

    public string Header(string? rarity) =>
        Url(rarity is null ? Name(NeutralHeaderTexture, NeutralRung) : Name(HeaderMask, rarity));

    public string PanelHeader => Url(Name(NeutralHeaderTexture));

    public string? Ornament(string rarity) =>
        ornaments.TryGetValue(rarity, out var texture) ? Url(Name(texture)) : null;

    public string IconFrame(string? rarity) => Url(Name(IconFrameMask, rarity ?? NeutralRung));

    public string SlotPlate => Url(Name(SlotPlateTexture));

    public string Panel => Url(Name(PanelTexture));

    public string Window => Url(Name(WindowTexture));

    public string SlotGlyph(string? slot) =>
        Url(Name(slot is not null && store.Vocabulary.SlotIcons.TryGetValue(slot, out var gear)
            ? gear
            : SlotTexture));

    public string ItemPowerGlyph => Url(Name(ItemPowerTexture));

    public string Rule => Url(Name(RuleTexture));

    public string TraitGlyph => Url(Name(TraitTexture));

    public string ExpandGlyph => Url(Name(ExpandTexture));

    public Badge AbilityBadge => Section(AbilityTexture);

    public Badge LegendaryBadge => Section(LegendaryTexture);

    public Badge SetBonusBadge => Section(SetBonusTexture);

    private Badge Section(string texture) => new(Url(Name(texture)), badgeColours[texture]);

    public IReadOnlyList<(string Name, byte[] Content)> Payloads()
    {
        var payloads = new List<(string, byte[])>();

        foreach (var file in Directory.GetFiles(paths.Ui, "*.png").Order(StringComparer.Ordinal))
        {
            payloads.Add((Name(Path.GetFileNameWithoutExtension(file)), File.ReadAllBytes(file)));
        }

        foreach (var texture in RarityTinted)
        {
            using var mask = Image.Load<Rgba32>(Path.Combine(paths.Ui, texture + ".png"));

            foreach (var rarity in store.Vocabulary.Rarities)
            {
                payloads.Add((Name(texture, rarity.Name), Tint(mask, Colour(tokens, rarity.Name))));
            }
        }

        foreach (var texture in NeutralTinted)
        {
            using var mask = Image.Load<Rgba32>(Path.Combine(paths.Ui, texture + ".png"));

            payloads.Add((Name(texture, NeutralRung), Tint(mask, border)));
        }

        using var plate = Image.Load<Rgba32>(Path.Combine(paths.Ui, NeutralHeaderTexture + ".png"));

        payloads.Add((Name(NeutralHeaderTexture, NeutralRung), Paint(plate, border)));

        return payloads;
    }

    private static string Name(string texture) => $"{Prefix}/{texture}.png";

    private static string Name(string texture, string rarity) =>
        $"{Prefix}/{texture}-{rarity.ToLowerInvariant()}.png";

    private string Url(string blob) => $"{container.Uri}/{blob}";

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

    private static byte[] Paint(Image<Rgba32> source, Rgba32 colour)
    {
        using var painted = source.Clone();

        painted.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    pixel.R = colour.R;
                    pixel.G = colour.G;
                    pixel.B = colour.B;
                }
            }
        });

        using var png = new MemoryStream();
        painted.Save(png, Encoder);
        return png.ToArray();
    }

    private static Rgba32 BorderColour(ExportPaths paths)
    {
        var file = Path.Combine(paths.Ui, WindowTexture + ".png");

        using var window = Image.Load<Rgba32>(file);

        var drawn = new Dictionary<Rgba32, int>();

        window.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    if (pixel.A < 128) continue;

                    pixel.A = 255;
                    drawn[pixel] = 1 + drawn.GetValueOrDefault(pixel);
                }
            }
        });

        if (drawn.Count == 0)
        {
            throw new InvalidOperationException(
                $"{file} draws no opaque pixel, so the colour of the tooltip's border cannot be read.");
        }

        return drawn.MaxBy(shade => shade.Value).Key;
    }

    private static IReadOnlyDictionary<string, string> MatchOrnaments(
        ExportPaths paths, EntityStore store, DesignTokens tokens)
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

    private static IReadOnlyDictionary<string, string> ReadBadgeColours(ExportPaths paths)
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

    private static Rgba32 Colour(DesignTokens tokens, string rarity) =>
        Color.ParseHex(tokens.Rarity(rarity)
            ?? throw new InvalidOperationException(
                $"The palette has no colour for rarity {rarity}, so its chrome cannot be tinted."))
            .ToPixel<Rgba32>();
}

public sealed record Badge(string Url, string Colour);
