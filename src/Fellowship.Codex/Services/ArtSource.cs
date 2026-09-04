using Fellowship.Storage;

namespace Fellowship.Codex.Services;

public interface IArtSource
{
    string Address(string name);
}

public sealed class StorageArtSource(IStorage storage) : IArtSource
{
    public string Address(string name) => storage.Address(name).ToString();
}

public sealed class NamedArtSource : IArtSource
{
    public string Address(string name) => name[(name.LastIndexOf('/') + 1)..];
}
