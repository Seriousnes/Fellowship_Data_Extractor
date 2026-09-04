using System.Runtime.CompilerServices;

using Fellowship.Storage;

namespace FellowshipExtractor.Tests;

internal sealed class MemoryStorage(IReadOnlyDictionary<string, string>? objects = null) : IStorage
{
    private readonly IReadOnlyDictionary<string, string> objects =
        objects ?? new Dictionary<string, string>(StringComparer.Ordinal);

    public string Name => StorageDefaults.Name;

    public Uri Address(string name) => new($"http://localhost/{StorageDefaults.Name}/{name}");

    public async IAsyncEnumerable<StoredObject> ListAsync(
        string? prefix, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        foreach (var name in objects.Keys)
        {
            if (prefix is null || name.StartsWith(prefix, StringComparison.Ordinal))
            {
                yield return new StoredObject(name, null);
            }
        }
    }

    public Task<string?> ReadTextAsync(string name, CancellationToken cancellationToken) =>
        Task.FromResult(objects.GetValueOrDefault(name));

    public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task WriteAsync(
        string name,
        ReadOnlyMemory<byte> content,
        string contentType,
        string cacheControl,
        byte[] md5,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
