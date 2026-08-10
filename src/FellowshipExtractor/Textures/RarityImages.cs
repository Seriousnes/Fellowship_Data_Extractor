using FellowshipExtractor.Tooltip;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Textures;

public readonly record struct RarityColour(string Rarity, Rgba32 Colour);

public static class RarityImages
{
    private static readonly float[] LinearByLevel =
        [.. Enumerable.Range(0, 256).Select(level => Linear(level / 255f))];

    public static IReadOnlyList<RarityColour> Ladder(Settings settings) =>
    [
        .. settings.Rarities.Select(rung => new RarityColour(rung.Name, Colour(settings, rung.Name))),
    ];

    public static byte[] Tint(Image<Rgba32> texture, Rgba32 colour)
    {
        using var tinted = texture.Clone();

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

        return PngImage.Save(tinted);
    }

    public static byte[] Compose(Image<Rgba32> icon, Rgba32 colour)
    {
        float red = LinearByLevel[colour.R], green = LinearByLevel[colour.G], blue = LinearByLevel[colour.B];

        using var composed = icon.Clone();

        composed.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    ref var pixel = ref row[x];
                    var coverage = pixel.A / 255f;

                    pixel.R = Encoded(float.Lerp(LinearByLevel[pixel.R], red, coverage));
                    pixel.G = Encoded(float.Lerp(LinearByLevel[pixel.G], green, coverage));
                    pixel.B = Encoded(float.Lerp(LinearByLevel[pixel.B], blue, coverage));
                    pixel.A = byte.MaxValue;
                }
            }
        });

        return PngImage.Save(composed);
    }

    private static Rgba32 Colour(Settings settings, string rarity)
    {
        if (!settings.RarityColorRows.TryGetValue(rarity, out var row))
        {
            throw new InvalidOperationException(
                $"The settings name no colour row for {rarity}, so nothing says what colour its chrome and its " +
                "icons are drawn in.");
        }

        if (!settings.Colors.TryGetValue(row, out var hex))
        {
            throw new InvalidOperationException(
                $"{rarity} draws in {row}, which the named colour table does not hold, so its chrome and its icons " +
                "would be composed against no colour at all.");
        }

        return Color.ParseHex(hex).ToPixel<Rgba32>();
    }

    private static float Linear(float channel) =>
        channel <= 0.04045f ? channel / 12.92f : MathF.Pow((channel + 0.055f) / 1.055f, 2.4f);

    private static byte Encoded(float linear)
    {
        var channel = linear <= 0.0031308f
            ? linear * 12.92f
            : (1.055f * MathF.Pow(linear, 1f / 2.4f)) - 0.055f;

        return (byte)Math.Clamp(MathF.Round(channel * 255f), 0f, 255f);
    }
}
