using System.Text.Json.Nodes;

using CUE4Parse_Conversion.Textures;

using FellowshipExtractor.Output;
using FellowshipExtractor.Textures;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Tests;

public sealed class IconArtTests
{
    private static string Ui(string name) =>
        Path.Combine(Export.Loaded.Folder, ExportLayout.UiFolder, name);

    private static string Image_(string objectPath)
    {
        var dot = objectPath.LastIndexOf('.');
        return (dot >= 0 ? objectPath[(dot + 1)..] : objectPath) + ".png";
    }

    private static IReadOnlyList<RarityEntry> Ladder => Export.Loaded.Settings.Rarities;

    private static string[] Composed =>
    [
        .. Export.Resolver.Collections.Renders
            .Select(Image_)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(render => File.Exists(Ui(ExportLayout.Variant(render, Ladder[^1].Name))))
            .Order(StringComparer.Ordinal),
    ];

    private static Rgba32 Colour(string rarity) =>
        Color.ParseHex(Export.Loaded.Settings.Colors[Export.Loaded.Settings.RarityColorRows[rarity]])
            .ToPixel<Rgba32>();

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
    public void EveryRenderedSkinIsComposedOncePerRarityAndWrittenOpaque()
    {
        var renders = Composed;

        Assert.Equal(450, renders.Length);

        var faults = new List<string>();

        foreach (var render in renders)
        {
            if (File.Exists(Ui(render)))
            {
                faults.Add($"{render} is written unchanged as well as composed");
            }

            foreach (var rung in Ladder)
            {
                var variant = ExportLayout.Variant(render, rung.Name);
                var file = Ui(variant);

                if (!File.Exists(file))
                {
                    faults.Add($"{variant} was not written");
                    continue;
                }

                var reading = Alpha(file);
                if (reading.Visible < 1)
                {
                    faults.Add($"{variant} is {reading.Visible:P0} opaque, mean alpha {reading.MeanAlpha:F1}");
                }
            }
        }

        Assert.Empty(faults);
    }

    [Fact]
    public void TheComposedIconsWrittenAreTheirSourceTexturesRunThroughTheFormula()
    {
        string[] sample =
        [
            .. Export.Resolver.Collections.Renders
                .Where(path => File.Exists(Ui(ExportLayout.Variant(Image_(path), Ladder[^1].Name))))
                .Order(StringComparer.Ordinal)
                .Take(3),
        ];

        Assert.Equal(3, sample.Length);

        var faults = new List<string>();

        foreach (var path in sample)
        {
            var texture = TextureExporter.Load(Export.Provider, path);
            Assert.NotNull(texture);

            var decoded = TextureExporter.Export(texture, ETextureFormat.Png);
            Assert.True(decoded.Written, $"{path} did not decode: {decoded.Error}");

            using var icon = Image.Load<Rgba32>(decoded.Content!);

            foreach (var rung in Ladder)
            {
                var variant = ExportLayout.Variant(Image_(path), rung.Name);

                if (!RarityImages.Compose(icon, Colour(rung.Name)).SequenceEqual(File.ReadAllBytes(Ui(variant))))
                {
                    faults.Add($"{variant} is not {Image_(path)} composed against {rung.Name}");
                }
            }
        }

        Assert.Empty(faults);
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
            .Where(entry => !Path.GetFileNameWithoutExtension(entry.Image)
                .EndsWith($"-{entry.Rarity.ToLowerInvariant()}", StringComparison.Ordinal))
            .Select(entry => $"{entry.Rarity} reads {entry.Image}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(wrong);
    }

    [Fact]
    public void AnItemsIconNamesTheHighestRungOfTheSpreadItCarries()
    {
        var rung = Ladder
            .Select((rarity, at) => (rarity.Name, At: at))
            .ToDictionary(entry => entry.Name, entry => entry.At, StringComparer.OrdinalIgnoreCase);

        var spreads = Export.Loaded.Documents
            .Select(document => (
                Icon: document.Body["icon"]?.GetValue<string>(),
                Spread: document.Body["iconByRarity"] as JsonObject))
            .Where(document => document.Spread is { Count: > 0 })
            .ToArray();

        Assert.NotEmpty(spreads);

        string[] unnamed =
        [
            .. spreads
                .SelectMany(document => document.Spread!.Select(entry => entry.Key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(rarity => !rung.ContainsKey(rarity))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Empty(unnamed);

        string[] wrong =
        [
            .. spreads
                .Select(document => (
                    document.Icon,
                    Highest: document.Spread!.MaxBy(entry => rung[entry.Key])))
                .Where(document => !string.Equals(
                    document.Icon, document.Highest.Value!.GetValue<string>(), StringComparison.Ordinal))
                .Select(document => $"{document.Icon} is named where {document.Highest.Key} reads {document.Highest.Value}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        Assert.Empty(wrong);
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
