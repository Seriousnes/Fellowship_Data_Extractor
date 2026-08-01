using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse_Conversion.Textures;

namespace FellowshipExtractor.Textures;

/// <summary>An exported image, or the reason the texture could not be written.</summary>
public readonly record struct TextureExport(string? File, int Width, int Height, string? PixelFormat, string? Error)
{
    public bool Written => File is not null;
}

/// <summary>
/// Decodes cooked textures to real image files. Cooked textures are block-compressed GPU data,
/// so a path to one is not an image until it has been through here.
/// </summary>
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

    /// <summary>
    /// Writes one texture beside the given path, choosing the extension from the format. The
    /// caller supplies the path without an extension because the encoder decides it.
    /// </summary>
    public static TextureExport Export(UTexture texture, string targetWithoutExtension, ETextureFormat format)
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

    /// <summary>
    /// Loads the texture an asset path points at. Object paths carry the object name after a dot,
    /// which the package loader does not want.
    /// </summary>
    public static UTexture2D? Load(AbstractFileProvider provider, string objectPath)
    {
        var dot = objectPath.LastIndexOf('.');
        var package = dot >= 0 ? objectPath[..dot] : objectPath;
        return provider.TryLoadPackageObject<UTexture2D>(package, out var texture) ? texture : null;
    }
}
