using Microsoft.Extensions.FileProviders;

using Fellowship.SDK;
using Fellowship.Storage;

namespace Fellowship.Codex.Services;

public static class Textures
{
    public static StaticFileOptions From(string root) => new()
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(root, ChromeAssetService.Prefix)),
        RequestPath = $"/{ChromeAssetService.Prefix}",
    };
}

public interface ITextureSource
{
    string Address(string name, TextureSize size);
}

public sealed class StorageTextureSource(IStorage storage) : ITextureSource
{
    public string Address(string name, TextureSize size) => storage.Address(TextureSizes.Key(name, size)).ToString();
}

public sealed class FolderTextureSource : ITextureSource
{
    public string Address(string name, TextureSize size) => $"/{TextureSizes.Key(name, size)}";
}

public sealed class NamedTextureSource : ITextureSource
{
    public string Address(string name, TextureSize size) => name;
}
