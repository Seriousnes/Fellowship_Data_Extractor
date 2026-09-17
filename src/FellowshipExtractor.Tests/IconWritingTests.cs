using Fellowship.SDK;

using System.Text.Json.Nodes;

using FellowshipExtractor.Output;
using FellowshipExtractor.Textures;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Tests;

public sealed class IconWritingTests
{
    private static string Ui(string name) =>
        Path.Combine(Export.Loaded.Ui, TextureSizes.File(name, TextureSize.Full));

    private static IReadOnlyList<RarityEntry> Ladder => Export.Loaded.Settings.Rarities;

    private static (double MeanAlpha, double Visible) Alpha(string file)
    {
        using var image = Image.Load<Rgba32>(file);

        long sum = 0, visible = 0, total = 0;

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                foreach (var pixel in rows.GetRowSpan(y))
                {
                    sum += pixel.A;
                    if (pixel.A == byte.MaxValue) visible++;
                    total++;
                }
            }
        });

        return (sum / (double)total, visible / (double)total);
    }

    [Fact]
    public void TheComposeLerpsTowardTheRarityColourInLinearSpaceNotInByteSpace()
    {
        using var icon = new Image<Rgba32>(3, 1);

        icon[0, 0] = new Rgba32(128, 128, 128, 0);
        icon[1, 0] = new Rgba32(128, 128, 128, 128);
        icon[2, 0] = new Rgba32(128, 128, 128, 255);

        using var composed = Image.Load<Rgba32>(RarityImages.Compose(icon, new Rgba32(255, 128, 0, 255)));

        Assert.Equal(new Rgba32(128, 128, 128, 255), composed[0, 0]);
        Assert.Equal(new Rgba32(205, 128, 92, 255), composed[1, 0]);
        Assert.Equal(new Rgba32(255, 128, 0, 255), composed[2, 0]);
    }

    [Fact]
    public void EveryRarityAnItemSpreadsItsArtOverNamesThatRaritysVariant()
    {
        var wrong = Export.Loaded.Documents
            .Select(document => document.Body["iconByRarity"] as JsonObject)
            .OfType<JsonObject>()
            .SelectMany(spread => spread.Select(entry => (Rarity: entry.Key, Image: entry.Value!.GetValue<string>())))
            .Distinct()
            .Where(entry => !entry.Image
                .EndsWith($"-{entry.Rarity.ToLowerInvariant()}", StringComparison.Ordinal))
            .Select(entry => $"{entry.Rarity} reads {entry.Image}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(wrong);
    }

    [Fact]
    public void AnItemsIconNamesTheHighestRungItDropsAt()
    {
        var rung = Ladder
            .Select((rarity, at) => (rarity.Name, At: at))
            .ToDictionary(entry => entry.Name, entry => entry.At, StringComparer.OrdinalIgnoreCase);

        var spreads = Spreads();

        Assert.NotEmpty(spreads);

        string[] unnamed =
        [
            .. spreads
                .SelectMany(document => document.Spread.Select(entry => entry.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(rarity => !rung.ContainsKey(rarity))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Empty(unnamed);

        string[] wrong =
        [
            .. spreads
                .Select(document => (document.Icon, Highest: Top(document.Spread, document.DropsAt, rung)))
                .Where(document => !string.Equals(
                    document.Icon, document.Highest.Image, StringComparison.Ordinal))
                .Select(document =>
                    $"{document.Icon} is named where {document.Highest.Rarity} reads {document.Highest.Image}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Empty(wrong);
    }

    [Fact]
    public void EverySpreadCoversEveryRungOfTheLadder()
    {
        string[] ladder = [.. Ladder.Select(rarity => rarity.Name).Order(StringComparer.Ordinal)];

        var spreads = Spreads();

        Assert.NotEmpty(spreads);

        string[] partial =
        [
            .. spreads
                .Where(document => !document.Spread
                    .Select(entry => entry.Key)
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(ladder, StringComparer.OrdinalIgnoreCase))
                .Select(document =>
                    $"{document.Icon} spreads over {string.Join(',', document.Spread.Select(entry => entry.Key))}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Empty(partial);
    }

    [Fact]
    public void EveryDocumentThatNamesAComposedIconCarriesTheSpreadTheRungsRead()
    {
        string[] alone =
        [
            .. Export.Loaded.Documents
                .Where(document => document.Body["iconByRarity"] is not JsonObject)
                .Select(document => document.Body["icon"]?.GetValue<string>())
                .OfType<string>()
                .Where(icon => Rung(icon) is not null)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Empty(alone);
    }

    private static string? Rung(string icon)
    {
        foreach (var rarity in Ladder)
        {
            if (icon.EndsWith($"-{rarity.Name.ToLowerInvariant()}", StringComparison.Ordinal)) return rarity.Name;
        }

        return null;
    }

    private static (string? Icon, JsonObject Spread, string[]? DropsAt)[] Spreads() =>
    [
        .. Export.Loaded.Documents
            .Select(document => (
                Icon: document.Body["icon"]?.GetValue<string>(),
                Spread: document.Body["iconByRarity"] as JsonObject,
                DropsAt: (document.Body["rarities"] as JsonArray)?
                    .Select(rarity => rarity!.GetValue<string>())
                    .ToArray()))
            .Where(document => document.Spread is { Count: > 0 })
            .Select(document => (Icon: document.Icon, Spread: document.Spread!, DropsAt: document.DropsAt)),
    ];

    private static (string Rarity, string Image) Top(
        JsonObject spread, string[]? dropsAt, IReadOnlyDictionary<string, int> rung)
    {
        KeyValuePair<string, JsonNode?>[] reachable =
        [
            .. spread.Where(entry => dropsAt is not { Length: > 0 } ||
                dropsAt.Contains(entry.Key, StringComparer.OrdinalIgnoreCase)),
        ];

        Assert.NotEmpty(reachable);

        var highest = reachable.MaxBy(entry => rung[entry.Key]);

        return (highest.Key, highest.Value!.GetValue<string>());
    }

    [Fact]
    public void EveryIconADocumentNamesIsWritten()
    {
        var named = Export.Loaded.Documents
            .SelectMany(document => Named(document.Body))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] missing = [.. named.Where(name => !File.Exists(Ui(name)))];

        Assert.NotEmpty(named);
        Assert.Empty(missing);
    }

    [Fact]
    public void NoIconAnEntityNamesIsBlank()
    {
        var blank = Export.Loaded.Documents
            .SelectMany(document => Named(document.Body))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(Ui)
            .Where(File.Exists)
            .Where(file => Alpha(file).MeanAlpha == 0)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(blank);
    }

    private static IEnumerable<string> Named(JsonObject body)
    {
        if (body["icon"]?.GetValue<string>() is { } icon) yield return icon;

        if (body["iconByRarity"] is not JsonObject spread) yield break;

        foreach (var (_, variant) in spread)
        {
            if (variant?.GetValue<string>() is { } named) yield return named;
        }
    }
}
