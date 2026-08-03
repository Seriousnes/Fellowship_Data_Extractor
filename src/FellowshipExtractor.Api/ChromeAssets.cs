using Azure.Storage.Blobs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Api;

/// <summary>
/// The tooltip's chrome as blobs a browser can fetch, and the one place that knows what they are
/// called.
///
/// Three of them are not finished art. The tooltip frame, the header band and the outline round an
/// item's own icon ship as untinted masks and the game draws them in the colour of the rung an item
/// rolled. A browser cannot tint a background image, and CSS mask-border is not portable enough to
/// lean on, so the tint is resolved once at seed time instead: every rung gets its own copy of each
/// mask. Seven rungs against three masks is twenty-one extra blobs, which costs less than making the
/// renderer composite anything.
///
/// Every URL a payload writes is built here rather than interpolated at the call site, so the
/// folder, the file extension and the rung suffix are written down once.
/// </summary>
public sealed class ChromeAssets(
    BlobContainerClient container,
    ExportPaths paths,
    EntityStore store,
    DesignTokens tokens)
{
    /// <summary>
    /// The folder the chrome sits in. The icon corpus is 1,354 blobs at the container root and a
    /// frame is not an icon, so the chrome gets its own prefix and neither listing has to filter
    /// the other out.
    /// </summary>
    public const string Prefix = "ui";

    private const string FrameMask = "T_Borders_Frame_TooltipRarityFrame";
    private const string IconFrameMask = "T_Borders_RarityFrame";
    private const string HeaderMask = "T_Backgrounds_TooltipHeader_Rarity";
    private const string OrnamentFamily = "T_Backgrounds_TooltipItemHeader_TopDecor_Clean_";

    private const string PanelTexture = "T_Borders_WindowBackground_TooltipSimple";
    private const string NeutralHeaderTexture = "T_Backgrounds_TooltipHeader_Subheader";
    private const string SlotTexture = "T_Icons_Generic_TooltipSlot";
    private const string ItemPowerTexture = "T_Icons_Progression_ItemLevel";
    private const string RuleTexture = "T_Borders_Divider_Simple";
    private const string AbilityTexture = "T_Icons_StatTooltip_AbilitySlot_Pink";
    private const string LegendaryTexture = "T_Icons_StatTooltip_Legendary";
    private const string SetBonusTexture = "T_Icons_StatTooltip_Set_Bonus";
    private const string SlotPlateTexture = "T_Borders_EmptySlot";

    /// <summary>
    /// The badges that head a section, and the only textures whose own colour is asked for. Each is
    /// finished art rather than a mask, and the colour the game writes its section heading in is the
    /// colour of the badge beside it, so the heading takes the badge's peak pixel rather than a
    /// palette row chosen to look similar. Read once at startup, by the same measurement that pairs
    /// an ornament with its rung.
    /// </summary>
    private static readonly string[] Badges = [AbilityTexture, LegendaryTexture, SetBonusTexture];

    /// <summary>
    /// The masks a rung's colour is multiplied into. Curated, for the same reason the extractor's
    /// chrome list is curated: nothing in the build enumerates which brushes take the rarity tint,
    /// the widget blueprints bind every brush individually. These three are the ones the game draws
    /// in the rarity colour, and each measures a channel spread of zero across every visible pixel,
    /// which is what a tintable mask looks like when it is read back off the export. The icon frame
    /// is a ramp rather than flat white, running luma 14 to 252, which the multiply preserves.
    /// </summary>
    private static readonly string[] RarityTinted = [FrameMask, HeaderMask, IconFrameMask];

    /// <summary>
    /// Pinned rather than left to the defaults so the bytes depend only on the mask and the colour.
    /// The seeder skips a blob whose content hash already matches, and that only holds while the
    /// same input encodes to the same file on every start.
    /// </summary>
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

    /// <summary>
    /// The rarity frame in one rung's colour, by the rung's internal name, or null for an entity
    /// that resolves no rung at all. An ability has no rarity to be framed in and neither does an
    /// item whose caller named none, and the shipped mask is pure white, so drawing it untinted
    /// would put a white rule round a tooltip the game frames in nothing.
    /// </summary>
    public string? Frame(string? rarity) => rarity is null ? null : Url(Name(FrameMask, rarity));

    /// <summary>
    /// The header band: the rarity gradient in the rung's colour, or the neutral band the build
    /// ships beside it for the tooltips that carry no rung.
    /// </summary>
    public string Header(string? rarity) =>
        rarity is null ? Url(Name(NeutralHeaderTexture)) : Url(Name(HeaderMask, rarity));

    /// <summary>
    /// The header ornament a rung wears, or null for the three low rungs the build ships none for.
    /// </summary>
    public string? Ornament(string rarity) =>
        ornaments.TryGetValue(rarity, out var texture) ? Url(Name(texture)) : null;

    /// <summary>
    /// The outline round the item's own art, in the rung's colour, or null for an entity with no
    /// rung to be framed in. The client only ever draws this mask tinted, so an untinted copy is not
    /// a fallback: a grey outline is not what a rung with no colour looks like, an absent one is.
    /// </summary>
    public string? IconFrame(string? rarity) => rarity is null ? null : Url(Name(IconFrameMask, rarity));

    /// <summary>The dark plate an item's art sits on in a bag square.</summary>
    public string SlotPlate => Url(Name(SlotPlateTexture));

    /// <summary>The dark panel every tooltip is drawn on, gold edge and all.</summary>
    public string Panel => Url(Name(PanelTexture));

    /// <summary>The diamond the game puts before the slot an item is worn in.</summary>
    public string SlotGlyph => Url(Name(SlotTexture));

    /// <summary>The gem the game puts before an item's power number.</summary>
    public string ItemPowerGlyph => Url(Name(ItemPowerTexture));

    /// <summary>The horizontal rule between sections.</summary>
    public string Rule => Url(Name(RuleTexture));

    public Badge AbilityBadge => Section(AbilityTexture);

    public Badge LegendaryBadge => Section(LegendaryTexture);

    public Badge SetBonusBadge => Section(SetBonusTexture);

    private Badge Section(string texture) => new(Url(Name(texture)), badgeColours[texture]);

    /// <summary>
    /// Everything the chrome contributes to the container: each exported texture as shipped, then
    /// one tinted copy of each rarity mask per rung. The tinted copies are built here rather than
    /// on disk because they are a function of the export and the palette, and both of those arrive
    /// with the export.
    /// </summary>
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

        return payloads;
    }

    /// <summary>A chrome texture's blob name, as shipped.</summary>
    private static string Name(string texture) => $"{Prefix}/{texture}.png";

    /// <summary>
    /// A tinted mask's blob name. The suffix is the rung's internal name, the same key
    /// <see cref="DesignTokens.Rarity"/> takes, never the word the rung prints: ordinal 4 is filed
    /// as Champion and prints "Heroic", ordinal 5 is filed as Heroic and prints "Regal", so a name
    /// built from the printed word would hand a gold item the red frame.
    /// </summary>
    private static string Name(string texture, string rarity) =>
        $"{Prefix}/{texture}-{rarity.ToLowerInvariant()}.png";

    /// <summary>
    /// Blob-level anonymous read is set on the container, so a blob name is a URL on its own.
    /// </summary>
    private string Url(string blob) => $"{container.Uri}/{blob}";

    /// <summary>
    /// One mask in one rung's colour: every channel multiplied, alpha untouched. Multiply rather
    /// than replace, which is the same arithmetic for these two because both are pure white, but is
    /// not the same for the ramps beside them in the export. T_Backgrounds_TooltipHeader_Rarity_Alt
    /// runs luma 75 to 255 and T_Icons_Arrows_Right holds its outline at luma 28, so replacing RGB
    /// would flatten the shading out of either one. Written as a multiply so a ramp added to the
    /// list above comes out right without this being revisited.
    /// </summary>
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

    /// <summary>
    /// Which ornament belongs to which rung, re-derived from the exported pixels rather than read
    /// off the file names. The four names are a legacy ladder offset by one rung from the internal
    /// names the palette is keyed by, so the file called _Champion is the Epic ornament and the
    /// names cannot be trusted. The art can: each ornament's most saturated pixel reproduces one
    /// rung's palette entry. Nearest by squared distance rather than equality, because two of the
    /// four are off by a single count in one channel, #BF2525 against the palette's #BE2525 and
    /// #A435EF against #A335EE. Two ornaments landing on one rung is a broken match and throws,
    /// rather than painting the header with whichever file was read second.
    /// </summary>
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

    /// <summary>
    /// The colour each section badge is written in, measured off the badge itself.
    /// </summary>
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

    /// <summary>
    /// A texture's identifying colour: the most saturated pixel that is more opaque than not, tied
    /// on brightness. Not the most opaque pixel, which for all four ornaments is the same near black
    /// outline and says nothing about which rung the file belongs to.
    /// </summary>
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

    /// <summary>
    /// A rung's colour, by the internal name the palette keys it under. A rung with no palette row
    /// is a broken export rather than a colour to fall back from, because every tint and every
    /// ornament match reads through here.
    /// </summary>
    private static Rgba32 Colour(DesignTokens tokens, string rarity) =>
        Color.ParseHex(tokens.Rarity(rarity)
            ?? throw new InvalidOperationException(
                $"The palette has no colour for rarity {rarity}, so its chrome cannot be tinted."))
            .ToPixel<Rgba32>();
}

/// <summary>
/// A section badge: the glyph the heading is drawn with and the colour it is drawn in, which is the
/// glyph's own.
/// </summary>
public sealed record Badge(string Url, string Colour);
