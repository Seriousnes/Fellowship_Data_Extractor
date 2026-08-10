using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FellowshipExtractor.Textures;

public static class GoldBand
{
    public const string Texture = "T_GoldDivider";

    public const int CanvasHeight = 56;

    public const int FirstRow = 24;

    public const int LastRow = 27;

    public const int Height = LastRow - FirstRow + 1;

    public static byte[] Crop(Image<Rgba32> canvas)
    {
        if (canvas.Height != CanvasHeight)
        {
            throw new InvalidOperationException(
                $"{Texture} is {canvas.Width}x{canvas.Height}, not {CanvasHeight} rows tall, so rows {FirstRow} to " +
                $"{LastRow} are no longer the gold band the header rule draws.");
        }

        using var band = canvas.Clone(painting =>
            painting.Crop(new Rectangle(0, FirstRow, canvas.Width, Height)));

        return PngImage.Save(band);
    }
}
