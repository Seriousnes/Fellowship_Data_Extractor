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

public interface ITextureReader
{
    Task<byte[]?> Read(string address, CancellationToken cancellationToken);
}

public sealed class FolderTextureReader(string root) : ITextureReader
{
    private readonly string full = Path.GetFullPath(root) + Path.DirectorySeparatorChar;

    public async Task<byte[]?> Read(string address, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(Path.Combine(full, address.TrimStart('/')));

        return path.StartsWith(full, StringComparison.OrdinalIgnoreCase) && File.Exists(path)
            ? await File.ReadAllBytesAsync(path, cancellationToken)
            : null;
    }
}

public sealed class StorageTextureReader(IHttpClientFactory clients) : ITextureReader
{
    public async Task<byte[]?> Read(string address, CancellationToken cancellationToken)
    {
        using var response = await clients.CreateClient().GetAsync(address, cancellationToken);

        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(cancellationToken) : null;
    }
}
