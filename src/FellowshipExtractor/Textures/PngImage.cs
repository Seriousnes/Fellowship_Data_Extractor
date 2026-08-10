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

    internal static byte[] Save(Image<Rgba32> image)
    {
        using var png = new MemoryStream();
        image.Save(png, Encoder);
        return png.ToArray();
    }
}
