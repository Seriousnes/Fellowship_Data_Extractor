using Microsoft.Extensions.FileProviders;

using Fellowship.Storage;

namespace Fellowship.Codex.Services;

public static class Art
{
    public static StaticFileOptions From(string root) => new()
    {
        FileProvider = new PhysicalFileProvider(Path.Combine(root, ChromeAssetService.Prefix)),
        RequestPath = $"/{ChromeAssetService.Prefix}",
    };
}

public interface IArtSource
{
    string Address(string name);
}

public sealed class StorageArtSource(IStorage storage) : IArtSource
{
    public string Address(string name) => storage.Address(name).ToString();
}

public sealed class FolderArtSource : IArtSource
{
    public string Address(string name) => $"/{name}";
}

public sealed class NamedArtSource : IArtSource
{
    public string Address(string name) => name[(name.LastIndexOf('/') + 1)..];
}
