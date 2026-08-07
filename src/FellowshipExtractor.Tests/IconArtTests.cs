using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Tests;

public sealed class IconArtTests
{
    private static string? Exported(string objectPath)
    {
        var dot = objectPath.LastIndexOf('.');
        var name = dot >= 0 ? objectPath[(dot + 1)..] : objectPath;
        var file = Path.Combine(Export.Loaded.Folder, name + ".png");

        return File.Exists(file) ? file : null;
    }

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
    public void AnIconARenderedSkinDeclaresCarriesItsOwnBackgroundSoItIsExportedOpaque()
    {
        var exported = Export.Resolver.Collections.Renders.Select(Exported).OfType<string>().ToArray();

        int declaredAndExported = exported.Length;
        Assert.Equal(450, declaredAndExported);

        var faded = exported
            .Select(file => (Name: Path.GetFileNameWithoutExtension(file), Reading: Alpha(file)))
            .Where(icon => icon.Reading.Visible < 1)
            .Select(icon => $"{icon.Name} is {icon.Reading.Visible:P0} opaque, mean alpha {icon.Reading.MeanAlpha:F1}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(faded);
    }

    [Fact]
    public void NoIconAnEntityNamesIsBlank()
    {
        var blank = Export.Loaded.Documents
            .Select(document => document.Body["icon"]?.GetValue<string>())
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => Path.Combine(Export.Loaded.Folder, name))
            .Where(File.Exists)
            .Where(file => Alpha(file).MeanAlpha == 0)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(blank);
    }
}
