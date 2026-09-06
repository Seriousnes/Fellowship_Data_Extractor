namespace Fellowship.Storage;

public static class StorageDefaults
{
    public const string Name = "icons";

    public static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public readonly record struct StoredObject(string Name, byte[]? Hash);

public interface IStorage
{
    string Name { get; }

    Uri Address(string name);

    IAsyncEnumerable<StoredObject> ListAsync(string? prefix, CancellationToken cancellationToken);

    Task<string?> ReadTextAsync(string name, CancellationToken cancellationToken);

    Task PrepareAsync(CancellationToken cancellationToken);

    Task WriteAsync(
        string name,
        ReadOnlyMemory<byte> content,
        string contentType,
        string cacheControl,
        byte[] md5,
        CancellationToken cancellationToken);
}
