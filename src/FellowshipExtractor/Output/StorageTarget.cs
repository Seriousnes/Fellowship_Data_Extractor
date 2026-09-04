using System.Text.Json;

using FellowshipExtractor.Extraction;

using Fellowship.SDK.Documents;
using Fellowship.Storage;

namespace FellowshipExtractor.Output;

public sealed class StorageTarget(IStorage storage, string build) : IExportTarget
{
    private const string CacheControl = "no-cache";

    private readonly WrittenNames _names = new();

    private readonly MemoryStream _entities = new();

    private Dictionary<string, string>? _published;

    private StreamWriter? _lines;

    private SettingsFile? _settings;

    private int _uploaded;

    private int _matched;

    private StreamWriter Lines() => _lines ??= new StreamWriter(_entities, leaveOpen: true);

    private string Name => storage.Name;

    private async Task ReadyAsync(CancellationToken cancellationToken)
    {
        if (_published is not null) return;

        await storage.PrepareAsync(cancellationToken);

        var published = new Dictionary<string, string>(StringComparer.Ordinal);
        await foreach (var stored in storage.ListAsync(null, cancellationToken))
        {
            if (stored.Hash is { } hash) published[stored.Name] = Convert.ToBase64String(hash);
        }

        _published = published;
        Console.WriteLine($"store {Name} already holds {published.Count} object(s) carrying a hash");
    }

    private async Task PublishAsync(
        string name, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        if (_names.Repeats(name, content, out var hash)) return;

        await ReadyAsync(cancellationToken);
        var stamp = Convert.ToBase64String(hash);

        if (_published!.TryGetValue(name, out var published) && published == stamp)
        {
            _matched++;
            return;
        }

        await storage.WriteAsync(name, content, contentType, CacheControl, hash, cancellationToken);

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
                ExportLayout.Data(build, ExportLayout.EntitiesFile),
                _entities.ToArray(),
                "application/x-ndjson",
                default);

            await PublishAsync(
                ExportLayout.Data(build, Settings.FileName),
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
                "both files from one pass, and the store keeps the pair the last finished pass left.");
        }

        await _entities.DisposeAsync();

        if (_published is not null)
        {
            Console.WriteLine(
                $"uploaded {_uploaded} object(s) to {storage.Address(string.Empty)}, {_matched} already matched their hash");
        }
    }
}
