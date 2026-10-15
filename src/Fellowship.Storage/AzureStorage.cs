using System.Runtime.CompilerServices;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Fellowship.Storage;

public sealed class AzureStorage : IStorage
{
    private const string ContainerKey = "ContainerName";

    private readonly Lazy<BlobContainerClient> container;

    public string Name { get; }

    public AzureStorage(BlobContainerClient container)
    {
        this.container = new Lazy<BlobContainerClient>(container);
        Name = container.Name;
    }

    private AzureStorage(string account, string name)
    {
        container = new Lazy<BlobContainerClient>(
            () => new BlobServiceClient(account).GetBlobContainerClient(name));
        Name = name;
    }

    public static AzureStorage FromConnectionString(string connectionString)
    {
        var (account, name) = Parse(connectionString);

        return new AzureStorage(account, name);
    }

    private BlobContainerClient Container => container.Value;

    public Uri Address(string name) => new($"{Container.Uri}/{name}");

    public async IAsyncEnumerable<StoredObject> ListAsync(
        string? prefix, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var blob in Container.GetBlobsAsync(
            BlobTraits.None, BlobStates.None, prefix, cancellationToken))
        {
            yield return new StoredObject(blob.Name, blob.Properties.ContentHash);
        }
    }

    public async Task<string?> ReadTextAsync(string name, CancellationToken cancellationToken)
    {
        var blob = Container.GetBlobClient(name);

        if (!await blob.ExistsAsync(cancellationToken)) return null;

        var content = await blob.DownloadContentAsync(cancellationToken);

        return content.Value.Content.ToString();
    }

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        await Container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        await Container.SetAccessPolicyAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
    }

    public Task WriteAsync(
        string name,
        ReadOnlyMemory<byte> content,
        string contentType,
        string cacheControl,
        byte[] md5,
        CancellationToken cancellationToken) =>
        Container.GetBlobClient(name).UploadAsync(
            BinaryData.FromBytes(content),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentHash = md5,
                    ContentType = contentType,
                    CacheControl = cacheControl,
                },
            },
            cancellationToken);

    private static (string Account, string Name) Parse(string connectionString)
    {
        List<string> account = [];
        var name = StorageDefaults.Name;

        foreach (var pair in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');

            if (separator > 0 && pair[..separator].Trim().Equals(ContainerKey, StringComparison.OrdinalIgnoreCase))
            {
                var value = pair[(separator + 1)..].Trim();
                if (value.Length > 0) name = value;

                continue;
            }

            account.Add(pair);
        }

        return (string.Join(';', account), name);
    }
}
