using System.Text.Json;

using FellowshipExtractor.Extraction;

using Fellowship.SDK.Documents;

namespace FellowshipExtractor.Output;

public sealed class FolderTarget(string output, string build) : IExportTarget
{
    private readonly WrittenNames _images = new();

    private StreamWriter? _entities;

    private string Data()
    {
        var data = Path.Combine(output, ExportFolders.Name(build));
        Directory.CreateDirectory(data);
        return data;
    }

    private StreamWriter Entities() =>
        _entities ??= new StreamWriter(Path.Combine(Data(), ExportLayout.EntitiesFile), append: false);

    public async Task WriteDocumentAsync(EntityDocument document, CancellationToken cancellationToken) =>
        await Entities().WriteLineAsync(JsonSerializer.Serialize<EntityDocument>(document, EntityDocuments.Options));

    public async Task WriteSettingsAsync(SettingsFile settings, CancellationToken cancellationToken) =>
        await File.WriteAllTextAsync(
            Path.Combine(Data(), Settings.FileName),
            JsonSerializer.Serialize(settings, ExportLayout.SettingsJson),
            cancellationToken);

    public async Task WriteImageAsync(string name, byte[] content, CancellationToken cancellationToken)
    {
        if (_images.Repeats(name, content, out _)) return;

        var image = Path.Combine(output, ExportLayout.UiFolder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(image)!);

        await File.WriteAllBytesAsync(image, content, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_entities is null) return;

        await _entities.FlushAsync();
        await _entities.DisposeAsync();
    }
}
