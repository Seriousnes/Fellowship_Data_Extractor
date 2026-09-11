using Fellowship.SDK;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FellowshipExtractor.Textures;

public readonly record struct TextureVariant(TextureSize Size, byte[] Webp);

public static class TextureVariants
{
    private static readonly WebpEncoder Encoder = new()
    {
        FileFormat = WebpFileFormatType.Lossless,
        TransparentColorMode = WebpTransparentColorMode.Preserve,
        Method = WebpEncodingMethod.Level6,
        SkipMetadata = true,
    };

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
