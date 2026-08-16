using System.Text.Json;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using FellowshipExtractor.Extraction;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public sealed class BlobTarget(string connectionString) : IExportTarget
{
    public const string ContainerName = "icons";

    private const string ContainerKey = "ContainerName";

    private const string CacheControl = "no-cache";

    private readonly (string Account, string Name) _storage = Parse(connectionString);

    private readonly WrittenNames _names = new();

    private readonly MemoryStream _entities = new();

    private BlobContainerClient? _client;

    private Dictionary<string, string>? _published;

    private StreamWriter? _lines;

    private SettingsFile? _settings;

    private int _uploaded;

    private int _matched;

    private StreamWriter Lines() => _lines ??= new StreamWriter(_entities, leaveOpen: true);

    public string Name => _storage.Name;

    public BlobContainerClient Container =>
        _client ??= new BlobServiceClient(_storage.Account).GetBlobContainerClient(_storage.Name);

    private static (string Account, string Name) Parse(string connectionString)
    {
        List<string> account = [];
        var name = ContainerName;

        foreach (var pair in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');

            if (separator > 0 && pair[..separator].Trim().Equals(ContainerKey, StringComparison.OrdinalIgnoreCase))
            {
                var named = pair[(separator + 1)..].Trim();
                if (named.Length > 0) name = named;

                continue;
            }

            account.Add(pair);
        }

        return (string.Join(';', account), name);
    }

    private async Task<BlobContainerClient> ContainerAsync(CancellationToken cancellationToken)
    {
        var container = Container;

        if (_published is not null) return container;

        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        await container.SetAccessPolicyAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var published = new Dictionary<string, string>(StringComparer.Ordinal);
        await foreach (var blob in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (blob.Properties.ContentHash is { } hash) published[blob.Name] = Convert.ToBase64String(hash);
        }

        _published = published;
        Console.WriteLine($"container {Name} already holds {published.Count} blob(s) carrying a hash");

        return container;
    }

    private async Task PublishAsync(
        string name, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        if (_names.Repeats(name, content, out var hash)) return;

        var container = await ContainerAsync(cancellationToken);
        var stamp = Convert.ToBase64String(hash);

        if (_published!.TryGetValue(name, out var published) && published == stamp)
        {
            _matched++;
            return;
        }

        await container.GetBlobClient(name).UploadAsync(
            BinaryData.FromBytes(content),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentHash = hash,
                    ContentType = contentType,
                    CacheControl = CacheControl,
                },
            },
            cancellationToken);

        _published[name] = stamp;
        _uploaded++;
    }

    public Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken) =>
        Lines().WriteLineAsync(JsonSerializer.Serialize<EntityDocument>(document, EntityDocuments.Options));

    public Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken)
    {
        _settings = settings;
        return Task.CompletedTask;
    }

    public Task WriteImageAsync(string name, byte[] png, CancellationToken cancellationToken) =>
        PublishAsync($"{ExportLayout.UiFolder}/{name}", png, "image/png", cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_lines is not null)
        {
            await _lines.FlushAsync();
            await _lines.DisposeAsync();
        }

        if (_lines is not null && _settings is not null)
        {
            await PublishAsync(
                $"{ExportLayout.DataFolder}/{ExportLayout.EntitiesFile}",
                _entities.ToArray(),
                "application/x-ndjson",
                default);

            await PublishAsync(
                $"{ExportLayout.DataFolder}/{Settings.FileName}",
                JsonSerializer.SerializeToUtf8Bytes(_settings, ExportLayout.SettingsJson),
                "application/json",
                default);
        }
        else if (_lines is not null || _settings is not null)
        {
            var held = _lines is not null ? ExportLayout.EntitiesFile : Settings.FileName;
            var absent = _lines is not null ? Settings.FileName : ExportLayout.EntitiesFile;

            Console.Error.WriteLine(
                $"the pass ended holding {held} without {absent}, so neither was published: a snapshot is " +
                "both files from one pass, and the container keeps the pair the last finished pass left.");
        }

        await _entities.DisposeAsync();

        if (_published is not null)
        {
            Console.WriteLine(
                $"uploaded {_uploaded} blob(s) to {Container.Uri}, {_matched} already matched their hash");
        }
    }
}
