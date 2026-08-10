using System.Text.Json;

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using FellowshipExtractor.Tooltip;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public sealed class BlobTarget(string connectionString) : IExportTarget
{
    public const string ContainerName = "icons";

    private const string CacheControl = "no-cache";

    private readonly WrittenNames _names = new();

    private readonly MemoryStream _entities = new();

    private BlobContainerClient? _container;

    private Dictionary<string, string>? _published;

    private StreamWriter? _lines;

    private SettingsFile? _settings;

    private int _uploaded;

    private int _matched;

    private StreamWriter Lines() => _lines ??= new StreamWriter(_entities, leaveOpen: true);

    private async Task<BlobContainerClient> ContainerAsync(CancellationToken cancellationToken)
    {
        if (_container is not null) return _container;

        var container = new BlobContainerClient(connectionString, ContainerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);
        await container.SetAccessPolicyAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var published = new Dictionary<string, string>(StringComparer.Ordinal);
        await foreach (var blob in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (blob.Properties.ContentHash is { } hash) published[blob.Name] = Convert.ToBase64String(hash);
        }

        _published = published;
        Console.WriteLine($"container {ContainerName} already holds {published.Count} blob(s) carrying a hash");

        return _container = container;
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

            await PublishAsync(
                $"{ExportLayout.DataFolder}/{ExportLayout.EntitiesFile}",
                _entities.ToArray(),
                "application/x-ndjson",
                default);
        }

        if (_settings is not null)
        {
            await PublishAsync(
                $"{ExportLayout.DataFolder}/{Settings.FileName}",
                JsonSerializer.SerializeToUtf8Bytes(_settings, TooltipCommand.JsonOptions),
                "application/json",
                default);
        }

        await _entities.DisposeAsync();

        if (_container is not null)
        {
            Console.WriteLine(
                $"uploaded {_uploaded} blob(s) to {_container.Uri}, {_matched} already matched their hash");
        }
    }
}
