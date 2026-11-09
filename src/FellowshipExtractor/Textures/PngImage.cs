using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Textures;

internal static class PngImage
{
    private static readonly PngEncoder Encoder = new()
    {
        ColorType = PngColorType.RgbWithAlpha,
        BitDepth = PngBitDepth.Bit8,
        CompressionLevel = PngCompressionLevel.BestCompression,
        FilterMethod = PngFilterMethod.Adaptive,
        SkipMetadata = true,
    };

    private static readonly string Identity =
        $"ImageSharp {PngCache.Version(typeof(PngEncoder))};Rgba32;" +
        $"{Encoder.ColorType}/{Encoder.BitDepth}/{Encoder.CompressionLevel}/{Encoder.FilterMethod}/{Encoder.SkipMetadata}";

    internal static byte[] Save(Image<Rgba32> image)
    {
        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);

        return PngCache.Encoded(Identity, image.Width, image.Height, pixels, () =>
        {
            using var png = new MemoryStream();
            image.Save(png, Encoder);
            return png.ToArray();
        });
    }
}
