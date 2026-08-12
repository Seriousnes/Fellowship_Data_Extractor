using Azure.Storage.Blobs;

namespace Fellowship.Codex.Services;

public interface IArtSource
{
    string Address(string blob);
}

public sealed class BlobArtSource(BlobContainerClient container) : IArtSource
{
    public string Address(string blob) => $"{container.Uri}/{blob}";
}

public sealed class NamedArtSource : IArtSource
{
    public string Address(string blob) => blob[(blob.LastIndexOf('/') + 1)..];
}
