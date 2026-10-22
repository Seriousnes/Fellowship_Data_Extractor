using System.Security.Cryptography;
using System.Text;

using Fellowship.SDK;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FellowshipExtractor.Textures;

public readonly record struct TextureVariant(TextureSize Size, byte[] Webp);

public static class TextureVariants
{
    private const string CompleteMarker = "complete";

    private static readonly WebpEncoder Encoder = new()
    {
        FileFormat = WebpFileFormatType.Lossless,
        TransparentColorMode = WebpTransparentColorMode.Preserve,
        Method = WebpEncodingMethod.Level6,
        SkipMetadata = true,
    };

    private static readonly string Recipe = string.Join(
        ";",
        [
            $"{Encoder.FileFormat}/{Encoder.TransparentColorMode}/{Encoder.Method}/{Encoder.SkipMetadata}",
            "Crop/Top/Lanczos3",
            .. TextureSizes.All.Select(size => $"{size}={TextureSizes.Pixels(size)}"),
        ]);

    public static string CacheFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FellowshipExtractor",
        "texture-variants",
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Recipe)))[..16]);

    public static IReadOnlyList<TextureVariant> Cached(byte[] png)
    {
        var folder = Path.Combine(CacheFolder, Convert.ToHexStringLower(SHA256.HashData(png)));

        if (File.Exists(Path.Combine(folder, CompleteMarker)))
        {
            return
            [
                .. TextureSizes.All
                    .Select(size => (Size: size, File: Path.Combine(folder, TextureSizes.Segment(size))))
                    .Where(entry => File.Exists(entry.File))
                    .Select(entry => new TextureVariant(entry.Size, File.ReadAllBytes(entry.File))),
            ];
        }

        var variants = Render(png);

        var staging = $"{folder}.{Guid.NewGuid():N}";
        Directory.CreateDirectory(staging);

        foreach (var variant in variants)
        {
            File.WriteAllBytes(Path.Combine(staging, TextureSizes.Segment(variant.Size)), variant.Webp);
        }

        File.WriteAllBytes(Path.Combine(staging, CompleteMarker), []);

        try
        {
            Directory.Move(staging, folder);
        }
        catch (IOException) when (Directory.Exists(folder))
        {
            Directory.Delete(staging, recursive: true);
        }

        return variants;
    }

    public static IReadOnlyList<TextureVariant> Render(byte[] png)
    {
        using var image = Image.Load<Rgba32>(png);

        List<TextureVariant> variants = [];

        foreach (var size in TextureSizes.All)
        {
            if (!TextureSizes.Fits(image.Width, image.Height, size)) continue;

            variants.Add(new TextureVariant(
                size, TextureSizes.Pixels(size) is { } pixels ? Square(image, pixels) : Encoded(image)));
        }

        return variants;
    }

    private static byte[] Square(Image<Rgba32> image, int pixels)
    {
        using var square = image.Width == pixels && image.Height == pixels
            ? image.Clone()
            : image.Clone(source => source.Resize(new ResizeOptions
            {
                Size = new Size(pixels, pixels),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Top,
                Sampler = KnownResamplers.Lanczos3,
            }));

        return Encoded(square);
    }

    private static byte[] Encoded(Image<Rgba32> image)
    {
        using var webp = new MemoryStream();
        image.Save(webp, Encoder);

        return webp.ToArray();
    }
}
