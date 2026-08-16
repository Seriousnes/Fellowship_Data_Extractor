using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Textures;

public readonly record struct TextureBytes(
    byte[]? Content, string? Extension, int Width, int Height, string? PixelFormat, string? Error)
{
    public bool Written => Content is not null;
}

public static class TextureExporter
{
    public static TextureBytes Export(UTexture texture, ETextureFormat format, bool discardAlpha = false)
    {
        var pixelFormat = texture.Format.ToString();

        try
        {
            var decoded = texture.Decode(ETexturePlatform.DesktopMobile);
            if (decoded is null)
            {
                return new TextureBytes(null, null, 0, 0, pixelFormat, "decoder returned nothing");
            }

            var bytes = decoded.Encode(format, saveHdrAsHdr: false, out var extension);
            if (bytes is null or { Length: 0 })
            {
                return new TextureBytes(
                    null, null, decoded.Width, decoded.Height, pixelFormat, "encoder produced no bytes");
            }

            if (discardAlpha) bytes = Opaque(bytes);

            return new TextureBytes(bytes, extension, decoded.Width, decoded.Height, pixelFormat, null);
        }
        catch (Exception ex)
        {
            return new TextureBytes(null, null, 0, 0, pixelFormat, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static byte[] Opaque(byte[] encoded)
    {
        using var image = Image.Load<Rgba32>(encoded);

        image.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++) row[x].A = byte.MaxValue;
            }
        });

        using var stream = new MemoryStream(encoded.Length);
        image.Save(stream, image.Metadata.DecodedImageFormat!);

        return stream.ToArray();
    }

    public static UTexture2D? Load(AbstractFileProvider provider, string objectPath)
    {
        var dot = objectPath.LastIndexOf('.');
        var package = dot >= 0 ? objectPath[..dot] : objectPath;
        return provider.TryLoadPackageObject<UTexture2D>(package, out var texture) ? texture : null;
    }
}
