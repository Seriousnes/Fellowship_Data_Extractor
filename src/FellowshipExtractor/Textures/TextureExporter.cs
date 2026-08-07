using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FellowshipExtractor.Textures;

public readonly record struct TextureExport(string? File, int Width, int Height, string? PixelFormat, string? Error)
{
    public bool Written => File is not null;
}

public static class TextureExporter
{
    public static ETextureFormat ParseFormat(string? value) => value?.ToLowerInvariant() switch
    {
        null or "png" => ETextureFormat.Png,
        "jpg" or "jpeg" => ETextureFormat.Jpeg,
        "tga" => ETextureFormat.Tga,
        "dds" => ETextureFormat.Dds,
        _ => throw new ArgumentException($"Unknown image format '{value}'. Use png, jpeg, tga or dds."),
    };

    public static TextureExport Export(
        UTexture texture, string targetWithoutExtension, ETextureFormat format, bool discardAlpha = false)
    {
        var pixelFormat = texture.Format.ToString();

        try
        {
            var decoded = texture.Decode(ETexturePlatform.DesktopMobile);
            if (decoded is null)
            {
                return new TextureExport(null, 0, 0, pixelFormat, "decoder returned nothing");
            }

            var bytes = decoded.Encode(format, saveHdrAsHdr: false, out var extension);
            if (bytes is null or { Length: 0 })
            {
                return new TextureExport(null, decoded.Width, decoded.Height, pixelFormat, "encoder produced no bytes");
            }

            if (discardAlpha) bytes = Opaque(bytes);

            var target = $"{targetWithoutExtension}.{extension}";
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, bytes);

            return new TextureExport(target, decoded.Width, decoded.Height, pixelFormat, null);
        }
        catch (Exception ex)
        {
            return new TextureExport(null, 0, 0, pixelFormat, $"{ex.GetType().Name}: {ex.Message}");
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
